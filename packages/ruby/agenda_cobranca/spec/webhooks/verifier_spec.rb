# frozen_string_literal: true

require "spec_helper"

RSpec.describe AgendaCobranca::Webhooks::Verifier do
  let(:secret) { "test_client_secret" }
  let(:payload) { '{"id":"evt-1"}' }
  let(:headers) do
    {
      "X-Timestamp" => "1700000000",
      "X-Nonce" => "550e8400-e29b-41d4-a716-446655440000",
      "X-Signature" => "bcf2bd3d052eaaa22c83a552dc1cdbf33e9613fa34196f6920dcfa02db0052f6"
    }
  end

  it "valida webhook com a mesma canonical string dos SDKs" do
    expect(described_class.new(secret).verify(payload: payload, headers: headers)).to be(true)
    expect(AgendaCobranca::Webhooks.verify(payload, headers, client_secret: secret)).to be(true)
  end

  it "rejeita payload adulterado" do
    expect(described_class.new(secret).verify(payload: '{"id":"evt-2"}', headers: headers)).to be(false)
  end

  it "levanta SignatureError em verify! quando invalido" do
    expect do
      described_class.new(secret).verify!(payload: "{}", headers: headers)
    end.to raise_error(AgendaCobranca::SignatureError)
  end
end
