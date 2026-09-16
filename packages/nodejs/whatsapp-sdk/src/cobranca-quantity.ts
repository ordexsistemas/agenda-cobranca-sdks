import { billableUnits } from "./metering.js";
import { quotaFor } from "./plan.js";
import type {
  CategoryCobrancaPlan,
  CobrancaQuantityPlan,
  ConversionOptions,
  MessageCategory,
  PlanQuotas,
  QuantityStrategy,
  UsageCounts,
} from "./types.js";
import { MESSAGE_CATEGORIES } from "./types.js";

export const DEFAULT_SESSIONS_PER_COBRANCA = 10_000;
export const DEFAULT_VALOR_CENTAVOS_POR_COBRANCA = 100_000;

export function splitCentavos(total: number, quantity: number): number[] {
  if (quantity <= 0) return [];
  if (total <= 0) return Array.from({ length: quantity }, () => 0);
  const base = Math.floor(total / quantity);
  const remainder = total - base * quantity;
  return Array.from({ length: quantity }, (_, i) => base + (i < remainder ? 1 : 0));
}

export function costBrlCentavos(billable: number, unitCostUsd: number, usdToBrl: number): number {
  return Math.round(billable * unitCostUsd * usdToBrl * 100);
}

export function costUsd(billable: number, unitCostUsd: number): number {
  return Number((billable * unitCostUsd).toFixed(4));
}

function sessionsPer(category: MessageCategory, options: ConversionOptions): number {
  const raw = options.sessionsPerCobranca;
  if (typeof raw === "number" && raw > 0) return raw;
  if (raw && typeof raw === "object") {
    const n = raw[category];
    if (n && n > 0) return n;
  }
  return DEFAULT_SESSIONS_PER_COBRANCA;
}

function quantityFor(
  strategy: QuantityStrategy,
  category: MessageCategory,
  billable: number,
  costCents: number,
  options: ConversionOptions,
): number {
  if (billable <= 0 && costCents <= 0) {
    return options.includeZeroCategories ? 1 : 0;
  }
  switch (strategy) {
    case "by-sessions": {
      const per = sessionsPer(category, options);
      return Math.max(1, Math.ceil(billable / per));
    }
    case "by-value": {
      const per = options.valorCentavosPorCobranca ?? DEFAULT_VALOR_CENTAVOS_POR_COBRANCA;
      if (per <= 0) return 1;
      return Math.max(1, Math.ceil(costCents / per));
    }
    case "per-category":
    default:
      return 1;
  }
}

/**
 * Converte deltas/saldos de uso WhatsApp por categoria na quantidade de cobranças
 * a criar/ajustar na Agenda de Cobranças no período de faturamento.
 *
 * Regras:
 * - Service: os primeiros `freeAllowance` (1.000 no Cenário Base) não geram valor.
 * - `per-category`: 1 cobrança por categoria com uso faturável (fatura mensal).
 * - `by-sessions`: ceil(billable / sessionsPerCobranca).
 * - `by-value`: ceil(custo BRL centavos / valorCentavosPorCobranca).
 */
export function computeCobrancaQuantity(
  counts: UsageCounts,
  plan: PlanQuotas,
  options: ConversionOptions & { period: string; tenantId: string },
): CobrancaQuantityPlan {
  const strategy: QuantityStrategy = options.strategy ?? "per-category";
  const categories: CategoryCobrancaPlan[] = [];

  for (const category of MESSAGE_CATEGORIES) {
    const quota = quotaFor(plan, category);
    const used = Math.max(0, counts[category] ?? 0);
    const billable = billableUnits(used, quota.freeAllowance);
    const free = Math.min(used, Math.max(0, quota.freeAllowance));
    const overage = Math.max(0, used - quota.monthlyQuota);
    const usd = costUsd(billable, quota.unitCostUsd);
    const brl = costBrlCentavos(billable, quota.unitCostUsd, plan.usdToBrl);
    const quantity = quantityFor(strategy, category, billable, brl, options);
    categories.push({
      category,
      used,
      billable,
      free,
      quota: quota.monthlyQuota,
      overage,
      unitCostUsd: quota.unitCostUsd,
      costUsd: usd,
      costBrlCentavos: brl,
      quantity,
      valorCentavosEach: splitCentavos(brl, quantity),
    });
  }

  return {
    period: options.period,
    tenantId: options.tenantId,
    categories,
    totalQuantity: categories.reduce((sum, c) => sum + c.quantity, 0),
    totalCostUsd: Number(categories.reduce((sum, c) => sum + c.costUsd, 0).toFixed(4)),
    totalCostBrlCentavos: categories.reduce((sum, c) => sum + c.costBrlCentavos, 0),
    strategy,
  };
}

export function cobrancaExternalReference(
  tenantId: string,
  period: string,
  category: MessageCategory,
  index: number,
): string {
  return `wa:${tenantId}:${period}:${category}:${index}`;
}

export function parseCobrancaExternalReference(
  ref: string,
): { tenantId: string; period: string; category: MessageCategory; index: number } | null {
  const match = /^wa:([^:]+):(\d{4}-\d{2}):(auth|utility|service|marketing):(\d+)$/.exec(ref);
  if (!match) return null;
  return {
    tenantId: match[1],
    period: match[2],
    category: match[3] as MessageCategory,
    index: Number(match[4]),
  };
}
