# frozen_string_literal: true

require "spec_helper"

RSpec.describe OrdexWhatsApp::Webhooks do
  it "devolve hub.challenge no GET de verificacao" do
    challenge = described_class.verify_challenge(
      { "hub.mode" => "subscribe", "hub.verify_token" => "segredo", "hub.challenge" => "12345" },
      "segredo"
    )
    expect(challenge).to eq("12345")
  end

  it "rejeita verify_token errado" do
    expect do
      described_class.verify_challenge(
        { "hub.mode" => "subscribe", "hub.verify_token" => "x", "hub.challenge" => "1" },
        "segredo"
      )
    end.to raise_error(OrdexWhatsApp::ValidationError)
  end

  it "valida X-Hub-Signature-256" do
    body = '{"object":"whatsapp_business_account"}'
    header = described_class.signature_header(body, "app_secret_exemplo")
    expect(described_class.verify_signature(body, header, "app_secret_exemplo")).to be true
    expect(described_class.verify_signature(body, header, "outro")).to be false
    expect { described_class.verify_signature!("{}", header, "app_secret_exemplo") }
      .to raise_error(OrdexWhatsApp::SignatureError)
  end

  it "parseia mensagens inbound" do
    payload = {
      "entry" => [
        {
          "changes" => [
            {
              "value" => {
                "messages" => [
                  { "from" => "5511", "id" => "wamid.1", "timestamp" => "1", "type" => "text", "text" => { "body" => "pix" } }
                ]
              }
            }
          ]
        }
      ]
    }
    msgs = described_class.parse_inbound_messages(payload)
    expect(msgs.length).to eq(1)
    expect(msgs[0][:text]).to eq("pix")
    expect(msgs[0][:from]).to eq("5511")
  end
end

RSpec.describe OrdexWhatsApp::OrdexPayEntitlementChecker do
  let(:base) { "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo" }

  it "usa POST /addons/whatsapp/entitlement quando existe" do
    stub_request(:post, "#{base}/addons/whatsapp/entitlement")
      .to_return(status: 200, body: { data: { enabled: true, plan: "saas" } }.to_json, headers: { "Content-Type" => "application/json" })
    checker = described_class.new("chave")
    result = checker.check("acme")
    expect(result[:enabled]).to be true
    expect(result[:plan]).to eq("saas")
  end

  it "faz fallback para licenses/verify e le addons.whatsapp" do
    stub_request(:post, "#{base}/addons/whatsapp/entitlement")
      .to_return(status: 404, body: { message: "not found" }.to_json)
    stub_request(:post, "#{base}/licenses/verify")
      .to_return(
        status: 200,
        body: { success: true, data: { valid: true, addons: { whatsapp: { enabled: true } } } }.to_json,
        headers: { "Content-Type" => "application/json" }
      )
    checker = described_class.new("chave")
    result = checker.check("acme")
    expect(result[:enabled]).to be true
  end

  it "StaticEntitlementChecker cobre o mock de demo" do
    on = OrdexWhatsApp::StaticEntitlementChecker.new(true).check("t")
    off = OrdexWhatsApp::StaticEntitlementChecker.new(false).check("t")
    expect(on[:enabled]).to be true
    expect(off[:enabled]).to be false
    expect(on[:add_on]).to eq("whatsapp")
  end
end
