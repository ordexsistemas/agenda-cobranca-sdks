package whatsapp

import (
	"fmt"
	"time"
)

var CenarioBaseCategories = map[MessageCategory]CategoryQuota{
	CategoryAuth:      {MonthlyQuota: 40_000, UnitCostUSD: 0.0315, FreeAllowance: 0},
	CategoryUtility:   {MonthlyQuota: 60_000, UnitCostUSD: 0.0350, FreeAllowance: 0},
	CategoryService:   {MonthlyQuota: 5_000, UnitCostUSD: 0.0300, FreeAllowance: ServiceFreeAllowance},
	CategoryMarketing: {MonthlyQuota: 10_000, UnitCostUSD: 0.0625, FreeAllowance: 0},
}

var CenarioBaseVolume = UsageCounts{
	Auth: 40_000, Utility: 60_000, Service: 5_000, Marketing: 10_000,
}

var CenarioBasePlan = PlanQuotas{
	Categories: cloneQuotas(CenarioBaseCategories),
	UsdToBrl:   DefaultUsdToBrl,
	QuotaMode:  QuotaHard,
}

func MergePlan(overrides *PlanOverride, quotaMode QuotaMode) PlanQuotas {
	cats := cloneQuotas(CenarioBaseCategories)
	usd := DefaultUsdToBrl
	mode := QuotaHard
	if overrides != nil {
		if overrides.UsdToBrl != nil {
			usd = *overrides.UsdToBrl
		}
		if overrides.QuotaMode != "" {
			mode = overrides.QuotaMode
		}
		for k, patch := range overrides.Categories {
			cur := cats[k]
			if patch.MonthlyQuota != nil {
				cur.MonthlyQuota = *patch.MonthlyQuota
			}
			if patch.UnitCostUSD != nil {
				cur.UnitCostUSD = *patch.UnitCostUSD
			}
			if patch.FreeAllowance != nil {
				cur.FreeAllowance = *patch.FreeAllowance
			}
			cats[k] = cur
		}
	}
	if quotaMode != "" {
		mode = quotaMode
	}
	return PlanQuotas{Categories: cats, UsdToBrl: usd, QuotaMode: mode}
}

func QuotaFor(plan PlanQuotas, cat MessageCategory) CategoryQuota {
	return plan.Categories[cat]
}

func BillingPeriod(at time.Time, timeZone string) string {
	if timeZone == "" {
		timeZone = "America/Sao_Paulo"
	}
	loc, err := time.LoadLocation(timeZone)
	if err != nil {
		loc = time.UTC
	}
	local := at.In(loc)
	return fmt.Sprintf("%04d-%02d", local.Year(), local.Month())
}

func VencimentoForPeriod(period string, day int) (string, error) {
	var year, month int
	if _, err := fmt.Sscanf(period, "%d-%d", &year, &month); err != nil || year == 0 || month == 0 {
		return "", fmt.Errorf("periodo invalido: %s", period)
	}
	last := time.Date(year, time.Month(month)+1, 0, 0, 0, 0, 0, time.UTC).Day()
	chosen := last
	if day > 0 {
		chosen = day
		if chosen > last {
			chosen = last
		}
	}
	return fmt.Sprintf("%s-%02d", period, chosen), nil
}

func cloneQuotas(in map[MessageCategory]CategoryQuota) map[MessageCategory]CategoryQuota {
	out := make(map[MessageCategory]CategoryQuota, len(in))
	for k, v := range in {
		out[k] = v
	}
	return out
}
