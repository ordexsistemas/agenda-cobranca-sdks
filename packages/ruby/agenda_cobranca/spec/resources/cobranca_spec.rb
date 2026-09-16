# frozen_string_literal: true

require "spec_helper"

RSpec.describe AgendaCobranca::Resources::Cobranca do
  let(:base_url) { "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo" }
  let(:client) { AgendaCobranca::Client.new }
  let(:resource) { described_class.new(client) }

  let(:cobranca_json) do
    {
      "id" => "11111111-1111-4111-8111-111111111111",
      "external_reference" => "pedido-1",
      "valor_centavos" => 15_000,
      "vencimento" => "2026-10-01",
      "status" => "pendente",
      "pagador" => {
        "documento" => "12345678901",
        "nome" => "Maria Silva",
        "email" => "maria@example.com"
      },
      "juros" => { "percentual_mes" => 1.0 },
      "multa" => { "percentual" => 2.0 }
    }
  end

  def headers_api_key
    {
      "chave_api" => "api_key_exemplo",
      "X-Api-Key" => "api_key_exemplo"
    }
  end

  describe "#create" do
    it "envia POST /cobrancas com Idempotency-Key e headers de api_key" do
      stub = stub_request(:post, "#{base_url}/cobrancas")
             .with(
               headers: headers_api_key.merge("Idempotency-Key" => "idem-1"),
               body: hash_including(
                 "external_reference" => "pedido-1",
                 "valor_centavos" => 15_000,
                 "vencimento" => "2026-10-01"
               )
             )
             .to_return(
               status: 201,
               headers: { "Content-Type" => "application/json" },
               body: { "data" => cobranca_json }.to_json
             )

      cobranca = resource.create(
        external_reference: "pedido-1",
        valor_centavos: 15_000,
        vencimento: "2026-10-01",
        pagador: { documento: "12345678901", nome: "Maria Silva", email: "maria@example.com" },
        juros: { percentual_mes: 1.0 },
        multa: { percentual: 2.0 },
        idempotency_key: "idem-1"
      )

      expect(stub).to have_been_requested
      expect(cobranca.id).to eq("11111111-1111-4111-8111-111111111111")
      expect(cobranca.status).to eq("pendente")
      expect(cobranca.valor_centavos).to eq(15_000)
      expect(cobranca.pagador.nome).to eq("Maria Silva")
    end

    it "nao envia headers HMAC quando signing_enabled e false" do
      stub = stub_request(:post, "#{base_url}/cobrancas")
             .with { |req|
               headers = req.headers.transform_keys { |k| k.to_s.downcase.tr("_", "-") }
               api_ok = (headers["chave-api"] == "api_key_exemplo" || headers["x-api-key"] == "api_key_exemplo")
               hmac_ausente = %w[x-signature x-client-id x-timestamp x-nonce].none? { |h| headers.key?(h) }
               api_ok && hmac_ausente
             }
             .to_return(status: 201, body: cobranca_json.to_json, headers: { "Content-Type" => "application/json" })

      resource.create(
        valor_centavos: 15_000,
        vencimento: "2026-10-01",
        pagador: { documento: "12345678901", nome: "Maria Silva" },
        idempotency_key: "idem-no-hmac"
      )

      expect(stub).to have_been_requested
    end

    it "envia headers HMAC quando signing_enabled e true" do
      AgendaCobranca.configure do |cfg|
        cfg.api_key = "api_key_exemplo"
        cfg.client_id = "client_exemplo"
        cfg.client_secret = "test_client_secret"
        cfg.signing_enabled = true
        cfg.base_url = base_url
      end

      signed_client = AgendaCobranca::Client.new
      signed_resource = described_class.new(signed_client)

      stub = stub_request(:post, "#{base_url}/cobrancas")
             .with(
               headers: headers_api_key.merge(
                 "X-Client-Id" => "client_exemplo",
                 "X-Timestamp" => /.*/,
                 "X-Nonce" => /.*/,
                 "X-Signature" => /[a-f0-9]{64}/,
                 "Idempotency-Key" => "idem-hmac"
               )
             )
             .to_return(status: 201, body: cobranca_json.to_json, headers: { "Content-Type" => "application/json" })

      signed_resource.create(
        valor_centavos: 15_000,
        vencimento: "2026-10-01",
        pagador: { documento: "12345678901", nome: "Maria Silva" },
        idempotency_key: "idem-hmac"
      )

      expect(stub).to have_been_requested
    end

    it "gera Idempotency-Key quando o chamador nao informa" do
      stub = stub_request(:post, "#{base_url}/cobrancas")
             .with { |req| req.headers["Idempotency-Key"] =~ /\A[0-9a-f-]{36}\z/i }
             .to_return(status: 201, body: cobranca_json.to_json, headers: { "Content-Type" => "application/json" })

      resource.create(
        valor_centavos: 15_000,
        vencimento: "2026-10-01",
        pagador: { documento: "12345678901", nome: "Maria Silva" }
      )

      expect(stub).to have_been_requested
    end

    it "mapeia 422 para ValidationError" do
      stub_request(:post, "#{base_url}/cobrancas")
        .to_return(
          status: 422,
          headers: { "Content-Type" => "application/json" },
          body: { "success" => false, "message" => "Dados invalidos", "errors" => ["valor_centavos"] }.to_json
        )

      expect do
        resource.create(
          valor_centavos: 0,
          vencimento: "2026-10-01",
          pagador: { documento: "1", nome: "X" },
          idempotency_key: "idem-err"
        )
      end.to raise_error(AgendaCobranca::ValidationError, /Dados invalidos/)
    end
  end

  describe "#find" do
    it "busca GET /cobrancas/:id" do
      stub = stub_request(:get, "#{base_url}/cobrancas/11111111-1111-4111-8111-111111111111")
             .with(headers: headers_api_key)
             .to_return(status: 200, body: cobranca_json.to_json, headers: { "Content-Type" => "application/json" })

      cobranca = resource.find("11111111-1111-4111-8111-111111111111")

      expect(stub).to have_been_requested
      expect(cobranca.external_reference).to eq("pedido-1")
    end

    it "mapeia 404 para NotFoundError" do
      stub_request(:get, "#{base_url}/cobrancas/missing")
        .to_return(status: 404, body: { "message" => "Cobranca nao encontrada" }.to_json)

      expect { resource.find("missing") }.to raise_error(AgendaCobranca::NotFoundError)
    end
  end

  describe "#list" do
    it "lista GET /cobrancas com filtros" do
      stub = stub_request(:get, "#{base_url}/cobrancas")
             .with(query: { "status" => "pendente", "page" => "1", "per_page" => "20" })
             .to_return(
               status: 200,
               body: { "data" => [cobranca_json], "meta" => { "page" => 1, "per_page" => 20, "total" => 1 } }.to_json,
               headers: { "Content-Type" => "application/json" }
             )

      resultado = resource.list(status: "pendente", page: 1, per_page: 20)

      expect(stub).to have_been_requested
      expect(resultado[:data].size).to eq(1)
      expect(resultado[:meta][:total]).to eq(1)
    end
  end

  describe "#cancel" do
    it "envia POST /cobrancas/:id/cancel" do
      cancelada = cobranca_json.merge("status" => "cancelada")
      stub = stub_request(:post, "#{base_url}/cobrancas/11111111-1111-4111-8111-111111111111/cancel")
             .to_return(status: 200, body: cancelada.to_json, headers: { "Content-Type" => "application/json" })

      cobranca = resource.cancel("11111111-1111-4111-8111-111111111111")

      expect(stub).to have_been_requested
      expect(cobranca.status).to eq("cancelada")
    end
  end
end
