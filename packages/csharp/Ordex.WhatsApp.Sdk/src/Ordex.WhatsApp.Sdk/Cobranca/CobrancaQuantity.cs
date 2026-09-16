using System.Text.RegularExpressions;

namespace Ordex.WhatsApp.Sdk;

public static class CobrancaQuantity
{
    public const long DefaultSessionsPerCobranca = 10_000;
    public const long DefaultValorCentavosPorCobranca = 100_000;

    public static IReadOnlyList<long> SplitCentavos(long total, int quantity)
    {
        if (quantity <= 0) return Array.Empty<long>();
        if (total <= 0) return Enumerable.Repeat(0L, quantity).ToArray();
        var baseValue = total / quantity;
        var remainder = total - baseValue * quantity;
        return Enumerable.Range(0, quantity)
            .Select(i => baseValue + (i < remainder ? 1 : 0))
            .ToArray();
    }

    public static long CostBrlCentavos(long billable, decimal unitCostUsd, decimal usdToBrl) =>
        (long)Math.Round(billable * unitCostUsd * usdToBrl * 100m, MidpointRounding.AwayFromZero);

    public static decimal CostUsd(long billable, decimal unitCostUsd) =>
        decimal.Round(billable * unitCostUsd, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converte o uso WhatsApp por categoria na quantidade de cobranças a criar/ajustar
    /// na Agenda de Cobranças no período de faturamento.
    /// </summary>
    public static CobrancaQuantityPlan Compute(
        UsageCounts counts,
        PlanQuotas plan,
        string period,
        string tenantId,
        ConversionOptions? options = null)
    {
        options ??= new ConversionOptions();
        var strategy = options.Strategy;
        var categories = new List<CategoryCobrancaPlan>();

        foreach (var category in MessageCategories.All)
        {
            var quota = plan.QuotaFor(category);
            var used = Math.Max(0, counts[category]);
            var billable = Metering.MeteringMath.BillableUnits(used, quota.FreeAllowance);
            var free = Math.Min(used, Math.Max(0, quota.FreeAllowance));
            var overage = Math.Max(0, used - quota.MonthlyQuota);
            var usd = CostUsd(billable, quota.UnitCostUsd);
            var brl = CostBrlCentavos(billable, quota.UnitCostUsd, plan.UsdToBrl);
            var quantity = QuantityFor(strategy, category, billable, brl, options);
            categories.Add(new CategoryCobrancaPlan(
                category,
                used,
                billable,
                free,
                quota.MonthlyQuota,
                overage,
                quota.UnitCostUsd,
                usd,
                brl,
                quantity,
                SplitCentavos(brl, quantity)));
        }

        return new CobrancaQuantityPlan(
            period,
            tenantId,
            categories,
            categories.Sum(c => c.Quantity),
            decimal.Round(categories.Sum(c => c.CostUsd), 4, MidpointRounding.AwayFromZero),
            categories.Sum(c => c.CostBrlCentavos),
            strategy);
    }

    public static string ExternalReference(string tenantId, string period, MessageCategory category, int index) =>
        $"wa:{tenantId}:{period}:{category.ToApi()}:{index}";

    public static (string TenantId, string Period, MessageCategory Category, int Index)? ParseExternalReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var match = Regex.Match(reference, @"^wa:([^:]+):(\d{4}-\d{2}):(auth|utility|service|marketing):(\d+)$");
        if (!match.Success) return null;
        return (
            match.Groups[1].Value,
            match.Groups[2].Value,
            MessageCategories.Parse(match.Groups[3].Value),
            int.Parse(match.Groups[4].Value));
    }

    private static int QuantityFor(
        QuantityStrategy strategy,
        MessageCategory category,
        long billable,
        long costCents,
        ConversionOptions options)
    {
        if (billable <= 0 && costCents <= 0)
        {
            return options.IncludeZeroCategories ? 1 : 0;
        }

        return strategy switch
        {
            QuantityStrategy.BySessions => Math.Max(1, (int)Math.Ceiling(billable / (double)SessionsPer(category, options))),
            QuantityStrategy.ByValue => QuantityByValue(costCents, options.ValorCentavosPorCobranca),
            _ => 1
        };
    }

    private static int QuantityByValue(long costCents, long per)
    {
        if (per <= 0) return 1;
        return Math.Max(1, (int)Math.Ceiling(costCents / (double)per));
    }

    private static long SessionsPer(MessageCategory category, ConversionOptions options)
    {
        if (options.SessionsPerCobrancaByCategory is not null &&
            options.SessionsPerCobrancaByCategory.TryGetValue(category, out var n) &&
            n > 0)
        {
            return n;
        }

        return options.SessionsPerCobranca > 0 ? options.SessionsPerCobranca : DefaultSessionsPerCobranca;
    }
}
