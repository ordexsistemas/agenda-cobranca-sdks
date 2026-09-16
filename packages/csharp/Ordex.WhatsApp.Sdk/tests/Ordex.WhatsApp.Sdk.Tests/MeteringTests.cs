using Ordex.WhatsApp.Sdk.Metering;

namespace Ordex.WhatsApp.Sdk.Tests;

public class MeteringTests
{
    [Fact]
    public void Franquia_service_nao_fatura_os_primeiros_1000()
    {
        Assert.Equal(0, MeteringMath.BillableUnits(0, 1_000));
        Assert.Equal(0, MeteringMath.BillableUnits(1_000, 1_000));
        Assert.Equal(1, MeteringMath.BillableUnits(1_001, 1_000));
        Assert.Equal(4_000, MeteringMath.BillableUnits(5_000, 1_000));
    }

    [Fact]
    public void Reparte_delta_que_atravessa_a_franquia()
    {
        Assert.Equal((50, 50), MeteringMath.BillableDeltaForIncrement(950, 100, 1_000));
        Assert.Equal((0, 500), MeteringMath.BillableDeltaForIncrement(0, 500, 1_000));
        Assert.Equal((10, 0), MeteringMath.BillableDeltaForIncrement(1_000, 10, 1_000));
    }

    [Fact]
    public async Task Acumula_por_categoria_e_periodo()
    {
        var clock = () => DateTimeOffset.Parse("2026-09-16T12:00:00Z");
        var metering = new MeteringService(CenarioBase.Plan, new InMemoryUsageStore(), clock);
        var d1 = await metering.ConsumeAsync("tenant-a", MessageCategory.Auth, 3);
        var d2 = await metering.ConsumeAsync("tenant-a", MessageCategory.Utility, 1);
        Assert.Equal("2026-09", d1.Period);
        Assert.Equal(3, d1.Used);
        Assert.Equal(3, d1.BillableDelta);
        Assert.Equal(MessageCategory.Utility, d2.Category);
        var snap = await metering.SnapshotAsync("tenant-a");
        Assert.Equal(3, snap.Counts.Auth);
        Assert.Equal(1, snap.Counts.Utility);
        Assert.Equal(0, snap.Counts.Service);
        Assert.Equal(0, snap.Counts.Marketing);
    }

    [Fact]
    public async Task Modo_hard_bloqueia_e_nao_persiste_excedente()
    {
        var plan = CenarioBase.MergePlan(new PlanOverride
        {
            QuotaMode = QuotaMode.Hard,
            Categories = new Dictionary<MessageCategory, CategoryQuotaOverride>
            {
                [MessageCategory.Auth] = new() { MonthlyQuota = 2, UnitCostUsd = 0.0315m, FreeAllowance = 0 }
            }
        });
        var metering = new MeteringService(plan, new InMemoryUsageStore(), () => DateTimeOffset.Parse("2026-09-16T12:00:00Z"));
        await metering.ConsumeAsync("t", MessageCategory.Auth, 2);
        await Assert.ThrowsAsync<WhatsAppQuotaExceededException>(() => metering.ConsumeAsync("t", MessageCategory.Auth, 1));
        var snap = await metering.SnapshotAsync("t");
        Assert.Equal(2, snap.Counts.Auth);
    }

    [Fact]
    public async Task Modo_soft_permite_envio_e_marca_overage()
    {
        var plan = CenarioBase.MergePlan(new PlanOverride
        {
            QuotaMode = QuotaMode.Soft,
            Categories = new Dictionary<MessageCategory, CategoryQuotaOverride>
            {
                [MessageCategory.Marketing] = new() { MonthlyQuota = 2, UnitCostUsd = 0.0625m, FreeAllowance = 0 }
            }
        });
        var metering = new MeteringService(plan, new InMemoryUsageStore(), () => DateTimeOffset.Parse("2026-09-16T12:00:00Z"));
        await metering.ConsumeAsync("t", MessageCategory.Marketing, 2);
        var over = await metering.ConsumeAsync("t", MessageCategory.Marketing, 1);
        Assert.True(over.Allowed);
        Assert.True(over.Overage);
        Assert.Equal(1, over.OverageDelta);
        Assert.Equal(3, over.Used);
        Assert.Equal(0, over.Remaining);
    }

    [Fact]
    public async Task Service_so_comeca_a_faturar_apos_1000()
    {
        var metering = new MeteringService(CenarioBase.Plan, new InMemoryUsageStore(), () => DateTimeOffset.Parse("2026-09-16T12:00:00Z"));
        var first = await metering.ConsumeAsync("t", MessageCategory.Service, 1_000);
        Assert.Equal(0, first.BillableDelta);
        Assert.Equal(1_000, first.FreeDelta);
        Assert.False(first.Overage);
        var next = await metering.ConsumeAsync("t", MessageCategory.Service, 5);
        Assert.Equal(5, next.BillableDelta);
        Assert.Equal(0, next.FreeDelta);
        Assert.Equal(1_005, next.Used);
    }
}
