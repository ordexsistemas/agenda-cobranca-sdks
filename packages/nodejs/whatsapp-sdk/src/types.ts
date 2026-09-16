export const DEFAULT_ORDEX_BASE_URL =
  "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo";

export const DEFAULT_GRAPH_BASE_URL = "https://graph.facebook.com";

export const DEFAULT_GRAPH_VERSION = "v21.0";

export const MESSAGE_CATEGORIES = ["auth", "utility", "service", "marketing"] as const;

export type MessageCategory = (typeof MESSAGE_CATEGORIES)[number];

export type QuotaMode = "hard" | "soft";

export type QuantityStrategy = "per-category" | "by-sessions" | "by-value";

export type FetchLike = typeof fetch;

export interface CategoryQuota {
  /** Total de sessões/envios no mês (inclui franquia gratuita). */
  monthlyQuota: number;
  /** Custo unitário Meta/Ordex em USD. */
  unitCostUsd: number;
  /** Sessões gratuitas no mês (Service = 1.000 no Cenário Base). */
  freeAllowance: number;
}

export type CategoryQuotaMap = Record<MessageCategory, CategoryQuota>;

export interface PlanQuotas {
  categories: CategoryQuotaMap;
  usdToBrl: number;
  quotaMode: QuotaMode;
}

export interface UsageCounts {
  auth: number;
  utility: number;
  service: number;
  marketing: number;
}

export interface UsageSnapshot {
  tenantId: string;
  period: string;
  counts: UsageCounts;
}

export interface UsageStore {
  get(tenantId: string, period: string): Promise<UsageSnapshot>;
  /**
   * Soma `delta` (pode ser negativo para rollback) e devolve o snapshot após a operação.
   */
  increment(
    tenantId: string,
    period: string,
    category: MessageCategory,
    delta: number,
  ): Promise<UsageSnapshot>;
}

export interface MeteringDecision {
  category: MessageCategory;
  period: string;
  used: number;
  quota: number;
  remaining: number;
  billableDelta: number;
  freeDelta: number;
  overage: boolean;
  overageDelta: number;
  allowed: boolean;
  quotaMode: QuotaMode;
}

export interface EntitlementResult {
  enabled: boolean;
  tenantId: string;
  addOn: "whatsapp";
  plan?: string;
  message?: string;
  raw?: unknown;
}

export interface EntitlementChecker {
  check(tenantId: string): Promise<EntitlementResult>;
}

export interface Pagador {
  documento: string;
  nome: string;
  email?: string;
}

export interface CobrancaRecord {
  id: string;
  externalReference?: string;
  valorCentavos: number;
  vencimento: string;
  status: string;
  pagador?: Pagador;
  raw?: unknown;
}

export interface CreateCobrancaInput {
  externalReference?: string;
  valorCentavos: number;
  vencimento: string;
  pagador: Pagador;
  idempotencyKey?: string;
}

export interface ListCobrancasInput {
  status?: string;
  externalReference?: string;
  page?: number;
  perPage?: number;
}

export interface ListCobrancasResult {
  data: CobrancaRecord[];
  meta?: { page?: number; perPage?: number; total?: number };
}

/**
 * Superfície mínima da Agenda de Cobranças usada por este add-on.
 * Compatível com `@ordexsistemas/agenda-cobranca` (`Client.cobrancas`).
 */
export interface AgendaCobrancaLike {
  cobrancas: {
    create(input: CreateCobrancaInput): Promise<CobrancaRecord>;
    list(filtros?: ListCobrancasInput): Promise<ListCobrancasResult>;
    cancel(id: string): Promise<CobrancaRecord | unknown>;
  };
}

export interface TemplateComponent {
  type: "header" | "body" | "button" | "footer" | string;
  sub_type?: string;
  index?: string | number;
  parameters?: Array<Record<string, unknown>>;
}

export interface TemplatePayload {
  name: string;
  language: string;
  components?: TemplateComponent[];
}

