import { randomUUID } from "node:crypto";
import { cobrancaExternalReference } from "./cobranca-quantity.js";
import { errorFromStatus, ValidationError } from "./errors.js";
import { asRecord, compact, decodeBody, unwrapData } from "./models.js";
import { vencimentoForPeriod } from "./plan.js";
import type {
  AgendaCobrancaLike,
  ClientOptions,
  CobrancaRecord,
  CobrancaSyncItem,
  CobrancaSyncResult,
  CobrancaQuantityPlan,
  CreateCobrancaInput,
  FetchLike,
  ListCobrancasInput,
  ListCobrancasResult,
  Pagador,
} from "./types.js";
import { DEFAULT_ORDEX_BASE_URL } from "./types.js";

export class ThinAgendaCobrancaClient implements AgendaCobrancaLike {
  readonly cobrancas: AgendaCobrancaLike["cobrancas"];
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;
  private readonly timeoutMs: number;

  constructor(
    private readonly apiKey: string,
    options: { baseUrl?: string; fetch?: FetchLike; timeoutMs?: number } = {},
  ) {
    this.baseUrl = (options.baseUrl?.trim() || DEFAULT_ORDEX_BASE_URL).replace(/\/+$/, "") + "/";
    this.fetchImpl = options.fetch ?? fetch;
    this.timeoutMs = options.timeoutMs ?? 30_000;
    this.cobrancas = {
      create: (input) => this.create(input),
      list: (filtros) => this.list(filtros),
      cancel: (id) => this.cancel(id),
    };
  }

  private async create(input: CreateCobrancaInput): Promise<CobrancaRecord> {
    const idempotencyKey = input.idempotencyKey?.trim() || randomUUID();
    const body = compact({
      external_reference: input.externalReference,
      valor_centavos: input.valorCentavos,
      vencimento: input.vencimento,
      pagador: compact({
        documento: input.pagador?.documento,
        nome: input.pagador?.nome,
        email: input.pagador?.email,
      }),
    });
    const payload = await this.request("POST", "cobrancas", {
      body,
      headers: { "Idempotency-Key": idempotencyKey },
    });
    return cobrancaFromResponse(payload);
  }

  private async list(filtros: ListCobrancasInput = {}): Promise<ListCobrancasResult> {
    const query: Record<string, string> = {};
    if (filtros.status) query.status = filtros.status;
    if (filtros.externalReference) query.external_reference = filtros.externalReference;
    if (filtros.page != null) query.page = String(filtros.page);
    if (filtros.perPage != null) query.per_page = String(filtros.perPage);
    const payload = await this.request("GET", "cobrancas", { query });
    return listFromResponse(payload);
  }

  private async cancel(id: string): Promise<CobrancaRecord> {
    if (!id || id.trim() === "") {
      throw new ValidationError("id e obrigatorio", 400);
    }
    const payload = await this.request("POST", `cobrancas/${id}/cancel`);
    return cobrancaFromResponse(payload);
  }

  private async request(
    method: string,
    path: string,
    opts: { body?: unknown; query?: Record<string, string>; headers?: Record<string, string> } = {},
  ): Promise<unknown> {
    const url = new URL(path.replace(/^\/+/, ""), this.baseUrl);
    if (opts.query) {
      for (const [key, value] of Object.entries(opts.query)) {
        if (value !== "") url.searchParams.set(key, value);
      }
    }
    const headers = new Headers(opts.headers);
    headers.set("Accept", "application/json");
    headers.set("chave_api", this.apiKey);
    headers.set("X-Api-Key", this.apiKey);
    let bodyText: string | undefined;
    if (opts.body !== undefined && opts.body !== null) {
      bodyText = JSON.stringify(opts.body);
      headers.set("Content-Type", "application/json");
    }
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    let response: Response;
    try {
      response = await this.fetchImpl(url, {
        method: method.toUpperCase(),
        headers,
        body: bodyText,
        signal: controller.signal,
      });
    } finally {
      clearTimeout(timer);
    }
    const payload = decodeBody(await response.text());
    if (response.ok) return payload;
    const rec = asRecord(payload);
    const message =
      (typeof rec.message === "string" && rec.message) ||
      (typeof rec.error === "string" && rec.error) ||
      `Erro HTTP ${response.status}`;
    throw errorFromStatus(response.status, message, payload, rec.errors ?? rec.error ?? null);
  }
}

export function cobrancaFromResponse(payload: unknown): CobrancaRecord {
  const dados = unwrapData(payload);
  const pagadorRec = asRecord(dados.pagador);
  return {
    id: String(dados.id ?? ""),
    externalReference:
      dados.external_reference == null ? undefined : String(dados.external_reference),
    valorCentavos: Number(dados.valor_centavos ?? 0),
    vencimento: String(dados.vencimento ?? ""),
    status: String(dados.status ?? ""),
    pagador:
      Object.keys(pagadorRec).length === 0
        ? undefined
        : {
            documento: String(pagadorRec.documento ?? ""),
            nome: String(pagadorRec.nome ?? ""),
            email: pagadorRec.email == null ? undefined : String(pagadorRec.email),
          },
    raw: dados,
  };
}

