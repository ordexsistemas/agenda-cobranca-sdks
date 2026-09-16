import { describe, expect, it } from "vitest";
import { CobrancaQuantityController } from "../src/agenda.js";
import { WhatsAppClient } from "../src/client.js";
import { computeCobrancaQuantity } from "../src/cobranca-quantity.js";
import { StaticEntitlementChecker } from "../src/entitlement.js";
import { EntitlementError, QuotaExceededError } from "../src/errors.js";
import { InMemoryUsageStore } from "../src/metering.js";
import { CENARIO_BASE_PLAN } from "../src/plan.js";
import type {
  AgendaCobrancaLike,
  CobrancaRecord,
  CreateCobrancaInput,
  FetchLike,
  ListCobrancasInput,
} from "../src/types.js";

interface Captured {
  request: Request;
  body: string;
}

function mockFetch(
  handler: (req: Request, body: string) => Response | Promise<Response>,
): { fetch: FetchLike; calls: () => Captured[] } {
  const calls: Captured[] = [];
  const fetchImpl: FetchLike = (async (input, init) => {
    const request = new Request(input, init);
    const body = await request.clone().text();
    calls.push({ request, body });
    return handler(request, body);
  }) as FetchLike;
  return { fetch: fetchImpl, calls: () => calls };
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

const META_OK = {
  messaging_product: "whatsapp",
  contacts: [{ input: "5511999999999", wa_id: "5511999999999" }],
  messages: [{ id: "wamid.TEST" }],
};

const PAGADOR = { documento: "12345678901", nome: "Empresa SaaS", email: "fin@example.com" };

class MemoryAgenda implements AgendaCobrancaLike {
  readonly items = new Map<string, CobrancaRecord>();
  readonly cobrancas = {
    create: async (input: CreateCobrancaInput): Promise<CobrancaRecord> => {
      const rec: CobrancaRecord = {
        id: `cob-${input.externalReference}`,
        externalReference: input.externalReference,
        valorCentavos: input.valorCentavos,
        vencimento: input.vencimento,
        status: "pendente",
        pagador: input.pagador,
      };
      this.items.set(rec.id, rec);
      return rec;
    },
    list: async (filtros: ListCobrancasInput = {}) => {
      const data = [...this.items.values()].filter((c) => {
        if (filtros.externalReference && c.externalReference !== filtros.externalReference) return false;
        return true;
      });
      return { data };
    },
    cancel: async (id: string) => {
      const rec = this.items.get(id);
      if (!rec) throw new Error("missing");
      rec.status = "cancelada";
      return rec;
    },
  };
}

describe("WhatsAppClient", () => {
  it("rejeita configuracao incompleta", () => {
    expect(() => new WhatsAppClient({ accessToken: "", phoneNumberId: "", tenantId: "" })).toThrow(
      /accessToken/,
    );
  });

  it("bloqueia envio sem add-on habilitado", async () => {
    const { fetch } = mockFetch(() => jsonResponse(200, META_OK));
    const client = new WhatsAppClient({
      accessToken: "token",
      phoneNumberId: "123",
      tenantId: "acme",
      entitlement: new StaticEntitlementChecker(false, { message: "addon off" }),
      fetch,
    });
    await expect(
      client.sendText({ to: "5511999999999", body: "oi" }),
    ).rejects.toBeInstanceOf(EntitlementError);
  });

  it("envia template utility, marca categoria e mede uso", async () => {
    const { fetch, calls } = mockFetch(() => jsonResponse(200, META_OK));
    const client = new WhatsAppClient({
      accessToken: "token",
      phoneNumberId: "555",
      tenantId: "acme",
      entitlement: new StaticEntitlementChecker(true),
      clock: () => new Date("2026-09-16T15:00:00Z"),
      fetch,
    });

    const result = await client.sendTemplate({
      to: "5511999999999",
      category: "utility",
      template: { name: "pix_recebido", language: "pt_BR" },
    });

    expect(result.category).toBe("utility");
    expect(result.meta.messages?.[0]?.id).toBe("wamid.TEST");
    expect(result.metering.used).toBe(1);
    expect(result.metering.billableDelta).toBe(1);
    expect(result.cobrancaPlan?.totalQuantity).toBe(1);

    const captured = calls()[0];
    expect(captured.request.method).toBe("POST");
    expect(new URL(captured.request.url).pathname).toBe("/v21.0/555/messages");
    expect(captured.request.headers.get("Authorization")).toBe("Bearer token");
    const body = JSON.parse(captured.body);
    expect(body.type).toBe("template");
    expect(body.template.name).toBe("pix_recebido");
    expect(JSON.parse(body.biz_opaque_callback_data)).toEqual({ category: "utility" });
    expect(captured.body).not.toContain("token");
  });

  it("faz rollback da cota se a Graph API falhar", async () => {
    const { fetch } = mockFetch(() =>
      jsonResponse(400, { error: { message: "invalid to", type: "OAuthException", code: 100 } }),
    );
    const store = new InMemoryUsageStore();
    const client = new WhatsAppClient({
      accessToken: "token",
      phoneNumberId: "555",
      tenantId: "acme",
      entitlement: new StaticEntitlementChecker(true),
      usageStore: store,
      fetch,
    });
    await expect(client.sendText({ to: "1", body: "oi" })).rejects.toMatchObject({ status: 400 });
    const snap = await store.get("acme", client.metering.period());
    expect(snap.counts.service).toBe(0);
  });

  it("modo hard impede o POST a Meta", async () => {
    let graphCalls = 0;
    const { fetch } = mockFetch(() => {
      graphCalls += 1;
      return jsonResponse(200, META_OK);
    });
    const client = new WhatsAppClient({
      accessToken: "token",
      phoneNumberId: "555",
      tenantId: "acme",
      entitlement: new StaticEntitlementChecker(true),
      quotaMode: "hard",
      plan: { categories: { service: { monthlyQuota: 1 } } },
      fetch,
    });
    await client.sendText({ to: "5511", body: "1" });
    await expect(client.sendText({ to: "5511", body: "2" })).rejects.toBeInstanceOf(QuotaExceededError);
    expect(graphCalls).toBe(1);
  });
});

describe("CobrancaQuantityController", () => {
  it("cria cobrancas do plano e cancela extras quando o uso cai", async () => {
    const agenda = new MemoryAgenda();
    const controller = new CobrancaQuantityController(agenda, PAGADOR, 10);

    const full = computeCobrancaQuantity(
      { auth: 20_000, utility: 0, service: 0, marketing: 0 },
      CENARIO_BASE_PLAN,
      { period: "2026-09", tenantId: "acme", strategy: "by-sessions", sessionsPerCobranca: 10_000 },
    );
    expect(full.totalQuantity).toBe(2);

    const first = await controller.sync(full);
    expect(first.created).toBe(2);
    expect(first.cancelled).toBe(0);
    expect([...agenda.items.values()].filter((c) => c.status === "pendente")).toHaveLength(2);
    expect(first.items[0]?.cobranca?.vencimento).toBe("2026-09-10");

    const reduced = computeCobrancaQuantity(
      { auth: 10_000, utility: 0, service: 0, marketing: 0 },
      CENARIO_BASE_PLAN,
      { period: "2026-09", tenantId: "acme", strategy: "by-sessions", sessionsPerCobranca: 10_000 },
    );
    const second = await controller.sync(reduced);
    expect(second.kept).toBe(1);
    expect(second.cancelled).toBe(1);
    expect(second.created).toBe(0);
    const pending = [...agenda.items.values()].filter((c) => c.status === "pendente");
    expect(pending).toHaveLength(1);
    expect(pending[0]?.externalReference).toBe("wa:acme:2026-09:auth:1");
  });
});
