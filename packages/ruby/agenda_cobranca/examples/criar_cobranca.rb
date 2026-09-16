# frozen_string_literal: true

require "agenda_cobranca"

AgendaCobranca.configure do |config|
  config.client_id = ENV.fetch("AGENDA_COBRANCA_CLIENT_ID", "seu_client_id")
  config.api_key = ENV.fetch("AGENDA_COBRANCA_API_KEY", "sua_api_key")
  config.client_secret = ENV.fetch("AGENDA_COBRANCA_CLIENT_SECRET", "seu_client_secret")
  config.base_url = ENV.fetch("AGENDA_COBRANCA_BASE_URL", "https://api.agendacobranca.example/v1")
end

client = AgendaCobranca::Client.new

cobranca = client.cobrancas.create(
  external_reference: "pedido-1001",
  valor_centavos: 15_000,
  vencimento: "2026-10-01",
  pagador: {
    documento: "12345678901",
    nome: "Maria Silva",
    email: "maria@example.com"
  },
  juros: { percentual_mes: 1.0 },
  multa: { percentual: 2.0 },
  idempotency_key: "pedido-1001-cobranca"
)

puts cobranca.to_h
