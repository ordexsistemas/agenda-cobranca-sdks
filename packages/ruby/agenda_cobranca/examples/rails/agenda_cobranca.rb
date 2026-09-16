# frozen_string_literal: true

# config/initializers/agenda_cobranca.rb
#
# Copie este arquivo para a aplicacao Rails e preencha a API key
# via variaveis de ambiente. Nunca commite a chave.

AgendaCobranca.configure do |config|
  config.api_key = ENV.fetch("ORDEX_PAY_API_KEY")
  config.base_url = ENV.fetch(
    "ORDEX_PAY_BASE_URL",
    "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
  )
  # HMAC opcional (OFF por padrao):
  # config.signing_enabled = true
  # config.client_id = ENV.fetch("ORDEX_PAY_CLIENT_ID")
  # config.client_secret = ENV.fetch("ORDEX_PAY_CLIENT_SECRET")
  config.verify_license_on_initialize = ENV.fetch("ORDEX_PAY_VERIFY_LICENSE", "false") == "true"
end
