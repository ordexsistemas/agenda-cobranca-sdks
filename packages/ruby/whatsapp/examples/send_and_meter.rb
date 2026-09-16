# frozen_string_literal: true

# Exemplo local (WebMock / Faraday stub na aplicacao). Placeholders apenas.

require "ordex_whatsapp"

client = OrdexWhatsApp::Client.new(
  access_token: ENV.fetch("WHATSAPP_ACCESS_TOKEN", "placeholder_meta_token"),
  phone_number_id: ENV.fetch("WHATSAPP_PHONE_NUMBER_ID", "placeholder_phone_number_id"),
  waba_id: ENV.fetch("WHATSAPP_WABA_ID", "placeholder_waba_id"),
  tenant_id: ENV.fetch("ORDEX_PAY_TENANT_ID", "tenant-demo"),
  entitlement: OrdexWhatsApp::StaticEntitlementChecker.new(true, plan: "saas"),
  quota_mode: "soft",
  skip_entitlement_check: false
)

plan = OrdexWhatsApp::CobrancaQuantity.compute(
  OrdexWhatsApp::CENARIO_BASE_VOLUME,
  client.plan,
  period: "2026-09",
  tenant_id: "tenant-demo",
  strategy: "per-category"
)

puts "Cenario Base -> #{plan[:total_quantity]} cobrancas, US$ #{plan[:total_cost_usd]}, #{plan[:total_cost_brl_centavos]} centavos BRL"
