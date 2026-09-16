import { QuotaExceededError } from "./errors.js";
import { billingPeriod, emptyUsageCounts, quotaFor } from "./plan.js";
import type {
  MessageCategory,
  MeteringDecision,
  PlanQuotas,
  UsageSnapshot,
  UsageStore,
} from "./types.js";

export function billableUnits(used: number, freeAllowance: number): number {
  return Math.max(0, used - Math.max(0, freeAllowance));
}

/**
 * Quantas unidades do `delta` recém-aplicado são faturáveis, considerando a franquia.
 * Ex.: service usedBefore=950, delta=100, free=1000 → billableDelta=50.
 */
export function billableDeltaForIncrement(
  usedBefore: number,
  delta: number,
  freeAllowance: number,
): { billableDelta: number; freeDelta: number } {
  if (delta <= 0) {
    const billableBefore = billableUnits(usedBefore, freeAllowance);
    const billableAfter = billableUnits(usedBefore + delta, freeAllowance);
    return { billableDelta: billableAfter - billableBefore, freeDelta: 0 };
  }
  const usedAfter = usedBefore + delta;
  const billableBefore = billableUnits(usedBefore, freeAllowance);
  const billableAfter = billableUnits(usedAfter, freeAllowance);
  const billableDelta = billableAfter - billableBefore;
  return { billableDelta, freeDelta: delta - billableDelta };
}

export function evaluateUsage(
  used: number,
  quota: CategoryLike,
  quotaMode: PlanQuotas["quotaMode"],
  period: string,
  category: MessageCategory,
  delta: number,
): MeteringDecision {
  const remaining = Math.max(0, quota.monthlyQuota - used);
  const overageDelta = Math.max(0, used - quota.monthlyQuota);
  const usedBefore = used - delta;
  const { billableDelta, freeDelta } = billableDeltaForIncrement(
    usedBefore,
    delta,
    quota.freeAllowance,
  );
  const overage = used > quota.monthlyQuota;
  const allowed = quotaMode === "soft" || used <= quota.monthlyQuota;
  return {
    category,
    period,
    used,
    quota: quota.monthlyQuota,
    remaining,
    billableDelta,
    freeDelta,
    overage,
    overageDelta,
    allowed,
    quotaMode,
  };
}

interface CategoryLike {
  monthlyQuota: number;
  freeAllowance: number;
}

export class InMemoryUsageStore implements UsageStore {
  private readonly data = new Map<string, UsageSnapshot>();

  constructor(private readonly initial: UsageSnapshot[] = []) {
    for (const snap of initial) {
      this.data.set(keyOf(snap.tenantId, snap.period), cloneSnapshot(snap));
    }
  }

  async get(tenantId: string, period: string): Promise<UsageSnapshot> {
    const found = this.data.get(keyOf(tenantId, period));
    if (found) return cloneSnapshot(found);
    return { tenantId, period, counts: emptyUsageCounts() };
  }

  async increment(
    tenantId: string,
    period: string,
    category: MessageCategory,
    delta: number,
  ): Promise<UsageSnapshot> {
    const current = await this.get(tenantId, period);
    current.counts[category] = Math.max(0, current.counts[category] + delta);
    this.data.set(keyOf(tenantId, period), current);
    return cloneSnapshot(current);
  }
}

function keyOf(tenantId: string, period: string): string {
  return `${tenantId}:${period}`;
}

function cloneSnapshot(snap: UsageSnapshot): UsageSnapshot {
  return {
    tenantId: snap.tenantId,
    period: snap.period,
    counts: { ...snap.counts },
  };
}

export class MeteringService {
  constructor(
    private readonly plan: PlanQuotas,
    private readonly store: UsageStore,
    private readonly clock: () => Date = () => new Date(),
    private readonly timeZone = "America/Sao_Paulo",
  ) {}

  period(at?: Date): string {
    return billingPeriod(at ?? this.clock(), this.timeZone);
  }

  async snapshot(tenantId: string, period?: string): Promise<UsageSnapshot> {
    return this.store.get(tenantId, period ?? this.period());
  }

  async consume(
    tenantId: string,
    category: MessageCategory,
    delta = 1,
  ): Promise<MeteringDecision> {
    if (delta <= 0) {
      throw new Error("delta de consumo deve ser positivo");
    }
    const period = this.period();
    const quota = quotaFor(this.plan, category);
    const snapshot = await this.store.increment(tenantId, period, category, delta);
    const used = snapshot.counts[category];
    const decision = evaluateUsage(used, quota, this.plan.quotaMode, period, category, delta);
    if (!decision.allowed) {
      await this.store.increment(tenantId, period, category, -delta);
      throw new QuotaExceededError(
        `Cota ${category} excedida no periodo ${period} (${used}/${quota.monthlyQuota}, modo hard)`,
        { category, used, quota: quota.monthlyQuota, period },
      );
    }
    return decision;
  }

  async rollback(tenantId: string, category: MessageCategory, delta = 1): Promise<UsageSnapshot> {
    const period = this.period();
    return this.store.increment(tenantId, period, category, -Math.abs(delta));
  }
}
