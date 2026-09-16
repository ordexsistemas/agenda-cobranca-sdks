import type { Cobranca, Juros, ListCobrancasResult, ListMeta, Multa, Pagador } from "./types.js";

export function asRecord(value: unknown): Record<string, unknown> {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }
  return {};
}

export function unwrapData(payload: unknown): Record<string, unknown> {
  const rec = asRecord(payload);
  const data = rec.data;
  if (data && typeof data === "object" && !Array.isArray(data)) {
    return data as Record<string, unknown>;
  }
  return rec;
}

function pagadorFrom(raw: unknown): Pagador {
  const rec = asRecord(raw);
  return {
    documento: String(rec.documento ?? ""),
    nome: String(rec.nome ?? ""),
    email: rec.email == null ? undefined : String(rec.email),
  };
}

function jurosFrom(raw: unknown): Juros | undefined {
  if (raw == null || typeof raw !== "object") return undefined;
  const rec = asRecord(raw);
  const percentualMes = rec.percentual_mes ?? rec.percentualMes;
  return percentualMes == null ? {} : { percentualMes: Number(percentualMes) };
}

function multaFrom(raw: unknown): Multa | undefined {
  if (raw == null || typeof raw !== "object") return undefined;
  const rec = asRecord(raw);
  const percentual = rec.percentual;
  return percentual == null ? {} : { percentual: Number(percentual) };
}

export function cobrancaFromResponse(payload: unknown): Cobranca {
  const dados = unwrapData(payload);
  return {
    id: String(dados.id ?? ""),
    externalReference:
      dados.external_reference == null ? undefined : String(dados.external_reference),
    valorCentavos: Number(dados.valor_centavos ?? 0),
    vencimento: String(dados.vencimento ?? ""),
    status: String(dados.status ?? ""),
    pagador: pagadorFrom(dados.pagador),
    juros: jurosFrom(dados.juros),
    multa: multaFrom(dados.multa),
    raw: dados,
  };
}

export function listFromResponse(payload: unknown): ListCobrancasResult {
  if (Array.isArray(payload)) {
    return { data: payload.map(cobrancaFromResponse), meta: {} };
  }
  const rec = asRecord(payload);
  const items = Array.isArray(rec.data)
    ? rec.data
    : Array.isArray(rec.cobrancas)
      ? rec.cobrancas
      : [];
  const metaRaw = asRecord(rec.meta);
  const meta: ListMeta = {};
  const page = metaRaw.page ?? rec.page;
  const perPage = metaRaw.per_page ?? rec.per_page;
  const total = metaRaw.total ?? rec.total;
  if (page != null) meta.page = Number(page);
  if (perPage != null) meta.perPage = Number(perPage);
  if (total != null) meta.total = Number(total);
  return { data: items.map(cobrancaFromResponse), meta };
}

export function compact<T extends Record<string, unknown>>(obj: T): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(obj)) {
    if (value === undefined || value === null) continue;
    if (typeof value === "object" && !Array.isArray(value)) {
      const nested = compact(value as Record<string, unknown>);
      if (Object.keys(nested).length === 0) continue;
      out[key] = nested;
      continue;
    }
    out[key] = value;
  }
  return out;
}
