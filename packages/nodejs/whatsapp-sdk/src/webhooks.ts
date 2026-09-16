import { createHmac, timingSafeEqual } from "node:crypto";
import { ConfigurationError, SignatureError, ValidationError } from "./errors.js";

export interface WebhookChallengeQuery {
  "hub.mode"?: string;
  "hub.verify_token"?: string;
  "hub.challenge"?: string;
  hub_mode?: string;
  hub_verify_token?: string;
  hub_challenge?: string;
}

/**
 * Verificação GET do webhook Meta (`hub.mode=subscribe`).
 * Devolve o `hub.challenge` para o HTTP 200 em texto puro.
 */
export function verifyWebhookChallenge(query: WebhookChallengeQuery, verifyToken: string): string {
  if (!verifyToken || verifyToken.trim() === "") {
    throw new ConfigurationError("WHATSAPP_VERIFY_TOKEN e obrigatorio");
  }
  const mode = query["hub.mode"] ?? query.hub_mode;
  const token = query["hub.verify_token"] ?? query.hub_verify_token;
  const challenge = query["hub.challenge"] ?? query.hub_challenge ?? "";
  if (mode === "subscribe" && token === verifyToken) {
    return String(challenge);
  }
  throw new ValidationError("Token de verificacao de webhook invalido", 403);
}

export function metaSignatureHeader(rawBody: string, appSecret: string): string {
  const hex = createHmac("sha256", appSecret).update(rawBody, "utf8").digest("hex");
  return `sha256=${hex}`;
}

/**
 * Confere `X-Hub-Signature-256` do POST de webhook da Meta.
 */
export function verifyMetaSignature(
  rawBody: string,
  signatureHeader: string | undefined,
  appSecret: string,
): boolean {
  if (!appSecret || appSecret.trim() === "") {
    throw new ConfigurationError("WHATSAPP_APP_SECRET e obrigatorio");
  }
  if (!signatureHeader) return false;
  const expected = metaSignatureHeader(rawBody, appSecret);
  const left = Buffer.from(expected, "utf8");
  const right = Buffer.from(signatureHeader, "utf8");
  if (left.length !== right.length) return false;
  return timingSafeEqual(left, right);
}

export function verifyMetaSignatureOrThrow(
  rawBody: string,
  signatureHeader: string | undefined,
  appSecret: string,
): true {
  if (!verifyMetaSignature(rawBody, signatureHeader, appSecret)) {
    throw new SignatureError("Assinatura X-Hub-Signature-256 invalida");
  }
  return true;
}

export interface InboundMessage {
  from: string;
  id: string;
  timestamp: string;
  type: string;
  text?: string;
  callbackData?: string;
  raw: Record<string, unknown>;
}

/**
 * Extrai mensagens inbound do payload padrão Cloud API.
 */
export function parseInboundMessages(payload: unknown): InboundMessage[] {
  const out: InboundMessage[] = [];
  if (!payload || typeof payload !== "object") return out;
  const entries = (payload as { entry?: unknown[] }).entry;
  if (!Array.isArray(entries)) return out;
  for (const entry of entries) {
    if (!entry || typeof entry !== "object") continue;
    const changes = (entry as { changes?: unknown[] }).changes;
    if (!Array.isArray(changes)) continue;
    for (const change of changes) {
      if (!change || typeof change !== "object") continue;
      const value = (change as { value?: { messages?: unknown[] } }).value;
      const messages = value?.messages;
      if (!Array.isArray(messages)) continue;
      for (const msg of messages) {
        if (!msg || typeof msg !== "object") continue;
        const rec = msg as Record<string, unknown>;
        const textObj = rec.text && typeof rec.text === "object" ? (rec.text as { body?: string }) : undefined;
        out.push({
          from: String(rec.from ?? ""),
          id: String(rec.id ?? ""),
          timestamp: String(rec.timestamp ?? ""),
          type: String(rec.type ?? ""),
          text: textObj?.body,
          callbackData:
            rec.biz_opaque_callback_data == null ? undefined : String(rec.biz_opaque_callback_data),
          raw: rec,
        });
      }
    }
  }
  return out;
}
