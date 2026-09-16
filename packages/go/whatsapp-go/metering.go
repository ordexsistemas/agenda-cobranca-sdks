package whatsapp

import (
	"fmt"
	"sync"
	"time"
)

func BillableUnits(used, freeAllowance int64) int64 {
	free := freeAllowance
	if free < 0 {
		free = 0
	}
	out := used - free
	if out < 0 {
		return 0
	}
	return out
}

func BillableDeltaForIncrement(usedBefore, delta, freeAllowance int64) (billableDelta, freeDelta int64) {
	if delta <= 0 {
		before := BillableUnits(usedBefore, freeAllowance)
		after := BillableUnits(usedBefore+delta, freeAllowance)
		return after - before, 0
	}
	before := BillableUnits(usedBefore, freeAllowance)
	after := BillableUnits(usedBefore+delta, freeAllowance)
	billableDelta = after - before
	return billableDelta, delta - billableDelta
}

func EvaluateUsage(used int64, quota CategoryQuota, mode QuotaMode, period string, category MessageCategory, delta int64) MeteringDecision {
	remaining := quota.MonthlyQuota - used
	if remaining < 0 {
		remaining = 0
	}
	overageDelta := used - quota.MonthlyQuota
	if overageDelta < 0 {
		overageDelta = 0
	}
	usedBefore := used - delta
	billableDelta, freeDelta := BillableDeltaForIncrement(usedBefore, delta, quota.FreeAllowance)
	overage := used > quota.MonthlyQuota
	allowed := mode == QuotaSoft || used <= quota.MonthlyQuota
	return MeteringDecision{
		Category:      category,
		Period:        period,
		Used:          used,
		Quota:         quota.MonthlyQuota,
		Remaining:     remaining,
		BillableDelta: billableDelta,
		FreeDelta:     freeDelta,
		Overage:       overage,
		OverageDelta:  overageDelta,
		Allowed:       allowed,
		QuotaMode:     mode,
	}
}

type InMemoryUsageStore struct {
	mu   sync.Mutex
	data map[string]UsageSnapshot
}

func NewInMemoryUsageStore() *InMemoryUsageStore {
	return &InMemoryUsageStore{data: map[string]UsageSnapshot{}}
}

func (s *InMemoryUsageStore) Get(tenantID, period string) (UsageSnapshot, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	if found, ok := s.data[tenantID+":"+period]; ok {
		return cloneSnap(found), nil
	}
	return UsageSnapshot{TenantID: tenantID, Period: period, Counts: EmptyUsageCounts()}, nil
}

func (s *InMemoryUsageStore) Increment(tenantID, period string, category MessageCategory, delta int64) (UsageSnapshot, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	key := tenantID + ":" + period
	current, ok := s.data[key]
	if !ok {
		current = UsageSnapshot{TenantID: tenantID, Period: period, Counts: EmptyUsageCounts()}
	}
	current.Counts.Add(category, delta)
	s.data[key] = current
	return cloneSnap(current), nil
}

func cloneSnap(s UsageSnapshot) UsageSnapshot {
	return UsageSnapshot{TenantID: s.TenantID, Period: s.Period, Counts: s.Counts}
}

type MeteringService struct {
	plan     PlanQuotas
	store    UsageStore
	clock    func() time.Time
	timeZone string
}

func NewMeteringService(plan PlanQuotas, store UsageStore, clock func() time.Time, timeZone string) *MeteringService {
	if clock == nil {
		clock = time.Now
	}
	if timeZone == "" {
		timeZone = "America/Sao_Paulo"
	}
	return &MeteringService{plan: plan, store: store, clock: clock, timeZone: timeZone}
}

func (m *MeteringService) Period() string {
	return BillingPeriod(m.clock(), m.timeZone)
}

func (m *MeteringService) Snapshot(tenantID, period string) (UsageSnapshot, error) {
	if period == "" {
		period = m.Period()
	}
	return m.store.Get(tenantID, period)
}

func (m *MeteringService) Consume(tenantID string, category MessageCategory, delta int64) (MeteringDecision, error) {
	if delta <= 0 {
		return MeteringDecision{}, fmt.Errorf("delta de consumo deve ser positivo")
	}
	period := m.Period()
	quota := QuotaFor(m.plan, category)
	snapshot, err := m.store.Increment(tenantID, period, category, delta)
	if err != nil {
		return MeteringDecision{}, err
	}
	used := snapshot.Counts.Get(category)
	decision := EvaluateUsage(used, quota, m.plan.QuotaMode, period, category, delta)
	if !decision.Allowed {
		_, _ = m.store.Increment(tenantID, period, category, -delta)
		return MeteringDecision{}, &QuotaExceededError{
			Message:  fmt.Sprintf("Cota %s excedida no periodo %s (%d/%d, modo hard)", category, period, used, quota.MonthlyQuota),
			Category: category,
			Used:     used,
			Quota:    quota.MonthlyQuota,
			Period:   period,
		}
	}
	return decision, nil
}

func (m *MeteringService) Rollback(tenantID string, category MessageCategory, delta int64) (UsageSnapshot, error) {
	if delta < 0 {
		delta = -delta
	}
	return m.store.Increment(tenantID, m.Period(), category, -delta)
}
