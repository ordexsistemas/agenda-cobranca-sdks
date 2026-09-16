namespace Ordex.WhatsApp.Sdk.Metering;

public static class MeteringMath
{
    public static long BillableUnits(long used, long freeAllowance) =>
        Math.Max(0, used - Math.Max(0, freeAllowance));

    /// <summary>
    /// Quantas unidades do <paramref name="delta"/> recém-aplicado são faturáveis, considerando a franquia.
    /// Ex.: service usedBefore=950, delta=100, free=1000 → billableDelta=50.
    /// </summary>
    public static (long BillableDelta, long FreeDelta) BillableDeltaForIncrement(
        long usedBefore,
        long delta,
        long freeAllowance)
    {
        if (delta <= 0)
        {
            var billableBefore = BillableUnits(usedBefore, freeAllowance);
            var billableAfter = BillableUnits(usedBefore + delta, freeAllowance);
            return (billableAfter - billableBefore, 0);
        }

        var usedAfter = usedBefore + delta;
        var before = BillableUnits(usedBefore, freeAllowance);
        var after = BillableUnits(usedAfter, freeAllowance);
        var billableDelta = after - before;
        return (billableDelta, delta - billableDelta);
    }

    public static MeteringDecision EvaluateUsage(
        long used,
        CategoryQuota quota,
        QuotaMode quotaMode,
        string period,
        MessageCategory category,
        long delta)
    {
        var remaining = Math.Max(0, quota.MonthlyQuota - used);
        var overageDelta = Math.Max(0, used - quota.MonthlyQuota);
        var usedBefore = used - delta;
        var (billableDelta, freeDelta) = BillableDeltaForIncrement(usedBefore, delta, quota.FreeAllowance);
        var overage = used > quota.MonthlyQuota;
        var allowed = quotaMode == QuotaMode.Soft || used <= quota.MonthlyQuota;
        return new MeteringDecision(
            category,
            period,
            used,
            quota.MonthlyQuota,
            remaining,
            billableDelta,
            freeDelta,
            overage,
            overageDelta,
            allowed,
            quotaMode);
    }
}

public sealed class InMemoryUsageStore : IUsageStore
{
    private readonly Dictionary<string, UsageSnapshot> _data = new();
    private readonly object _gate = new();

    public InMemoryUsageStore(IEnumerable<UsageSnapshot>? initial = null)
    {
        if (initial is null) return;
        foreach (var snap in initial)
        {
            _data[KeyOf(snap.TenantId, snap.Period)] = Clone(snap);
        }
    }

    public Task<UsageSnapshot> GetAsync(string tenantId, string period, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_data.TryGetValue(KeyOf(tenantId, period), out var found)
                ? Clone(found)
                : new UsageSnapshot(tenantId, period, CenarioBase.EmptyUsageCounts()));
        }
    }

    public Task<UsageSnapshot> IncrementAsync(
        string tenantId,
        string period,
        MessageCategory category,
        long delta,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var current = _data.TryGetValue(KeyOf(tenantId, period), out var found)
                ? Clone(found)
                : new UsageSnapshot(tenantId, period, CenarioBase.EmptyUsageCounts());
            current.Counts[category] = Math.Max(0, current.Counts[category] + delta);
            _data[KeyOf(tenantId, period)] = current;
            return Task.FromResult(Clone(current));
        }
    }

    private static string KeyOf(string tenantId, string period) => $"{tenantId}:{period}";

    private static UsageSnapshot Clone(UsageSnapshot snap) =>
        new(snap.TenantId, snap.Period, snap.Counts.Clone());
}

public sealed class MeteringService
{
    private readonly PlanQuotas _plan;
    private readonly IUsageStore _store;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string _timeZone;

    public MeteringService(
        PlanQuotas plan,
        IUsageStore store,
        Func<DateTimeOffset>? clock = null,
        string timeZone = "America/Sao_Paulo")
    {
        _plan = plan;
        _store = store;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _timeZone = timeZone;
    }

    public string Period(DateTimeOffset? at = null) =>
        CenarioBase.BillingPeriod(at ?? _clock(), _timeZone);

    public Task<UsageSnapshot> SnapshotAsync(string tenantId, string? period = null, CancellationToken cancellationToken = default) =>
        _store.GetAsync(tenantId, period ?? Period(), cancellationToken);

    public async Task<MeteringDecision> ConsumeAsync(
        string tenantId,
        MessageCategory category,
        long delta = 1,
        CancellationToken cancellationToken = default)
    {
        if (delta <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "delta de consumo deve ser positivo");
        }

        var period = Period();
        var quota = _plan.QuotaFor(category);
        var snapshot = await _store.IncrementAsync(tenantId, period, category, delta, cancellationToken).ConfigureAwait(false);
        var used = snapshot.Counts[category];
        var decision = MeteringMath.EvaluateUsage(used, quota, _plan.QuotaMode, period, category, delta);
        if (!decision.Allowed)
        {
            await _store.IncrementAsync(tenantId, period, category, -delta, cancellationToken).ConfigureAwait(false);
            throw new WhatsAppQuotaExceededException(
                $"Cota {category.ToApi()} excedida no periodo {period} ({used}/{quota.MonthlyQuota}, modo hard)",
                category.ToApi(),
                used,
                quota.MonthlyQuota,
                period);
        }

        return decision;
    }

    public Task<UsageSnapshot> RollbackAsync(
        string tenantId,
        MessageCategory category,
        long delta = 1,
        CancellationToken cancellationToken = default) =>
        _store.IncrementAsync(tenantId, Period(), category, -Math.Abs(delta), cancellationToken);
}
