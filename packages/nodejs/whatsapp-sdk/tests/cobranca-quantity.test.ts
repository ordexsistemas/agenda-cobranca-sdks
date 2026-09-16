import { describe, expect, it } from "vitest";
import {
  cobrancaExternalReference,
  computeCobrancaQuantity,
  parseCobrancaExternalReference,
  splitCentavos,
} from "../src/cobranca-quantity.js";
import { CENARIO_BASE_PLAN, CENARIO_BASE_VOLUME, DEFAULT_USD_TO_BRL } from "../src/plan.js";

describe("computeCobrancaQuantity — Cenário Base", () => {
  const opts = { period: "2026-09", tenantId: "acme" };

  it("per-category: 1 cobranca por categoria faturavel e custos da tabela", () => {
    const plan = computeCobrancaQuantity(CENARIO_BASE_VOLUME, CENARIO_BASE_PLAN, {
      ...opts,
      strategy: "per-category",
    });

    expect(plan.totalQuantity).toBe(4);
    expect(plan.totalCostUsd).toBe(4105);
    expect(plan.totalCostBrlCentavos).toBe(Math.round(4105 * DEFAULT_USD_TO_BRL * 100));

    const byCat = Object.fromEntries(plan.categories.map((c) => [c.category, c]));
    expect(byCat.auth.billable).toBe(40_000);
    expect(byCat.auth.costUsd).toBe(1_260);
    expect(byCat.auth.costBrlCentavos).toBe(693_000);

    expect(byCat.utility.billable).toBe(60_000);
    expect(byCat.utility.costUsd).toBe(2_100);
    expect(byCat.utility.costBrlCentavos).toBe(1_155_000);

    expect(byCat.service.used).toBe(5_000);
    expect(byCat.service.free).toBe(1_000);
    expect(byCat.service.billable).toBe(4_000);
    expect(byCat.service.costUsd).toBe(120);
    expect(byCat.service.costBrlCentavos).toBe(66_000);

    expect(byCat.marketing.billable).toBe(10_000);
    expect(byCat.marketing.costUsd).toBe(625);
    expect(byCat.marketing.costBrlCentavos).toBe(343_750);
    expect(byCat.marketing.quantity).toBe(1);
    expect(byCat.marketing.valorCentavosEach).toEqual([343_750]);
  });

  it("service so com franquia nao gera cobranca", () => {
    const plan = computeCobrancaQuantity(
      { auth: 0, utility: 0, service: 1_000, marketing: 0 },
      CENARIO_BASE_PLAN,
      { ...opts, strategy: "per-category" },
    );
    expect(plan.totalQuantity).toBe(0);
    expect(plan.totalCostUsd).toBe(0);
    expect(plan.categories.find((c) => c.category === "service")?.billable).toBe(0);
  });

  it("by-sessions converte delta de uso em quantidade (ceil)", () => {
    const plan = computeCobrancaQuantity(CENARIO_BASE_VOLUME, CENARIO_BASE_PLAN, {
      ...opts,
      strategy: "by-sessions",
      sessionsPerCobranca: 10_000,
    });
    const byCat = Object.fromEntries(plan.categories.map((c) => [c.category, c.quantity]));
    expect(byCat.auth).toBe(4);
    expect(byCat.utility).toBe(6);
    expect(byCat.service).toBe(1); // 4000 billable / 10000 → 1
    expect(byCat.marketing).toBe(1);
    expect(plan.totalQuantity).toBe(12);
  });

  it("by-value parte o custo BRL em N cobrancas", () => {
    const plan = computeCobrancaQuantity(CENARIO_BASE_VOLUME, CENARIO_BASE_PLAN, {
      ...opts,
      strategy: "by-value",
      valorCentavosPorCobranca: 100_000,
    });
    const byCat = Object.fromEntries(plan.categories.map((c) => [c.category, c]));
    expect(byCat.auth.quantity).toBe(7); // 693000 / 100000
    expect(byCat.utility.quantity).toBe(12);
    expect(byCat.service.quantity).toBe(1);
    expect(byCat.marketing.quantity).toBe(4);
    expect(byCat.auth.valorCentavosEach.reduce((a, b) => a + b, 0)).toBe(693_000);
  });

  it("uso parcial reduz quantidade em by-sessions", () => {
    const plan = computeCobrancaQuantity(
      { auth: 10_000, utility: 0, service: 0, marketing: 0 },
      CENARIO_BASE_PLAN,
      { ...opts, strategy: "by-sessions", sessionsPerCobranca: 10_000 },
    );
    expect(plan.totalQuantity).toBe(1);
    expect(plan.categories.find((c) => c.category === "auth")?.costUsd).toBe(315);
  });
});

describe("splitCentavos / external_reference", () => {
  it("distribui resto nas primeiras parcelas", () => {
    expect(splitCentavos(100, 3)).toEqual([34, 33, 33]);
    expect(splitCentavos(0, 2)).toEqual([0, 0]);
    expect(splitCentavos(50, 0)).toEqual([]);
  });

  it("round-trip da referencia deterministica", () => {
    const ref = cobrancaExternalReference("acme", "2026-09", "utility", 2);
    expect(ref).toBe("wa:acme:2026-09:utility:2");
    expect(parseCobrancaExternalReference(ref)).toEqual({
      tenantId: "acme",
      period: "2026-09",
      category: "utility",
      index: 2,
    });
    expect(parseCobrancaExternalReference("pedido-1001")).toBeNull();
  });
});
