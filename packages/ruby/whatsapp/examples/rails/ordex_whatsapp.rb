# frozen_string_literal: true

# config/initializers/ordex_whatsapp.rb
#
# Copie este arquivo para a aplicacao Rails. Nunca commite tokens reais.

require "ordex_whatsapp"

ORDEX_WHATSAPP = OrdexWhatsApp::Client.new(
  access_token: ENV.fetch("WHATSAPP_ACCESS_TOKEN"),
  phone_number_id: ENV.fetch("WHATSAPP_PHONE_NUMBER_ID"),
  waba_id: ENV["WHATSAPP_WABA_ID"],
  tenant_id: ENV.fetch("ORDEX_PAY_TENANT_ID"),
  ordex_api_key: ENV["ORDEX_PAY_API_KEY"],
  ordex_base_url: ENV.fetch(
    "ORDEX_PAY_BASE_URL",
    "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
  ),
  agenda_pagador: {
    documento: ENV.fetch("ORDEX_PAY_PAGADOR_DOCUMENTO", "00000000000"),
    nome: ENV.fetch("ORDEX_PAY_PAGADOR_NOME", "Empresa SaaS")
  },
  entitlement: OrdexWhatsApp::StaticEntitlementChecker.new(true) # demo; produção: OrdexPayEntitlementChecker
)

# Webhook Meta (controller):
#   challenge = OrdexWhatsApp::Webhooks.verify_challenge(params, ENV.fetch("WHATSAPP_VERIFY_TOKEN"))
#   OrdexWhatsApp::Webhooks.verify_signature!(request.raw_post, request.headers["X-Hub-Signature-256"], ENV.fetch("WHATSAPP_APP_SECRET"))
