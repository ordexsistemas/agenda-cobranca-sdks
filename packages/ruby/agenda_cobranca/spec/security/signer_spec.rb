# frozen_string_literal: true

require "spec_helper"

RSpec.describe AgendaCobranca::Security::Signer do
  let(:secret) { "test_client_secret" }
  let(:timestamp) { 1_700_000_000 }
  let(:nonce) { "550e8400-e29b-41d4-a716-446655440000" }
  let(:signer) do
    described_class.new(
      secret,
      clock: -> { timestamp },
      nonce_generator: -> { nonce }
    )
  end

  describe "#canonical_string" do
    it "monta METHOD, PATH, TIMESTAMP, NONCE e HASH_SHA256 do body" do
      corpo = '{"external_reference":"pedido-1","valor_centavos":15000}'
      canonical = signer.canonical_string(
        method: "POST",
        path: "/v1/cobrancas",
        timestamp: timestamp,
        nonce: nonce,
        body: corpo
      )

      expect(canonical).to eq(
        "POST\n/v1/cobrancas\n1700000000\n550e8400-e29b-41d4-a716-446655440000\n" \
        "d453a65109b62169820d03d496180180c9ffd50248be209887b3786f374d0a75"
      )
    end

    it "usa SHA256 de string vazia quando o body e nil" do
      canonical = signer.canonical_string(
        method: "GET",
        path: "/v1/cobrancas/abc",
        timestamp: timestamp,
        nonce: nonce,
        body: nil
      )

      expect(canonical).to end_with("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")
    end
  end

  describe "#sign" do
    it "produz a assinatura HMAC-SHA256 do vetor canonico compartilhado" do
      resultado = signer.sign(
        method: "post",
        path: "/v1/cobrancas",
        body: '{"external_reference":"pedido-1","valor_centavos":15000}'
      )

      expect(resultado[:timestamp]).to eq("1700000000")
      expect(resultado[:nonce]).to eq(nonce)
      expect(resultado[:signature]).to eq("f0334aadd5c24c375365a83cf301e8c39a5686f7680b0be51f84c95836cb60a3")
    end

    it "assina GET sem body com o vetor compartilhado" do
      resultado = signer.sign(method: "GET", path: "/v1/cobrancas/abc", body: "")

      expect(resultado[:signature]).to eq("19eadbfc80ca376521472ef1297e4513e5f8ef17dedcee5e88cc34a6492349c9")
    end
  end

  describe "#valid_signature?" do
    it "aceita assinatura valida e rejeita adulteracao" do
      corpo = '{"external_reference":"pedido-1","valor_centavos":15000}'
      assinatura = signer.signature_for(
        method: "POST",
        path: "/v1/cobrancas",
        timestamp: timestamp,
        nonce: nonce,
        body: corpo
      )

      expect(
        signer.valid_signature?(
          method: "POST",
          path: "/v1/cobrancas",
          timestamp: timestamp,
          nonce: nonce,
          body: corpo,
          signature: assinatura
        )
      ).to be(true)

      expect(
        signer.valid_signature?(
          method: "POST",
          path: "/v1/cobrancas",
          timestamp: timestamp,
          nonce: nonce,
          body: '{"valor_centavos":1}',
          signature: assinatura
        )
      ).to be(false)
    end
  end

  it "nao aceita secret vazio" do
    expect { described_class.new("") }.to raise_error(AgendaCobranca::ConfigurationError)
  end
end
