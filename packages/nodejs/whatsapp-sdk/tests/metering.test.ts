import { describe, expect, it } from "vitest";
import { QuotaExceededError } from "../src/errors.js";
import {
  billableDeltaForIncrement,
  billableUnits,
  InMemoryUsageStore,
  MeteringService,
} from "../src/metering.js";
import { CENARIO_BASE_PLAN, mergePlan } from "../src/plan.js";

describe("franquia Service (1.000 sessões/mês)", () => {
  it("nao fatura os primeiros 1000", () => {
    expect(billableUnits(0, 1_000)).toBe(0);
    expect(billableUnits(1_000, 1_000)).toBe(0);
    expect(billableUnits(1_001, 1_000)).toBe(1);
    expect(billableUnits(5_000, 1_000)).toBe(4_000);
  });

  it("reparte delta que atravessa a franquia", () => {
    expect(billableDeltaForIncrement(950, 100, 1_000)).toEqual({
      billableDelta: 50,
      freeDelta: 50,
    });
    expect(billableDeltaForIncrement(0, 500, 1_000)).toEqual({
      billableDelta: 0,
      freeDelta: 500,
    });
    expect(billableDeltaForIncrement(1_000, 10, 1_000)).toEqual({
      billableDelta: 10,
      freeDelta: 0,
    });
  });
});

describe("MeteringService", () => {
  const clock = () => new Date("2026-09-16T12:00:00Z");

  it("acumula por categoria e periodo YYYY-MM", async () => {
    const metering = new MeteringService(CENARIO_BASE_PLAN, new InMemoryUsageStore(), clock);
    const d1 = await metering.consume("tenant-a", "auth", 3);
    const d2 = await metering.consume("tenant-a", "utility", 1);
    expect(d1.period).toBe("2026-09");
    expect(d1.used).toBe(3);
    expect(d1.billableDelta).toBe(3);
    expect(d2.category).toBe("utility");
    const snap = await metering.snapshot("tenant-a");
    expect(snap.counts).toEqual({ auth: 3, utility: 1, service: 0, marketing: 0 });
  });

  it("modo hard bloqueia e nao persiste o consumo excedente", async () => {
    const plan = mergePlan({
      quotaMode: "hard",
      categories: { auth: { monthlyQuota: 2, unitCostUsd: 0.0315, freeAllowance: 0 } },
    });
    const metering = new MeteringService(plan, new InMemoryUsageStore(), clock);
    await metering.consume("t", "auth", 2);
    await expect(metering.consume("t", "auth", 1)).rejects.toBeInstanceOf(QuotaExceededError);
    const snap = await metering.snapshot("t");
    expect(snap.counts.auth).toBe(2);
  });

  it("modo soft permite envio e marca overage", async () => {
    const plan = mergePlan({
      quotaMode: "soft",
      categories: { marketing: { monthlyQuota: 2, unitCostUsd: 0.0625, freeAllowance: 0 } },
    });
    const metering = new MeteringService(plan, new InMemoryUsageStore(), clock);
    await metering.consume("t", "marketing", 2);
    const over = await metering.consume("t", "marketing", 1);
    expect(over.allowed).toBe(true);
    expect(over.overage).toBe(true);
    expect(over.overageDelta).toBe(1);
    expect(over.used).toBe(3);
    expect(over.remaining).toBe(0);
  });

  it("service so comeca a faturar apos 1000", async () => {
    const metering = new MeteringService(CENARIO_BASE_PLAN, new InMemoryUsageStore(), clock);
    const first = await metering.consume("t", "service", 1_000);
    expect(first.billableDelta).toBe(0);
    expect(first.freeDelta).toBe(1_000);
    expect(first.overage).toBe(false);
    const next = await metering.consume("t", "service", 5);
    expect(next.billableDelta).toBe(5);
    expect(next.freeDelta).toBe(0);
    expect(next.used).toBe(1_005);
  });
});
