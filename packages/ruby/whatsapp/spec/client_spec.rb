# frozen_string_literal: true

require "spec_helper"

class MemoryAgenda
  attr_reader :items

  def initialize
    @items = {}
  end

  def create(input)
    rec = {
      id: "cob-#{input[:external_reference]}",
      external_reference: input[:external_reference],
      valor_centavos: input[:valor_centavos],
      vencimento: input[:vencimento],
      status: "pendente",
      pagador: input[:pagador]
    }
    @items[rec[:id]] = rec
    rec
  end

  def list(filtros = {})
    @items.values.select do |c|
      filtros[:external_reference].nil? || c[:external_reference] == filtros[:external_reference]
    end
  end

  def cancel(id)
    rec = @items[id]
    rec[:status] = "cancelada"
    rec
  end
end

RSpec.describe OrdexWhatsApp::Client do
  let(:meta_ok) do
    {
      "messaging_product" => "whatsapp",
      "contacts" => [{ "input" => "5511999999999", "wa_id" => "5511999999999" }],
      "messages" => [{ "id" => "wamid.TEST" }]
    }
  end

  def stub_graph(status: 200, body: meta_ok)
    stub_request(:post, "https://graph.facebook.com/v21.0/555/messages")
      .to_return(status: status, body: JSON.dump(body), headers: { "Content-Type" => "application/json" })
  end

  it "rejeita configuracao incompleta" do
    expect { described_class.new }.to raise_error(OrdexWhatsApp::ConfigurationError, /access_token/)
  end

  it "bloqueia envio sem add-on habilitado" do
    stub_graph
    client = described_class.new(
      access_token: "token",
      phone_number_id: "555",
      tenant_id: "acme",
      entitlement: OrdexWhatsApp::StaticEntitlementChecker.new(false, message: "addon off")
    )
    expect { client.send_text(to: "5511", body: "oi") }.to raise_error(OrdexWhatsApp::EntitlementError)
  end

  it "envia template utility, marca categoria e mede uso" do
    stub = stub_graph
    client = described_class.new(
      access_token: "token",
      phone_number_id: "555",
      tenant_id: "acme",
      entitlement: OrdexWhatsApp::StaticEntitlementChecker.new(true),
      clock: -> { Time.utc(2026, 9, 16, 15, 0, 0) }
    )
    result = client.send_template(
      to: "5511999999999",
      category: "utility",
      template: { name: "pix_recebido", language: "pt_BR" }
    )
    expect(result[:category]).to eq("utility")
    expect(result[:meta][:messages][0][:id]).to eq("wamid.TEST")
    expect(result[:metering][:used]).to eq(1)
    expect(result[:cobranca_plan][:total_quantity]).to eq(1)
    expect(stub).to have_been_requested
    expect(WebMock).to have_requested(:post, "https://graph.facebook.com/v21.0/555/messages").with { |req|
      payload = JSON.parse(req.body)
      payload["type"] == "template" &&
        payload["template"]["name"] == "pix_recebido" &&
        JSON.parse(payload["biz_opaque_callback_data"])["category"] == "utility" &&
        req.headers["Authorization"] == "Bearer token"
    }
  end

  it "faz rollback da cota se a Graph API falhar" do
    stub_graph(status: 400, body: { "error" => { "message" => "invalid to" } })
    store = OrdexWhatsApp::InMemoryUsageStore.new
    client = described_class.new(
      access_token: "token",
      phone_number_id: "555",
      tenant_id: "acme",
      entitlement: OrdexWhatsApp::StaticEntitlementChecker.new(true),
      usage_store: store
    )
    expect { client.send_text(to: "1", body: "oi") }.to raise_error(OrdexWhatsApp::ApiError)
    snap = store.get("acme", client.metering.period)
    expect(snap[:counts]["service"]).to eq(0)
  end

  it "modo hard impede o POST a Meta" do
    stub = stub_graph
    client = described_class.new(
      access_token: "token",
      phone_number_id: "555",
      tenant_id: "acme",
      entitlement: OrdexWhatsApp::StaticEntitlementChecker.new(true),
      quota_mode: "hard",
      plan: { categories: { "service" => { monthly_quota: 1 } } }
    )
    client.send_text(to: "5511", body: "1")
    expect { client.send_text(to: "5511", body: "2") }.to raise_error(OrdexWhatsApp::QuotaExceededError)
    expect(stub).to have_been_requested.once
  end
end

RSpec.describe OrdexWhatsApp::CobrancaQuantityController do
  it "cria cobrancas do plano e cancela extras quando o uso cai" do
    agenda = MemoryAgenda.new
    controller = described_class.new(agenda, { "documento" => "12345678901", "nome" => "Empresa SaaS" }, vencimento_day: 10)
    full = OrdexWhatsApp::CobrancaQuantity.compute(
      { "auth" => 20_000, "utility" => 0, "service" => 0, "marketing" => 0 },
      OrdexWhatsApp::Plan.cenario_base,
      period: "2026-09", tenant_id: "acme", strategy: "by-sessions", sessions_per_cobranca: 10_000
    )
    expect(full[:total_quantity]).to eq(2)
    first = controller.sync(full)
    expect(first[:created]).to eq(2)
    expect(first[:items][0][:cobranca][:vencimento]).to eq("2026-09-10")

    reduced = OrdexWhatsApp::CobrancaQuantity.compute(
      { "auth" => 10_000, "utility" => 0, "service" => 0, "marketing" => 0 },
      OrdexWhatsApp::Plan.cenario_base,
      period: "2026-09", tenant_id: "acme", strategy: "by-sessions", sessions_per_cobranca: 10_000
    )
    second = controller.sync(reduced)
    expect(second[:kept]).to eq(1)
    expect(second[:cancelled]).to eq(1)
    expect(second[:created]).to eq(0)
    pending = agenda.items.values.select { |c| c[:status] == "pendente" }
    expect(pending.length).to eq(1)
    expect(pending[0][:external_reference]).to eq("wa:acme:2026-09:auth:1")
  end
end