export interface MediaPayload {
  type: "image" | "audio" | "document" | "video" | "sticker";
  id?: string;
  link?: string;
  caption?: string;
  filename?: string;
}

export interface SendMessageInput {
  to: string;
  category: MessageCategory;
  /**
   * `template` (auth/utility/marketing) ou sessão (`text`/`media`, em geral `service`).
   */
  type: "template" | "text" | "image" | "audio" | "document" | "video" | "sticker";
  template?: TemplatePayload;
  text?: { body: string; previewUrl?: boolean };
  media?: MediaPayload;
  /** Correlação opcional devolvida nos webhooks da Meta (`biz_opaque_callback_data`). */
  callbackData?: string;
}

export interface MetaMessageResult {
  messagingProduct?: string;
  contacts?: Array<{ input?: string; waId?: string }>;
  messages?: Array<{ id: string; messageStatus?: string }>;
  raw: unknown;
}

export interface SendMessageResult {
  category: MessageCategory;
  to: string;
  meta: MetaMessageResult;
  metering: MeteringDecision;
  cobrancaPlan?: CobrancaQuantityPlan;
}

export interface CategoryCobrancaPlan {
  category: MessageCategory;
  used: number;
  billable: number;
  free: number;
  quota: number;
  overage: number;
  unitCostUsd: number;
  costUsd: number;
  costBrlCentavos: number;
  quantity: number;
  valorCentavosEach: number[];
}

export interface CobrancaQuantityPlan {
  period: string;
  tenantId: string;
  categories: CategoryCobrancaPlan[];
  totalQuantity: number;
  totalCostUsd: number;
  totalCostBrlCentavos: number;
  strategy: QuantityStrategy;
}

export interface ConversionOptions {
  strategy?: QuantityStrategy;
  /** Usado em `by-sessions`. Número único ou override por categoria. */
  sessionsPerCobranca?: number | Partial<Record<MessageCategory, number>>;
  /** Usado em `by-value`. Default R$ 1.000,00 (100_000 centavos). */
  valorCentavosPorCobranca?: number;
  includeZeroCategories?: boolean;
}

export interface CobrancaSyncItem {
  category: MessageCategory;
  index: number;
  externalReference: string;
  action: "created" | "kept" | "cancelled";
  cobranca?: CobrancaRecord;
}

export interface CobrancaSyncResult {
  period: string;
  tenantId: string;
  plan: CobrancaQuantityPlan;
  items: CobrancaSyncItem[];
  created: number;
  cancelled: number;
  kept: number;
}

export interface ClientOptions {
  accessToken: string;
  phoneNumberId: string;
  wabaId?: string;
  graphVersion?: string;
  graphBaseUrl?: string;
  appSecret?: string;

  tenantId: string;
  ordexApiKey?: string;
  ordexBaseUrl?: string;

  plan?: DeepPartialPlan;
  quotaMode?: QuotaMode;
  usageStore?: UsageStore;
  clock?: () => Date;
  timeZone?: string;

  entitlement?: EntitlementChecker;
  /** Pula a checagem (somente testes). Produção deve manter o add-on plugável. */
  skipEntitlementCheck?: boolean;

  agendaClient?: AgendaCobrancaLike;
  agendaPagador?: Pagador;
  agendaVencimentoDay?: number;
  syncCobrancasOnSend?: boolean;
  quantityStrategy?: QuantityStrategy;
  sessionsPerCobranca?: number | Partial<Record<MessageCategory, number>>;
  valorCentavosPorCobranca?: number;

  fetch?: FetchLike;
  timeoutMs?: number;
}

export type DeepPartialPlan = {
  usdToBrl?: number;
  quotaMode?: QuotaMode;
  categories?: Partial<Record<MessageCategory, Partial<CategoryQuota>>>;
};

export interface GraphErrorBody {
  message?: string;
  type?: string;
  code?: number;
  errorSubcode?: number;
  fbtraceId?: string;
}
