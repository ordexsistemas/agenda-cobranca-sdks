import { CobrancaQuantityController, resolveAgendaClient } from "./agenda.js";
import { computeCobrancaQuantity } from "./cobranca-quantity.js";
import { assertEntitlement, OrdexPayEntitlementChecker, StaticEntitlementChecker } from "./entitlement.js";
import { ConfigurationError } from "./errors.js";
import { GraphClient } from "./graph.js";
import { InMemoryUsageStore, MeteringService } from "./metering.js";
import { mergePlan } from "./plan.js";
import type {
  ClientOptions,
  CobrancaQuantityPlan,
  CobrancaSyncResult,
  ConversionOptions,
  EntitlementChecker,
  MediaPayload,
  MessageCategory,
  SendMessageInput,
  SendMessageResult,
  TemplatePayload,
  UsageSnapshot,
} from "./types.js";

export class WhatsAppClient {
  readonly options: Readonly<ClientOptions>;
  readonly metering: MeteringService;
  readonly graph: GraphClient;

  private readonly entitlement?: EntitlementChecker;
  private readonly agendaController?: CobrancaQuantityController;
  private readonly conversion: ConversionOptions;

  constructor(options: ClientOptions) {
    const missing: string[] = [];
    if (!options.accessToken || options.accessToken.trim() === "") missing.push("accessToken");
    if (!options.phoneNumberId || options.phoneNumberId.trim() === "") missing.push("phoneNumberId");
    if (!options.tenantId || options.tenantId.trim() === "") missing.push("tenantId");
    if (missing.length > 0) {
      throw new ConfigurationError(`Configuracao incompleta: ${missing.join(", ")}`);
    }

    const plan = mergePlan(options.plan, options.quotaMode);
    const store = options.usageStore ?? new InMemoryUsageStore();
    const clock = options.clock ?? (() => new Date());
    const timeZone = options.timeZone ?? "America/Sao_Paulo";

    this.options = Object.freeze({ ...options });
    this.metering = new MeteringService(plan, store, clock, timeZone);
    this.graph = new GraphClient({
      accessToken: options.accessToken,
      phoneNumberId: options.phoneNumberId,
      wabaId: options.wabaId,
      graphVersion: options.graphVersion,
      graphBaseUrl: options.graphBaseUrl,
      fetch: options.fetch,
      timeoutMs: options.timeoutMs,
    });
    this.entitlement = resolveEntitlement(options);
    this.conversion = {
      strategy: options.quantityStrategy ?? "per-category",
      sessionsPerCobranca: options.sessionsPerCobranca,
      valorCentavosPorCobranca: options.valorCentavosPorCobranca,
    };

    const agenda = resolveAgendaClient(options);
    if (agenda && options.agendaPagador) {
      this.agendaController = new CobrancaQuantityController(
        agenda,
        options.agendaPagador,
        options.agendaVencimentoDay,
      );
    }
  }

  get plan() {
    return mergePlan(this.options.plan, this.options.quotaMode);
  }

  async usage(period?: string): Promise<UsageSnapshot> {
    return this.metering.snapshot(this.options.tenantId, period);
  }

  cobrancaPlan(counts: UsageSnapshot["counts"], period?: string): CobrancaQuantityPlan {
    return computeCobrancaQuantity(counts, this.plan, {
      ...this.conversion,
      period: period ?? this.metering.period(),
      tenantId: this.options.tenantId,
    });
  }

  async previewCobrancaPlan(period?: string): Promise<CobrancaQuantityPlan> {
    const snap = await this.usage(period);
    return this.cobrancaPlan(snap.counts, snap.period);
  }

  /**
   * Ajusta a agenda: cria cobranças faltantes e cancela extras acima da quantidade
   * calculada a partir do uso WhatsApp do período.
   */
  async syncCobrancas(period?: string): Promise<CobrancaSyncResult> {
    if (!this.agendaController) {
      throw new ConfigurationError(
        "Agenda de Cobrancas nao configurada: informe agendaClient (ou ordexApiKey) e agendaPagador",
      );
    }
    const plan = await this.previewCobrancaPlan(period);
    return this.agendaController.sync(plan);
  }

  async sendTemplate(input: {
    to: string;
    category: MessageCategory;
    template: TemplatePayload;
    callbackData?: string;
  }): Promise<SendMessageResult> {
    return this.send({
      to: input.to,
      category: input.category,
      type: "template",
      template: input.template,
      callbackData: input.callbackData,
    });
  }

  async sendText(input: {
    to: string;
    category?: MessageCategory;
    body: string;
    previewUrl?: boolean;
    callbackData?: string;
  }): Promise<SendMessageResult> {
    return this.send({
      to: input.to,
      category: input.category ?? "service",
      type: "text",
      text: { body: input.body, previewUrl: input.previewUrl },
      callbackData: input.callbackData,
    });
  }

  async sendMedia(input: {
    to: string;
    category: MessageCategory;
    media: MediaPayload;
    callbackData?: string;
  }): Promise<SendMessageResult> {
    return this.send({
      to: input.to,
      category: input.category,
      type: input.media.type,
      media: input.media,
      callbackData: input.callbackData,
    });
  }

  async send(input: SendMessageInput): Promise<SendMessageResult> {
    await assertEntitlement(this.entitlement, this.options.tenantId, Boolean(this.options.skipEntitlementCheck));

    let metering;
    try {
      metering = await this.metering.consume(this.options.tenantId, input.category, 1);
    } catch (err) {
      throw err;
    }

    let meta;
    try {
      meta = await this.graph.sendMessage(input);
    } catch (err) {
      await this.metering.rollback(this.options.tenantId, input.category, 1);
      throw err;
    }

    let cobrancaPlan: CobrancaQuantityPlan | undefined;
    if (this.options.syncCobrancasOnSend && this.agendaController) {
      const sync = await this.syncCobrancas(metering.period);
      cobrancaPlan = sync.plan;
    } else {
      const snap = await this.usage(metering.period);
      cobrancaPlan = this.cobrancaPlan(snap.counts, snap.period);
    }

    return { category: input.category, to: input.to, meta, metering, cobrancaPlan };
  }
}

export function createWhatsAppClient(options: ClientOptions): WhatsAppClient {
  return new WhatsAppClient(options);
}

function resolveEntitlement(options: ClientOptions): EntitlementChecker | undefined {
  if (options.entitlement) return options.entitlement;
  if (options.skipEntitlementCheck) return new StaticEntitlementChecker(true, { plan: "demo" });
  if (options.ordexApiKey) {
    return new OrdexPayEntitlementChecker(options.ordexApiKey, {
      baseUrl: options.ordexBaseUrl,
      fetch: options.fetch,
      timeoutMs: options.timeoutMs,
    });
  }
  return undefined;
}
