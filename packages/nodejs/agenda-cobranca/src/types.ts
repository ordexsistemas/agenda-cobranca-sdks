export const DEFAULT_BASE_URL =
  "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo";

export const DEFAULT_WEBHOOK_METHOD = "POST";
export const DEFAULT_WEBHOOK_PATH = "/v1/webhooks";

export type FetchLike = typeof fetch;

export interface ClientOptions {
  apiKey: string;
  baseUrl?: string;
  clientId?: string;
  clientSecret?: string;
  /** HMAC off by default. When true, also sends X-Client-Id / X-Timestamp / X-Nonce / X-Signature. */
  signingEnabled?: boolean;
  timeoutMs?: number;
  verifyLicenseOnInitialize?: boolean;
  /** Unix time in seconds. Injected in tests. */
  clock?: () => number;
  nonceGenerator?: () => string;
  fetch?: FetchLike;
}

export interface Pagador {
  documento: string;
  nome: string;
  email?: string;
}

export interface Juros {
  percentualMes?: number;
}

export interface Multa {
  percentual?: number;
}

export interface Cobranca {
  id: string;
  externalReference?: string;
  valorCentavos: number;
  vencimento: string;
  status: string;
  pagador: Pagador;
  juros?: Juros;
  multa?: Multa;
  raw: Record<string, unknown>;
}

export interface CreateCobrancaInput {
  externalReference?: string;
  valorCentavos: number;
  vencimento: string;
  pagador: Pagador;
  juros?: Juros;
  multa?: Multa;
  idempotencyKey?: string;
}

export interface ListCobrancasInput {
  status?: string;
  externalReference?: string;
  page?: number;
  perPage?: number;
}

export interface ListMeta {
  page?: number;
  perPage?: number;
  total?: number;
}

export interface ListCobrancasResult {
  data: Cobranca[];
  meta: ListMeta;
}

export interface LicenseVerifyResult {
  valid: boolean;
  message?: string;
  raw: unknown;
}

export interface SignedHeaders {
  timestamp: string;
  nonce: string;
  signature: string;
  bodyHash: string;
}
