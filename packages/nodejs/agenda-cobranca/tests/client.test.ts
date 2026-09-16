import { describe, expect, it } from "vitest";
import { Client, createClient } from "../src/client.js";
import {
  AuthenticationError,
  ConfigurationError,
  NotFoundError,
  ValidationError,
} from "../src/errors.js";
import { Signer } from "../src/signer.js";
import type { FetchLike } from "../src/types.js";

const BASE = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo";
const COBRANCA = {
  id: "11111111-1111-4111-8111-111111111111",
  external_reference: "pedido-1",
  valor_centavos: 15_000,
  vencimento: "2026-10-01",
  status: "pendente",
  pagador: {
    documento: "12345678901",
    nome: "Maria Silva",
    email: "maria@example.com",
  },
  juros: { percentual_mes: 1.0 },
  multa: { percentual: 2.0 },
};

interface Captured {
  request: Request;
  body: string;
}

function mockFetch(
  handler: (req: Request, body: string) => Response | Promise<Response>,
): { fetch: FetchLike; last: () => Captured | undefined } {
  let last: Captured | undefined;
  const fetchImpl: FetchLike = (async (input, init) => {
    const request = new Request(input, init);
    const body = await request.clone().text();
    last = { request, body };
    return handler(request, body);
  }) as FetchLike;
  return { fetch: fetchImpl, last: () => last };
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("Client", () => {
  it("rejeita configuracao sem apiKey", () => {
    expect(() => new Client({ apiKey: "" })).toThrow(ConfigurationError);
  });

  it("aceita somente apiKey no modo padrao", () => {
    expect(() => new Client({ apiKey: "chave" })).not.toThrow();
  });

  it("exige clientSecret quando signingEnabled", () => {
    expect(() => new Client({ apiKey: "chave", signingEnabled: true })).toThrow(/clientSecret/);
  });

  it("envia POST /cobrancas com Idempotency-Key e headers de api_key, sem HMAC", async () => {
    const { fetch, last } = mockFetch(() => jsonResponse(201, { data: COBRANCA }));
    const client = new Client({ apiKey: "api_key_exemplo", fetch });

    const cobranca = await client.cobrancas.create({
      externalReference: "pedido-1",
      valorCentavos: 15_000,
      vencimento: "2026-10-01",
      pagador: { documento: "12345678901", nome: "Maria Silva", email: "maria@example.com" },
      juros: { percentualMes: 1.0 },
      multa: { percentual: 2.0 },
      idempotencyKey: "idem-1",
    });

    const captured = last();
    expect(captured).toBeDefined();
    expect(captured!.request.method).toBe("POST");
    expect(new URL(captured!.request.url).pathname).toBe("/api/v2/externo/cobrancas");
    expect(captured!.request.headers.get("Idempotency-Key")).toBe("idem-1");
    expect(captured!.request.headers.get("chave_api")).toBe("api_key_exemplo");
    expect(captured!.request.headers.get("X-Api-Key")).toBe("api_key_exemplo");
    expect(captured!.request.headers.get("X-Client-Id")).toBeNull();
    expect(captured!.request.headers.get("X-Timestamp")).toBeNull();
    expect(captured!.request.headers.get("X-Nonce")).toBeNull();
    expect(captured!.request.headers.get("X-Signature")).toBeNull();
    expect(JSON.parse(captured!.body)).toMatchObject({
      external_reference: "pedido-1",
      valor_centavos: 15_000,
      vencimento: "2026-10-01",
    });
    expect(cobranca.id).toBe(COBRANCA.id);
    expect(cobranca.status).toBe("pendente");
    expect(cobranca.valorCentavos).toBe(15_000);
    expect(cobranca.pagador.nome).toBe("Maria Silva");
  });

  it("envia headers HMAC quando signingEnabled e true", async () => {
    const { fetch, last } = mockFetch(() => jsonResponse(201, { data: COBRANCA }));
    const client = new Client({
      apiKey: "api_key_exemplo",
      clientId: "client_exemplo",
      clientSecret: "test_client_secret",
      signingEnabled: true,
      baseUrl: BASE,
      clock: () => 1_700_000_000,
      nonceGenerator: () => "550e8400-e29b-41d4-a716-446655440000",
      fetch,
    });

    await client.cobrancas.create({
      valorCentavos: 15_000,
      vencimento: "2026-10-01",
      pagador: { documento: "12345678901", nome: "Maria Silva" },
      idempotencyKey: "idem-hmac",
    });

    const captured = last()!;
    expect(captured.request.headers.get("chave_api")).toBe("api_key_exemplo");
    expect(captured.request.headers.get("X-Api-Key")).toBe("api_key_exemplo");
    expect(captured.request.headers.get("X-Client-Id")).toBe("client_exemplo");
    expect(captured.request.headers.get("X-Timestamp")).toBe("1700000000");
    expect(captured.request.headers.get("X-Nonce")).toBe("550e8400-e29b-41d4-a716-446655440000");
    expect(captured.body).not.toContain("test_client_secret");

    const esperado = new Signer("test_client_secret").signatureFor(
      "POST",
      "/api/v2/externo/cobrancas",
      "1700000000",
      "550e8400-e29b-41d4-a716-446655440000",
      captured.body,
    );
    expect(captured.request.headers.get("X-Signature")).toBe(esperado);
  });

  it("gera Idempotency-Key quando o chamador nao informa", async () => {
    const { fetch, last } = mockFetch(() => jsonResponse(201, COBRANCA));
    const client = new Client({ apiKey: "api_key_exemplo", fetch });
    await client.cobrancas.create({
      valorCentavos: 15_000,
      vencimento: "2026-10-01",
      pagador: { documento: "12345678901", nome: "Maria Silva" },
    });
    expect(last()!.request.headers.get("Idempotency-Key")).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
    );
  });

  it("mapeia 422 para ValidationError", async () => {
    const { fetch } = mockFetch(() =>
      jsonResponse(422, { success: false, message: "Dados invalidos", errors: ["valor_centavos"] }),
    );
    const client = new Client({ apiKey: "k", fetch });
    await expect(
      client.cobrancas.create({
        valorCentavos: 0,
        vencimento: "2026-10-01",
        pagador: { documento: "1", nome: "X" },
        idempotencyKey: "idem-err",
      }),
    ).rejects.toMatchObject({ constructor: ValidationError, message: expect.stringMatching(/Dados invalidos/) });
  });

  it("mapeia 403 para AuthenticationError", async () => {
    const { fetch } = mockFetch(() => jsonResponse(403, { message: "plano pausado" }));
    const client = new Client({ apiKey: "k", fetch });
    await expect(client.cobrancas.find("abc")).rejects.toBeInstanceOf(AuthenticationError);
  });

  it("busca GET /cobrancas/:id", async () => {
    const { fetch, last } = mockFetch(() => jsonResponse(200, COBRANCA));
    const client = new Client({ apiKey: "api_key_exemplo", fetch });
    const cobranca = await client.cobrancas.find(COBRANCA.id);
    expect(new URL(last()!.request.url).pathname).toBe(`/api/v2/externo/cobrancas/${COBRANCA.id}`);
    expect(last()!.request.headers.get("chave_api")).toBe("api_key_exemplo");
    expect(cobranca.externalReference).toBe("pedido-1");
  });

  it("mapeia 404 para NotFoundError", async () => {
    const { fetch } = mockFetch(() => jsonResponse(404, { message: "Cobranca nao encontrada" }));
    const client = new Client({ apiKey: "k", fetch });
    await expect(client.cobrancas.find("missing")).rejects.toBeInstanceOf(NotFoundError);
  });

  it("lista GET /cobrancas com filtros", async () => {
    const { fetch, last } = mockFetch(() =>
      jsonResponse(200, { data: [COBRANCA], meta: { page: 1, per_page: 20, total: 1 } }),
    );
    const client = new Client({ apiKey: "api_key_exemplo", fetch });
    const resultado = await client.cobrancas.list({ status: "pendente", page: 1, perPage: 20 });
    const url = new URL(last()!.request.url);
    expect(url.searchParams.get("status")).toBe("pendente");
    expect(url.searchParams.get("page")).toBe("1");
    expect(url.searchParams.get("per_page")).toBe("20");
    expect(resultado.data).toHaveLength(1);
    expect(resultado.meta.total).toBe(1);
  });

  it("envia POST /cobrancas/:id/cancel", async () => {
    const { fetch, last } = mockFetch(() => jsonResponse(200, { ...COBRANCA, status: "cancelada" }));
    const client = new Client({ apiKey: "api_key_exemplo", fetch });
    const cobranca = await client.cobrancas.cancel(COBRANCA.id);
    expect(last()!.request.method).toBe("POST");
    expect(new URL(last()!.request.url).pathname).toBe(`/api/v2/externo/cobrancas/${COBRANCA.id}/cancel`);
    expect(cobranca.status).toBe("cancelada");
  });

  it("chama POST /licenses/verify", async () => {
    const { fetch, last } = mockFetch(() =>
      jsonResponse(200, { success: true, data: { valid: true, message: "ok" } }),
    );
    const client = new Client({ apiKey: "api_key_exemplo", clientId: "client_exemplo", fetch });
    const resultado = await client.licenses.verify();
    expect(JSON.parse(last()!.body)).toMatchObject({ client_id: "client_exemplo" });
    expect(resultado.valid).toBe(true);
    expect(resultado.message).toBe("ok");
  });

  it("createClient verifica licenca quando pedido", async () => {
    let called = 0;
    const { fetch } = mockFetch((req) => {
      if (new URL(req.url).pathname.endsWith("/licenses/verify")) {
        called += 1;
        return jsonResponse(200, { data: { valid: true } });
      }
      return jsonResponse(404, { message: "nope" });
    });
    await createClient({
      apiKey: "k",
      verifyLicenseOnInitialize: true,
      fetch,
    });
    expect(called).toBe(1);
  });
});
