package whatsapp

import "testing"

func TestComputeCobrancaQuantityCenarioBase(t *testing.T) {
	opts := ConversionOptions{Period: "2026-09", TenantID: "acme", Strategy: StrategyPerCategory}
	plan := ComputeCobrancaQuantity(CenarioBaseVolume, CenarioBasePlan, opts)
	if plan.TotalQuantity != 4 {
		t.Fatalf("qty %d", plan.TotalQuantity)
	}
	if plan.TotalCostUSD != 4105 {
		t.Fatalf("usd %v", plan.TotalCostUSD)
	}
	by := map[MessageCategory]CategoryCobrancaPlan{}
	for _, c := range plan.Categories {
		by[c.Category] = c
	}
	if by[CategoryAuth].CostUSD != 1260 || by[CategoryAuth].CostBrlCentavos != 693000 {
		t.Fatalf("auth %+v", by[CategoryAuth])
	}
	if by[CategoryUtility].CostUSD != 2100 || by[CategoryUtility].CostBrlCentavos != 1155000 {
		t.Fatalf("utility %+v", by[CategoryUtility])
	}
	if by[CategoryService].Billable != 4000 || by[CategoryService].Free != 1000 || by[CategoryService].CostUSD != 120 {
		t.Fatalf("service %+v", by[CategoryService])
	}
	if by[CategoryMarketing].CostUSD != 625 || by[CategoryMarketing].CostBrlCentavos != 343750 {
		t.Fatalf("marketing %+v", by[CategoryMarketing])
	}
}

func TestServiceSoFranquiaNaoGeraCobranca(t *testing.T) {
	plan := ComputeCobrancaQuantity(UsageCounts{Service: 1000}, CenarioBasePlan, ConversionOptions{
		Period: "2026-09", TenantID: "acme", Strategy: StrategyPerCategory,
	})
	if plan.TotalQuantity != 0 || plan.TotalCostUSD != 0 {
		t.Fatalf("%+v", plan)
	}
}

func TestBySessions(t *testing.T) {
	plan := ComputeCobrancaQuantity(CenarioBaseVolume, CenarioBasePlan, ConversionOptions{
		Period: "2026-09", TenantID: "acme", Strategy: StrategyBySessions, SessionsPerCobranca: 10_000,
	})
	by := map[MessageCategory]int{}
	for _, c := range plan.Categories {
		by[c.Category] = c.Quantity
	}
	if by[CategoryAuth] != 4 || by[CategoryUtility] != 6 || by[CategoryService] != 1 || by[CategoryMarketing] != 1 {
		t.Fatalf("%v", by)
	}
	if plan.TotalQuantity != 12 {
		t.Fatalf("total %d", plan.TotalQuantity)
	}
}

func TestByValue(t *testing.T) {
	plan := ComputeCobrancaQuantity(CenarioBaseVolume, CenarioBasePlan, ConversionOptions{
		Period: "2026-09", TenantID: "acme", Strategy: StrategyByValue, ValorCentavosPorCobranca: 100_000,
	})
	by := map[MessageCategory]CategoryCobrancaPlan{}
	for _, c := range plan.Categories {
		by[c.Category] = c
	}
	if by[CategoryAuth].Quantity != 7 || by[CategoryUtility].Quantity != 12 || by[CategoryService].Quantity != 1 || by[CategoryMarketing].Quantity != 4 {
		t.Fatalf("%+v", by)
	}
	var sum int64
	for _, v := range by[CategoryAuth].ValorCentavosEach {
		sum += v
	}
	if sum != 693000 {
		t.Fatalf("split %d", sum)
	}
}

func TestSplitEReferencia(t *testing.T) {
	got := SplitCentavos(100, 3)
	if len(got) != 3 || got[0] != 34 || got[1] != 33 || got[2] != 33 {
		t.Fatalf("%v", got)
	}
	ref := CobrancaExternalReference("acme", "2026-09", CategoryUtility, 2)
	if ref != "wa:acme:2026-09:utility:2" {
		t.Fatal(ref)
	}
	tenant, period, cat, idx, ok := ParseCobrancaExternalReference(ref)
	if !ok || tenant != "acme" || period != "2026-09" || cat != CategoryUtility || idx != 2 {
		t.Fatal("parse")
	}
	if _, _, _, _, ok := ParseCobrancaExternalReference("pedido-1001"); ok {
		t.Fatal("deveria falhar")
	}
}
