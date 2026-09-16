import { ConfigurationError, SignatureError } from "./errors.js";
import { Signer } from "./signer.js";
import { DEFAULT_WEBHOOK_METHOD, DEFAULT_WEBHOOK_PATH } from "./types.js";

function headerValue(
  headers: Record<string, string | undefined> | Headers,
  name: string,
): string | undefined {
  if (headers instanceof Headers) {
    return headers.get(name) ?? undefined;
  }

  const wanted = name.toLowerCase();
  const wantedAlt = wanted.replace(/-/g, "_");
  for (const [key, value] of Object.entries(headers)) {
    if (!value) continue;
    const normalized = key.toLowerCase();
    if (normalized === wanted || normalized === wantedAlt || normalized.replace(/_/g, "-") === wanted) {
      return value;
    }
  }
  return undefined;
}

export class WebhookVerifier {
  static readonly DEFAULT_METHOD = DEFAULT_WEBHOOK_METHOD;
  static readonly DEFAULT_PATH = DEFAULT_WEBHOOK_PATH;

  private readonly signer: Signer;

  constructor(clientSecret: string) {
    if (!clientSecret || clientSecret.trim() === "") {
      throw new ConfigurationError("client_secret e obrigatorio");
    }
    this.signer = new Signer(clientSecret);
  }

  verify(
    payload: string,
    headers: Record<string, string | undefined> | Headers,
    method: string = WebhookVerifier.DEFAULT_METHOD,
    path: string = WebhookVerifier.DEFAULT_PATH,
  ): boolean {
    const timestamp = headerValue(headers, "X-Timestamp");
    const nonce = headerValue(headers, "X-Nonce");
    const signature = headerValue(headers, "X-Signature");
    if (!timestamp || !nonce || !signature) {
      return false;
    }
    return this.signer.validSignature(method, path, timestamp, nonce, signature, payload);
  }

  verifyOrThrow(
    payload: string,
    headers: Record<string, string | undefined> | Headers,
    method: string = WebhookVerifier.DEFAULT_METHOD,
    path: string = WebhookVerifier.DEFAULT_PATH,
  ): true {
    if (!this.verify(payload, headers, method, path)) {
      throw new SignatureError("Assinatura de webhook invalida");
    }
    return true;
  }
}

export function verifyWebhook(
  payload: string,
  headers: Record<string, string | undefined> | Headers,
  clientSecret: string,
  options: { method?: string; path?: string } = {},
): boolean {
  return new WebhookVerifier(clientSecret).verify(
    payload,
    headers,
    options.method ?? WebhookVerifier.DEFAULT_METHOD,
    options.path ?? WebhookVerifier.DEFAULT_PATH,
  );
}

export function verifyWebhookOrThrow(
  payload: string,
  headers: Record<string, string | undefined> | Headers,
  clientSecret: string,
  options: { method?: string; path?: string } = {},
): true {
  return new WebhookVerifier(clientSecret).verifyOrThrow(
    payload,
    headers,
    options.method ?? WebhookVerifier.DEFAULT_METHOD,
    options.path ?? WebhookVerifier.DEFAULT_PATH,
  );
}
