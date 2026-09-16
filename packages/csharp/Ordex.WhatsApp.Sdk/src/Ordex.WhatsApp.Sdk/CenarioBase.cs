namespace Ordex.WhatsApp.Sdk;

/// <summary>
/// Quotas e custos unitários do Cenário Base (~100k transações/mês).
/// Configuráveis por tenant via <see cref="CenarioBase.MergePlan"/>.
/// </summary>
public static class CenarioBase
{
    public const decimal DefaultUsdToBrl = 5.5m;
    public const long ServiceFreeAllowance = 1_000;

    public static readonly IReadOnlyDictionary<MessageCategory, CategoryQuota> Categories =
        new Dictionary<MessageCategory, CategoryQuota>
        {
            [MessageCategory.Auth] = new(40_000, 0.0315m, 0),
            [MessageCategory.Utility] = new(60_000, 0.0350m, 0),
            [MessageCategory.Service] = new(5_000, 0.0300m, ServiceFreeAllowance),
            [MessageCategory.Marketing] = new(10_000, 0.0625m, 0)
        };

    public static UsageCounts Volume => new()
    {
        Auth = 40_000,
        Utility = 60_000,
        Service = 5_000,
        Marketing = 10_000
    };

    public static PlanQuotas Plan { get; } = new()
    {
        Categories = Categories,
        UsdToBrl = DefaultUsdToBrl,
        QuotaMode = QuotaMode.Hard
    };

    public static UsageCounts EmptyUsageCounts() => new();

    public static PlanQuotas MergePlan(PlanOverride? overrides = null, QuotaMode? quotaMode = null)
    {
        var categories = new Dictionary<MessageCategory, CategoryQuota>(Categories);
        if (overrides?.Categories is not null)
        {
            foreach (var (key, patch) in overrides.Categories)
            {
                var current = categories[key];
                categories[key] = new CategoryQuota(
                    patch.MonthlyQuota ?? current.MonthlyQuota,
                    patch.UnitCostUsd ?? current.UnitCostUsd,
                    patch.FreeAllowance ?? current.FreeAllowance);
            }
        }

        return new PlanQuotas
        {
            Categories = categories,
            UsdToBrl = overrides?.UsdToBrl ?? DefaultUsdToBrl,
            QuotaMode = quotaMode ?? overrides?.QuotaMode ?? QuotaMode.Hard
        };
    }

    public static string BillingPeriod(DateTimeOffset at, string timeZone = "America/Sao_Paulo")
    {
        var tz = ResolveTimeZone(timeZone);
        var local = TimeZoneInfo.ConvertTime(at, tz);
        return $"{local.Year:D4}-{local.Month:D2}";
    }

    public static string VencimentoForPeriod(string period, int? day = null)
    {
        var (year, month) = ParsePeriod(period);
        var last = DateTime.DaysInMonth(year, month);
        var chosen = day is null or <= 0 ? last : Math.Min(day.Value, last);
        return $"{period}-{chosen:D2}";
    }

    internal static (int Year, int Month) ParsePeriod(string period)
    {
        var parts = period.Split('-');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month))
        {
            throw new ArgumentException($"periodo invalido: {period}", nameof(period));
        }

        return (year, month);
    }

    internal static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
