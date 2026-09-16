# frozen_string_literal: true

require "agenda_cobranca"

AgendaCobranca.configure do |config|
  config.api_key = ENV.fetch("ORDEX_PAY_API_KEY", "sua_api_key")
  config.base_url = ENV.fetch(
    "ORDEX_PAY_BASE_URL",
    "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
  )
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