export function listFromResponse(payload: unknown): ListCobrancasResult {
  if (Array.isArray(payload)) {
    return { data: payload.map(cobrancaFromResponse) };
  }
  const rec = asRecord(payload);
  const items = Array.isArray(rec.data)
    ? rec.data
    : Array.isArray(rec.cobrancas)
      ? rec.cobrancas
      : [];
  const metaRaw = asRecord(rec.meta);
  const meta: ListCobrancasResult["meta"] = {};
  if (metaRaw.page != null || rec.page != null) meta.page = Number(metaRaw.page ?? rec.page);
  if (metaRaw.per_page != null || rec.per_page != null) {
    meta.perPage = Number(metaRaw.per_page ?? rec.per_page);
  }
  if (metaRaw.total != null || rec.total != null) meta.total = Number(metaRaw.total ?? rec.total);
  return { data: items.map(cobrancaFromResponse), meta };
}

export function resolveAgendaClient(options: ClientOptions): AgendaCobrancaLike | undefined {
  if (options.agendaClient) return options.agendaClient;
  if (options.ordexApiKey && options.ordexApiKey.trim() !== "") {
    return new ThinAgendaCobrancaClient(options.ordexApiKey, {
      baseUrl: options.ordexBaseUrl,
      fetch: options.fetch,
      timeoutMs: options.timeoutMs,
    });
  }
  return undefined;
}

/**
 * Cria, mantém ou cancela cobranças do período para coincidir com `plan.totalQuantity`
 * (e a quantidade por categoria). Usa `external_reference` determinístico
 * `wa:{tenant}:{period}:{category}:{index}`.
 */
export class CobrancaQuantityController {
  constructor(
    private readonly agenda: AgendaCobrancaLike,
    private readonly pagador: Pagador,
    private readonly vencimentoDay?: number,
  ) {}

  async sync(plan: CobrancaQuantityPlan): Promise<CobrancaSyncResult> {
    const items: CobrancaSyncItem[] = [];
    for (const categoryPlan of plan.categories) {
      const vencimento = vencimentoForPeriod(plan.period, this.vencimentoDay);
      for (let index = 1; index <= categoryPlan.quantity; index += 1) {
        const ref = cobrancaExternalReference(plan.tenantId, plan.period, categoryPlan.category, index);
        const existing = await this.findByRef(ref);
        if (existing && !isCancelled(existing)) {
          items.push({ category: categoryPlan.category, index, externalReference: ref, action: "kept", cobranca: existing });
          continue;
        }
        const valor = categoryPlan.valorCentavosEach[index - 1] ?? 0;
        const created = await this.agenda.cobrancas.create({
          externalReference: ref,
          valorCentavos: valor,
          vencimento,
          pagador: this.pagador,
          idempotencyKey: ref,
        });
        items.push({ category: categoryPlan.category, index, externalReference: ref, action: "created", cobranca: created });
      }
      await this.cancelExtras(plan, categoryPlan.category, categoryPlan.quantity, items);
    }
    return summarize(plan, items);
  }

  private async findByRef(ref: string): Promise<CobrancaRecord | undefined> {
    const listed = await this.agenda.cobrancas.list({ externalReference: ref, perPage: 20 });
    return listed.data.find((c) => c.externalReference === ref && !isCancelled(c));
  }

  private async cancelExtras(
    plan: CobrancaQuantityPlan,
    category: CobrancaQuantityPlan["categories"][number]["category"],
    keep: number,
    items: CobrancaSyncItem[],
  ): Promise<void> {
    // Limita extras: tenta índices keep+1 .. keep+20 até não achar mais.
    const scanUntil = keep + 20;
    for (let index = keep + 1; index <= scanUntil; index += 1) {
      const ref = cobrancaExternalReference(plan.tenantId, plan.period, category, index);
      const existing = await this.findByRef(ref);
      if (!existing) break;
      await this.agenda.cobrancas.cancel(existing.id);
      items.push({ category, index, externalReference: ref, action: "cancelled", cobranca: existing });
    }
  }
}

function isCancelled(cobranca: CobrancaRecord): boolean {
  const status = (cobranca.status || "").toLowerCase();
  return status === "cancelada" || status === "cancelled" || status === "canceled";
}

function summarize(plan: CobrancaQuantityPlan, items: CobrancaSyncItem[]): CobrancaSyncResult {
  return {
    period: plan.period,
    tenantId: plan.tenantId,
    plan,
    items,
    created: items.filter((i) => i.action === "created").length,
    cancelled: items.filter((i) => i.action === "cancelled").length,
    kept: items.filter((i) => i.action === "kept").length,
  };
}
