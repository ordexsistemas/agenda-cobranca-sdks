# frozen_string_literal: true

# config/initializers/agenda_cobranca.rb
#
# Copie este arquivo para a aplicacao Rails e preencha as credenciais
# via variaveis de ambiente. Nunca commite client_secret.

AgendaCobranca.configure do |config|
  config.client_id = ENV.fetch("AGENDA_COBRANCA_CLIENT_ID")
  config.api_key = ENV.fetch("AGENDA_COBRANCA_API_KEY")
  config.client_secret = ENV.fetch("AGENDA_COBRANCA_CLIENT_SECRET")
  config.base_url = ENV.fetch("AGENDA_COBRANCA_BASE_URL", "https://api.agendacobranca.example/v1")
  config.verify_license_on_initialize = ENV.fetch("AGENDA_COBRANCA_VERIFY_LICENSE", "false") == "true"
end
