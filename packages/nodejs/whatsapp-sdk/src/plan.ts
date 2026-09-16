import type {
  CategoryQuota,
  CategoryQuotaMap,
  DeepPartialPlan,
  MessageCategory,
  PlanQuotas,
  UsageCounts,
} from "./types.js";

/**
 * Câmbio implícito na tabela do Cenário Base (ex.: US$ 1.260 → R$ 6.930).
 * Configurável por tenant/plano — não é taxa fixa de produto.
 */
export const DEFAULT_USD_TO_BRL = 5.5;

/**
 * Quotas e custos unitários do Cenário Base (~100k transações/mês).
 *
 * | Categoria     | Volume | Unitário USD | Total USD | Total BRL |
 * |---------------|--------|--------------|-----------|-----------|
 * | auth          | 40.000 | 0.0315       | 1.260     | 6.930     |
 * | utility       | 60.000 | 0.0350       | 2.100     | 11.550    |
 * | service       | 5.000  | 0.0300       | 120 (após 1k free) | 660 |
 * | marketing     | 10.000 | 0.0625       | 625       | (câmbio do plano) |
 */
export const CENARIO_BASE_CATEGORIES: CategoryQuotaMap = {
  auth: { monthlyQuota: 40_000, unitCostUsd: 0.0315, freeAllowance: 0 },
  utility: { monthlyQuota: 60_000, unitCostUsd: 0.035, freeAllowance: 0 },
  service: { monthlyQuota: 5_000, unitCostUsd: 0.03, freeAllowance: 1_000 },
  marketing: { monthlyQuota: 10_000, unitCostUsd: 0.0625, freeAllowance: 0 },
};

export const CENARIO_BASE_VOLUME: UsageCounts = {
  auth: 40_000,
  utility: 60_000,
  service: 5_000,
  marketing: 10_000,
};

export const CENARIO_BASE_PLAN: PlanQuotas = {
  categories: CENARIO_BASE_CATEGORIES,
  usdToBrl: DEFAULT_USD_TO_BRL,
  quotaMode: "hard",
};

export const SERVICE_FREE_ALLOWANCE = 1_000;

export function emptyUsageCounts(): UsageCounts {
  return { auth: 0, utility: 0, service: 0, marketing: 0 };
}

export function mergePlan(overrides?: DeepPartialPlan, quotaMode?: PlanQuotas["quotaMode"]): PlanQuotas {
  const categories = { ...CENARIO_BASE_CATEGORIES };
  if (overrides?.categories) {
    for (const key of Object.keys(overrides.categories) as MessageCategory[]) {
      const patch = overrides.categories[key];
      if (!patch) continue;
      categories[key] = { ...categories[key], ...patch };
    }
  }
  return {
    categories,
    usdToBrl: overrides?.usdToBrl ?? DEFAULT_USD_TO_BRL,
    quotaMode: quotaMode ?? overrides?.quotaMode ?? "hard",
  };
}

export function quotaFor(plan: PlanQuotas, category: MessageCategory): CategoryQuota {
  return plan.categories[category];
}

export function billingPeriod(date: Date = new Date(), timeZone = "America/Sao_Paulo"): string {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone,
    year: "numeric",
    month: "2-digit",
  }).formatToParts(date);
  const year = parts.find((p) => p.type === "year")?.value;
  const month = parts.find((p) => p.type === "month")?.value;
  if (!year || !month) {
    return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, "0")}`;
  }
  return `${year}-${month}`;
}

export function lastDayOfPeriod(period: string): string {
  const [year, month] = period.split("-").map((v) => Number(v));
  if (!year || !month) {
    throw new Error(`periodo invalido: ${period}`);
  }
  const last = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return `${period}-${String(last).padStart(2, "0")}`;
}

export function vencimentoForPeriod(period: string, day?: number): string {
  const [year, month] = period.split("-").map((v) => Number(v));
  if (!year || !month) {
    throw new Error(`periodo invalido: ${period}`);
  }
  const last = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const chosen = day == null || day <= 0 ? last : Math.min(day, last);
  return `${period}-${String(chosen).padStart(2, "0")}`;
}
