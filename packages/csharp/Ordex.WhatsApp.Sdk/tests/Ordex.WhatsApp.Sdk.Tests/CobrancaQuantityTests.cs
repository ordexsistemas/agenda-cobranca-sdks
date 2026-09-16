namespace Ordex.WhatsApp.Sdk.Tests;

public class CobrancaQuantityTests
{
    private static readonly ConversionOptions Opts = new() { Strategy = QuantityStrategy.PerCategory };

    [Fact]
    public void Per_category_uma_cobranca_por_categoria_faturavel()
    {
        var plan = CobrancaQuantity.Compute(CenarioBase.Volume, CenarioBase.Plan, "2026-09", "acme", Opts);
        Assert.Equal(4, plan.TotalQuantity);
        Assert.Equal(4105m, plan.TotalCostUsd);
        Assert.Equal((long)Math.Round(4105m * CenarioBase.DefaultUsdToBrl * 100m, MidpointRounding.AwayFromZero), plan.TotalCostBrlCentavos);

        var byCat = plan.Categories.ToDictionary(c => c.Category);
        Assert.Equal(40_000, byCat[MessageCategory.Auth].Billable);
        Assert.Equal(1260m, byCat[MessageCategory.Auth].CostUsd);
        Assert.Equal(693_000, byCat[MessageCategory.Auth].CostBrlCentavos);

        Assert.Equal(60_000, byCat[MessageCategory.Utility].Billable);
        Assert.Equal(2100m, byCat[MessageCategory.Utility].CostUsd);
        Assert.Equal(1_155_000, byCat[MessageCategory.Utility].CostBrlCentavos);

        Assert.Equal(5_000, byCat[MessageCategory.Service].Used);
        Assert.Equal(1_000, byCat[MessageCategory.Service].Free);
        Assert.Equal(4_000, byCat[MessageCategory.Service].Billable);
        Assert.Equal(120m, byCat[MessageCategory.Service].CostUsd);
        Assert.Equal(66_000, byCat[MessageCategory.Service].CostBrlCentavos);

        Assert.Equal(10_000, byCat[MessageCategory.Marketing].Billable);
        Assert.Equal(625m, byCat[MessageCategory.Marketing].CostUsd);
        Assert.Equal(343_750, byCat[MessageCategory.Marketing].CostBrlCentavos);
        Assert.Equal(1, byCat[MessageCategory.Marketing].Quantity);
        Assert.Equal(new long[] { 343_750 }, byCat[MessageCategory.Marketing].ValorCentavosEach);
    }

    [Fact]
    public void Service_so_com_franquia_nao_gera_cobranca()
    {
        var plan = CobrancaQuantity.Compute(
            new UsageCounts { Service = 1_000 },
            CenarioBase.Plan,
            "2026-09",
            "acme",
            Opts);
        Assert.Equal(0, plan.TotalQuantity);
        Assert.Equal(0m, plan.TotalCostUsd);
        Assert.Equal(0, plan.Categories.Single(c => c.Category == MessageCategory.Service).Billable);
    }

    [Fact]
    public void By_sessions_converte_uso_em_quantidade()
    {
        var plan = CobrancaQuantity.Compute(CenarioBase.Volume, CenarioBase.Plan, "2026-09", "acme", new ConversionOptions
        {
            Strategy = QuantityStrategy.BySessions,
            SessionsPerCobranca = 10_000
        });
        var byCat = plan.Categories.ToDictionary(c => c.Category, c => c.Quantity);
        Assert.Equal(4, byCat[MessageCategory.Auth]);
        Assert.Equal(6, byCat[MessageCategory.Utility]);
        Assert.Equal(1, byCat[MessageCategory.Service]);
        Assert.Equal(1, byCat[MessageCategory.Marketing]);
        Assert.Equal(12, plan.TotalQuantity);
    }

    [Fact]
    public void By_value_parte_o_custo_brl()
    {
        var plan = CobrancaQuantity.Compute(CenarioBase.Volume, CenarioBase.Plan, "2026-09", "acme", new ConversionOptions
        {
            Strategy = QuantityStrategy.ByValue,
            ValorCentavosPorCobranca = 100_000
        });
        var byCat = plan.Categories.ToDictionary(c => c.Category);
        Assert.Equal(7, byCat[MessageCategory.Auth].Quantity);
        Assert.Equal(12, byCat[MessageCategory.Utility].Quantity);
        Assert.Equal(1, byCat[MessageCategory.Service].Quantity);
        Assert.Equal(4, byCat[MessageCategory.Marketing].Quantity);
        Assert.Equal(693_000, byCat[MessageCategory.Auth].ValorCentavosEach.Sum());
    }

    [Fact]
    public void Uso_parcial_reduz_quantidade_em_by_sessions()
    {
        var plan = CobrancaQuantity.Compute(
            new UsageCounts { Auth = 10_000 },
            CenarioBase.Plan,
            "2026-09",
            "acme",
            new ConversionOptions { Strategy = QuantityStrategy.BySessions, SessionsPerCobranca = 10_000 });
        Assert.Equal(1, plan.TotalQuantity);
        Assert.Equal(315m, plan.Categories.Single(c => c.Category == MessageCategory.Auth).CostUsd);
    }

    [Fact]
    public void Split_centavos_e_referencia_deterministica()
    {
        Assert.Equal(new long[] { 34, 33, 33 }, CobrancaQuantity.SplitCentavos(100, 3));
        Assert.Equal(new long[] { 0, 0 }, CobrancaQuantity.SplitCentavos(0, 2));
        Assert.Empty(CobrancaQuantity.SplitCentavos(50, 0));

        var reference = CobrancaQuantity.ExternalReference("acme", "2026-09", MessageCategory.Utility, 2);
        Assert.Equal("wa:acme:2026-09:utility:2", reference);
        var parsed = CobrancaQuantity.ParseExternalReference(reference);
        Assert.NotNull(parsed);
        Assert.Equal("acme", parsed.Value.TenantId);
        Assert.Equal("2026-09", parsed.Value.Period);
        Assert.Equal(MessageCategory.Utility, parsed.Value.Category);
        Assert.Equal(2, parsed.Value.Index);
        Assert.Null(CobrancaQuantity.ParseExternalReference("pedido-1001"));
    }
}
