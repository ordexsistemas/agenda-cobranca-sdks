/**
 * Exemplo de add-on SaaS: envio por categoria, medição e preview da
 * quantidade de cobranças. Usa fetch mock — não chama Meta nem Ordex de verdade.
 *
 * Copie o padrão e troque `fetch` + tokens de ambiente (veja .env.example).
 */
import {
  CENARIO_BASE_VOLUME,
  computeCobrancaQuantity,
  InMemoryUsageStore,
  StaticEntitlementChecker,
  WhatsAppClient,
} from "../src/index.js";

const metaOk = {
  messaging_product: "whatsapp",
  contacts: [{ input: "5511999999999", wa_id: "5511999999999" }],
  messages: [{ id: "wamid.EXAMPLE" }],
};

const fetchMock = (async () =>
  new Response(JSON.stringify(metaOk), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  })) as typeof fetch;

const client = new WhatsAppClient({
  accessToken: process.env.WHATSAPP_ACCESS_TOKEN ?? "placeholder_meta_token",
  phoneNumberId: process.env.WHATSAPP_PHONE_NUMBER_ID ?? "placeholder_phone_number_id",
  wabaId: process.env.WHATSAPP_WABA_ID ?? "placeholder_waba_id",
  tenantId: process.env.ORDEX_PAY_TENANT_ID ?? "tenant-demo",
  entitlement: new StaticEntitlementChecker(true, { plan: "saas" }),
  usageStore: new InMemoryUsageStore(),
  quotaMode: "soft",
  fetch: fetchMock,
});

const utility = await client.sendTemplate({
  to: "5511999999999",
  category: "utility",
  template: { name: "pix_recebido", language: "pt_BR" },
});

const support = await client.sendText({
  to: "5511999999999",
  category: "service",
  body: "Olá, sou o suporte Ordex Pay. Como posso ajudar?",
});

console.log("utility", utility.metering);
console.log("service", support.metering);

const baseline = computeCobrancaQuantity(CENARIO_BASE_VOLUME, client.plan, {
  tenantId: "tenant-demo",
  period: "2026-09",
  strategy: "per-category",
});

console.log(
  `Cenário Base → ${baseline.totalQuantity} cobranças, US$ ${baseline.totalCostUsd}, ${baseline.totalCostBrlCentavos} centavos BRL`,
);
