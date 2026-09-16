import { CobrancasResource } from "./cobrancas.js";
import { errorFromStatus, ConfigurationError } from "./errors.js";
import { LicensesResource } from "./licenses.js";
import { asRecord } from "./models.js";
import { Signer } from "./signer.js";
import {
  DEFAULT_BASE_URL,
  type ClientOptions,
  type FetchLike,
} from "./types.js";

export interface RequestOptions {
  body?: unknown;
  query?: Record<string, string>;
  headers?: Record<string, string>;
}

function normalizeBaseUrl(baseUrl: string): string {
  return baseUrl.replace(/\/+$/, "") + "/";
}

function requestPath(url: URL): string {
  return url.pathname;
}

export class Client {
  readonly options: Readonly<ClientOptions>;
  readonly cobrancas: CobrancasResource;
  readonly licenses: LicensesResource;

  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;
  private readonly signer: Signer | null;
  private readonly timeoutMs: number;

  constructor(options: ClientOptions) {
    const missing: string[] = [];
    if (!options.apiKey || options.apiKey.trim() === "") missing.push("apiKey");
    if (options.signingEnabled && (!options.clientSecret || options.clientSecret.trim() === "")) {
      missing.push("clientSecret");
    }
    if (missing.length > 0) {
      throw new ConfigurationError(`Configuracao incompleta: ${missing.join(", ")}`);
    }

    this.options = Object.freeze({ ...options });
    this.baseUrl = normalizeBaseUrl(options.baseUrl?.trim() || DEFAULT_BASE_URL);
    this.fetchImpl = options.fetch ?? fetch;
    this.timeoutMs = options.timeoutMs && options.timeoutMs > 0 ? options.timeoutMs : 30_000;
    this.signer = options.signingEnabled
      ? new Signer(
          options.clientSecret as string,
          options.clock,
          options.nonceGenerator,
        )
      : null;
    this.cobrancas = new CobrancasResource(this);
    this.licenses = new LicensesResource(this);
  }

  async request(method: string, path: string, opts: RequestOptions = {}): Promise<unknown> {
    const url = new URL(path.replace(/^\/+/, ""), this.baseUrl);
    if (opts.query) {
      for (const [key, value] of Object.entries(opts.query)) {
        if (value !== "") url.searchParams.set(key, value);
      }
    }

    const headers = new Headers(opts.headers);
    headers.set("Accept", "application/json");
    headers.set("chave_api", this.options.apiKey);
    headers.set("X-Api-Key", this.options.apiKey);

    let bodyText: string | undefined;
    if (opts.body !== undefined && opts.body !== null) {
      bodyText = JSON.stringify(opts.body);
      headers.set("Content-Type", "application/json");
    }

    if (this.options.signingEnabled) {
      if (!this.signer) {
        throw new ConfigurationError("signer ausente com signingEnabled");
      }
      const signed = this.signer.sign(method, requestPath(url), bodyText ?? "");
      headers.set("X-Client-Id", this.options.clientId ?? "");
      headers.set("X-Timestamp", signed.timestamp);
      headers.set("X-Nonce", signed.nonce);
      headers.set("X-Signature", signed.signature);
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

    const rawText = await response.text();
    const payload = decodeBody(rawText);
    if (response.ok) {
      return payload;
    }

    const rec = asRecord(payload);
    const message =
      (typeof rec.message === "string" && rec.message) ||
      (typeof rec.error === "string" && rec.error) ||
      `Erro HTTP ${response.status}`;
    const errors = rec.errors ?? rec.error ?? null;
    throw errorFromStatus(response.status, message, payload, errors);
  }
}

export async function createClient(options: ClientOptions): Promise<Client> {
  const client = new Client(options);
  if (options.verifyLicenseOnInitialize) {
    await client.licenses.verify();
  }
  return client;
}

function decodeBody(raw: string): unknown {
  const trimmed = raw.trim();
  if (!trimmed) return {};
  try {
    return JSON.parse(trimmed);
  } catch {
    return { raw };
  }
}
