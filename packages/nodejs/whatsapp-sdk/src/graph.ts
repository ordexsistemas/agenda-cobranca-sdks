import { errorFromStatus } from "./errors.js";
import { asRecord, decodeBody } from "./models.js";
import { DEFAULT_GRAPH_BASE_URL, DEFAULT_GRAPH_VERSION } from "./types.js";
import type { FetchLike, MetaMessageResult, SendMessageInput } from "./types.js";

export interface GraphClientOptions {
  accessToken: string;
  phoneNumberId: string;
  wabaId?: string;
  graphVersion?: string;
  graphBaseUrl?: string;
  fetch?: FetchLike;
  timeoutMs?: number;
}

export class GraphClient {
  private readonly base: string;
  private readonly fetchImpl: FetchLike;
  private readonly timeoutMs: number;

  constructor(private readonly options: GraphClientOptions) {
    const version = options.graphVersion?.trim() || DEFAULT_GRAPH_VERSION;
    const root = (options.graphBaseUrl?.trim() || DEFAULT_GRAPH_BASE_URL).replace(/\/+$/, "");
    this.base = `${root}/${version}/`;
    this.fetchImpl = options.fetch ?? fetch;
    this.timeoutMs = options.timeoutMs && options.timeoutMs > 0 ? options.timeoutMs : 30_000;
  }

  get phoneNumberId(): string {
    return this.options.phoneNumberId;
  }

  get wabaId(): string | undefined {
    return this.options.wabaId;
  }

  async sendMessage(input: SendMessageInput): Promise<MetaMessageResult> {
    const body = buildMessageBody(input);
    const payload = await this.request("POST", `${this.options.phoneNumberId}/messages`, body);
    return parseMessageResult(payload);
  }

  async listTemplates(): Promise<unknown> {
    if (!this.options.wabaId) {
      throw new Error("wabaId e obrigatorio para listar templates");
    }
    return this.request("GET", `${this.options.wabaId}/message_templates`);
  }

  async uploadMedia(bytes: Uint8Array, mimeType: string, filename = "file"): Promise<{ id: string }> {
    const form = new FormData();
    form.set("messaging_product", "whatsapp");
    form.set("type", mimeType);
    form.set("file", new Blob([Buffer.from(bytes)], { type: mimeType }), filename);
    const payload = await this.request(
      "POST",
      `${this.options.phoneNumberId}/media`,
      form,
    );
    const rec = asRecord(payload);
    return { id: String(rec.id ?? "") };
  }

  async getMedia(mediaId: string): Promise<{ url?: string; mimeType?: string; sha256?: string; fileSize?: number; raw: unknown }> {
    const payload = await this.request("GET", mediaId);
    const rec = asRecord(payload);
    return {
      url: rec.url == null ? undefined : String(rec.url),
      mimeType: rec.mime_type == null ? undefined : String(rec.mime_type),
      sha256: rec.sha256 == null ? undefined : String(rec.sha256),
      fileSize: rec.file_size == null ? undefined : Number(rec.file_size),
      raw: payload,
    };
  }

  async request(method: string, path: string, body?: unknown): Promise<unknown> {
    const url = new URL(path.replace(/^\/+/, ""), this.base);
    const headers = new Headers();
    headers.set("Authorization", `Bearer ${this.options.accessToken}`);
    headers.set("Accept", "application/json");

    let encoded: string | FormData | undefined;
    if (body instanceof FormData) {
      encoded = body;
    } else if (body !== undefined && body !== null) {
      encoded = JSON.stringify(body);
      headers.set("Content-Type", "application/json");
    }

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    let response: Response;
    try {
      response = await this.fetchImpl(url, {
        method: method.toUpperCase(),
        headers,
        body: encoded,
        signal: controller.signal,
      });
    } finally {
      clearTimeout(timer);
    }

    const payload = decodeBody(await response.text());
    if (response.ok) return payload;

    const rec = asRecord(payload);
    const err = asRecord(rec.error);
    const message =
      (typeof err.message === "string" && err.message) ||
      (typeof rec.message === "string" && rec.message) ||
      `Erro HTTP ${response.status} na Graph API`;
    throw errorFromStatus(response.status, message, payload, rec.error ?? rec.errors ?? null);
  }
}

export function buildMessageBody(input: SendMessageInput): Record<string, unknown> {
  const body: Record<string, unknown> = {
    messaging_product: "whatsapp",
    recipient_type: "individual",
    to: input.to,
    type: input.type === "template" ? "template" : input.type,
  };

  const callback = buildCallbackData(input);
  if (callback) body.biz_opaque_callback_data = callback;

  if (input.type === "template") {
    if (!input.template?.name) {
      throw new Error("template.name e obrigatorio para envio de template");
    }
    body.template = {
      name: input.template.name,
      language: { code: input.template.language || "pt_BR" },
      components: input.template.components,
    };
    return body;
  }

  if (input.type === "text") {
    if (!input.text?.body) {
      throw new Error("text.body e obrigatorio para mensagem de sessao");
    }
    body.text = {
      preview_url: Boolean(input.text.previewUrl),
      body: input.text.body,
    };
    return body;
  }

  const media = input.media;
  if (!media || (!media.id && !media.link)) {
    throw new Error("media.id ou media.link e obrigatorio");
  }
  const mediaBody: Record<string, unknown> = {};
  if (media.id) mediaBody.id = media.id;
  if (media.link) mediaBody.link = media.link;
  if (media.caption) mediaBody.caption = media.caption;
  if (media.filename) mediaBody.filename = media.filename;
  body[input.type] = mediaBody;
  return body;
}

function buildCallbackData(input: SendMessageInput): string | undefined {
  if (input.callbackData) return input.callbackData.slice(0, 512);
  const payload = JSON.stringify({ category: input.category });
  return payload.length <= 512 ? payload : undefined;
}

export function parseMessageResult(payload: unknown): MetaMessageResult {
  const rec = asRecord(payload);
  const contactsRaw = Array.isArray(rec.contacts) ? rec.contacts : [];
  const messagesRaw = Array.isArray(rec.messages) ? rec.messages : [];
  return {
    messagingProduct: rec.messaging_product == null ? undefined : String(rec.messaging_product),
    contacts: contactsRaw.map((c) => {
      const row = asRecord(c);
      return {
        input: row.input == null ? undefined : String(row.input),
        waId: row.wa_id == null ? undefined : String(row.wa_id),
      };
    }),
    messages: messagesRaw.map((m) => {
      const row = asRecord(m);
      return {
        id: String(row.id ?? ""),
        messageStatus: row.message_status == null ? undefined : String(row.message_status),
      };
    }),
    raw: payload,
  };
}
