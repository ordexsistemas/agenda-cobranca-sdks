package whatsapp

import (
	"testing"
	"time"
)

func TestFranquiaService(t *testing.T) {
	if BillableUnits(0, 1000) != 0 || BillableUnits(1000, 1000) != 0 {
		t.Fatal("franquia deveria zerar")
	}
	if BillableUnits(1001, 1000) != 1 || BillableUnits(5000, 1000) != 4000 {
		t.Fatal("billable inesperado")
	}
	b, f := BillableDeltaForIncrement(950, 100, 1000)
	if b != 50 || f != 50 {
		t.Fatalf("delta atraves da franquia: %d %d", b, f)
	}
}

func TestMeteringAcumula(t *testing.T) {
	clock := func() time.Time { return time.Date(2026, 9, 16, 12, 0, 0, 0, time.UTC) }
	m := NewMeteringService(CenarioBasePlan, NewInMemoryUsageStore(), clock, "America/Sao_Paulo")
	d1, err := m.Consume("tenant-a", CategoryAuth, 3)
	if err != nil {
		t.Fatal(err)
	}
	if d1.Period != "2026-09" || d1.Used != 3 || d1.BillableDelta != 3 {
		t.Fatalf("%+v", d1)
	}
	if _, err := m.Consume("tenant-a", CategoryUtility, 1); err != nil {
		t.Fatal(err)
	}
	snap, _ := m.Snapshot("tenant-a", "")
	if snap.Counts.Auth != 3 || snap.Counts.Utility != 1 {
		t.Fatalf("%+v", snap.Counts)
	}
}

func TestMeteringHardNaoPersiste(t *testing.T) {
	quota := int64(2)
	plan := MergePlan(&PlanOverride{
		QuotaMode: QuotaHard,
		Categories: map[MessageCategory]CategoryQuotaPatch{
			CategoryAuth: {MonthlyQuota: &quota},
		},
	}, QuotaHard)
	clock := func() time.Time { return time.Date(2026, 9, 16, 12, 0, 0, 0, time.UTC) }
	m := NewMeteringService(plan, NewInMemoryUsageStore(), clock, "")
	if _, err := m.Consume("t", CategoryAuth, 2); err != nil {
		t.Fatal(err)
	}
	_, err := m.Consume("t", CategoryAuth, 1)
	if _, ok := err.(*QuotaExceededError); !ok {
		t.Fatalf("esperado QuotaExceededError, got %v", err)
	}
	snap, _ := m.Snapshot("t", "")
	if snap.Counts.Auth != 2 {
		t.Fatalf("persistiu excedente: %d", snap.Counts.Auth)
	}
}

func TestMeteringSoftOverage(t *testing.T) {
	quota := int64(2)
	plan := MergePlan(&PlanOverride{
		QuotaMode: QuotaSoft,
		Categories: map[MessageCategory]CategoryQuotaPatch{
			CategoryMarketing: {MonthlyQuota: &quota},
		},
	}, QuotaSoft)
	clock := func() time.Time { return time.Date(2026, 9, 16, 12, 0, 0, 0, time.UTC) }
	m := NewMeteringService(plan, NewInMemoryUsageStore(), clock, "")
	if _, err := m.Consume("t", CategoryMarketing, 2); err != nil {
		t.Fatal(err)
	}
	over, err := m.Consume("t", CategoryMarketing, 1)
	if err != nil {
		t.Fatal(err)
	}
	if !over.Allowed || !over.Overage || over.OverageDelta != 1 || over.Used != 3 {
		t.Fatalf("%+v", over)
	}
}

func TestServiceFaturaApos1000(t *testing.T) {
	clock := func() time.Time { return time.Date(2026, 9, 16, 12, 0, 0, 0, time.UTC) }
	m := NewMeteringService(CenarioBasePlan, NewInMemoryUsageStore(), clock, "")
	first, err := m.Consume("t", CategoryService, 1000)
	if err != nil {
		t.Fatal(err)
	}
	if first.BillableDelta != 0 || first.FreeDelta != 1000 {
		t.Fatalf("%+v", first)
	}
	next, err := m.Consume("t", CategoryService, 5)
	if err != nil {
		t.Fatal(err)
	}
	if next.BillableDelta != 5 || next.Used != 1005 {
		t.Fatalf("%+v", next)
	}
}
