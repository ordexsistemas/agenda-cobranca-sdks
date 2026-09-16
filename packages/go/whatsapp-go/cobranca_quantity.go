package whatsapp

import (
	"math"
	"regexp"
	"strconv"
)

func SplitCentavos(total int64, quantity int) []int64 {
	if quantity <= 0 {
		return []int64{}
	}
	out := make([]int64, quantity)
	if total <= 0 {
		return out
	}
	base := total / int64(quantity)
	remainder := total - base*int64(quantity)
	for i := 0; i < quantity; i++ {
		out[i] = base
		if int64(i) < remainder {
			out[i]++
		}
	}
	return out
}

func CostBrlCentavos(billable int64, unitCostUSD, usdToBrl float64) int64 {
	return int64(math.Round(float64(billable) * unitCostUSD * usdToBrl * 100))
}

func CostUSD(billable int64, unitCostUSD float64) float64 {
	v := float64(billable) * unitCostUSD
	return math.Round(v*10000) / 10000
}

func ComputeCobrancaQuantity(counts UsageCounts, plan PlanQuotas, opts ConversionOptions) CobrancaQuantityPlan {
	strategy := opts.Strategy
	if strategy == "" {
		strategy = StrategyPerCategory
	}
	categories := make([]CategoryCobrancaPlan, 0, len(MessageCategories))
	var totalQty int
	var totalUSD float64
	var totalBrl int64
	for _, category := range MessageCategories {
		quota := QuotaFor(plan, category)
		used := counts.Get(category)
		if used < 0 {
			used = 0
		}
		billable := BillableUnits(used, quota.FreeAllowance)
		free := used
		if quota.FreeAllowance < free {
			free = quota.FreeAllowance
		}
		if free < 0 {
			free = 0
		}
		overage := used - quota.MonthlyQuota
		if overage < 0 {
			overage = 0
		}
		usd := CostUSD(billable, quota.UnitCostUSD)
		brl := CostBrlCentavos(billable, quota.UnitCostUSD, plan.UsdToBrl)
		qty := quantityFor(strategy, category, billable, brl, opts)
		cat := CategoryCobrancaPlan{
			Category:          category,
			Used:              used,
			Billable:          billable,
			Free:              free,
			Quota:             quota.MonthlyQuota,
			Overage:           overage,
			UnitCostUSD:       quota.UnitCostUSD,
			CostUSD:           usd,
			CostBrlCentavos:   brl,
			Quantity:          qty,
			ValorCentavosEach: SplitCentavos(brl, qty),
		}
		categories = append(categories, cat)
		totalQty += qty
		totalUSD += usd
		totalBrl += brl
	}
	return CobrancaQuantityPlan{
		Period:               opts.Period,
		TenantID:             opts.TenantID,
		Categories:           categories,
		TotalQuantity:        totalQty,
		TotalCostUSD:         math.Round(totalUSD*10000) / 10000,
		TotalCostBrlCentavos: totalBrl,
		Strategy:             strategy,
	}
}

func CobrancaExternalReference(tenantID, period string, category MessageCategory, index int) string {
	return "wa:" + tenantID + ":" + period + ":" + string(category) + ":" + strconv.Itoa(index)
}

var refRE = regexp.MustCompile(`^wa:([^:]+):(\d{4}-\d{2}):(auth|utility|service|marketing):(\d+)$`)

func ParseCobrancaExternalReference(ref string) (tenantID, period string, category MessageCategory, index int, ok bool) {
	m := refRE.FindStringSubmatch(ref)
	if m == nil {
		return "", "", "", 0, false
	}
	idx, _ := strconv.Atoi(m[4])
	return m[1], m[2], MessageCategory(m[3]), idx, true
}

func quantityFor(strategy QuantityStrategy, category MessageCategory, billable, costCents int64, opts ConversionOptions) int {
	if billable <= 0 && costCents <= 0 {
		if opts.IncludeZeroCategories {
			return 1
		}
		return 0
	}
	switch strategy {
	case StrategyBySessions:
		per := sessionsPer(category, opts)
		return max(1, int(math.Ceil(float64(billable)/float64(per))))
	case StrategyByValue:
		per := opts.ValorCentavosPorCobranca
		if per <= 0 {
			per = DefaultValorCentavos
		}
		if per <= 0 {
			return 1
		}
		return max(1, int(math.Ceil(float64(costCents)/float64(per))))
	default:
		return 1
	}
}

func sessionsPer(category MessageCategory, opts ConversionOptions) int64 {
	if opts.SessionsPerCobrancaByCat != nil {
		if n, ok := opts.SessionsPerCobrancaByCat[category]; ok && n > 0 {
			return n
		}
	}
	if opts.SessionsPerCobranca > 0 {
		return opts.SessionsPerCobranca
	}
	return DefaultSessionsPer
}
