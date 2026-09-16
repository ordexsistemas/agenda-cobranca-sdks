import type { ClientOptions } from "./types.js";

export interface EnvLike {
  [key: string]: string | undefined;
}

/**
 * Lê placeholders de ambiente. Não falha se estiver incompleto —
 * a validação fica no construtor de `WhatsAppClient`.
 */
export function optionsFromEnv(env: EnvLike = process.env): ClientOptions {
  return {
    accessToken: env.WHATSAPP_ACCESS_TOKEN ?? env.META_ACCESS_TOKEN ?? "",
    phoneNumberId: env.WHATSAPP_PHONE_NUMBER_ID ?? "",
    wabaId: env.WHATSAPP_WABA_ID,
    graphVersion: env.WHATSAPP_GRAPH_VERSION,
    appSecret: env.WHATSAPP_APP_SECRET,
    tenantId: env.ORDEX_PAY_TENANT_ID ?? env.ORDEX_TENANT_ID ?? "",
    ordexApiKey: env.ORDEX_PAY_API_KEY,
    ordexBaseUrl: env.ORDEX_PAY_BASE_URL,
    quotaMode: env.ORDEX_WHATSAPP_QUOTA_MODE === "soft" ? "soft" : "hard",
  };
}
