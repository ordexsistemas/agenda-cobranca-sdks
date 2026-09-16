# frozen_string_literal: true

require "spec_helper"

RSpec.describe OrdexWhatsApp::CobrancaQuantity do
  let(:opts) { { period: "2026-09", tenant_id: "acme", strategy: "per-category" } }

  it "per-category: 1 cobranca por categoria faturavel e custos da tabela" do
    plan = described_class.compute(OrdexWhatsApp::CENARIO_BASE_VOLUME, OrdexWhatsApp::Plan.cenario_base, opts)
    expect(plan[:total_quantity]).to eq(4)
    expect(plan[:total_cost_usd]).to eq(4105)
    expect(plan[:total_cost_brl_centavos]).to eq((4105 * OrdexWhatsApp::DEFAULT_USD_TO_BRL * 100).round)

    by_cat = plan[:categories].to_h { |c| [c[:category], c] }
    expect(by_cat["auth"][:cost_usd]).to eq(1_260)
    expect(by_cat["auth"][:cost_brl_centavos]).to eq(693_000)
    expect(by_cat["utility"][:cost_usd]).to eq(2_100)
    expect(by_cat["utility"][:cost_brl_centavos]).to eq(1_155_000)
    expect(by_cat["service"][:used]).to eq(5_000)
    expect(by_cat["service"][:free]).to eq(1_000)
    expect(by_cat["service"][:billable]).to eq(4_000)
    expect(by_cat["service"][:cost_usd]).to eq(120)
    expect(by_cat["marketing"][:cost_usd]).to eq(625)
    expect(by_cat["marketing"][:cost_brl_centavos]).to eq(343_750)
    expect(by_cat["marketing"][:valor_centavos_each]).to eq([343_750])
  end

  it "service so com franquia nao gera cobranca" do
    plan = described_class.compute(
      { "auth" => 0, "utility" => 0, "service" => 1_000, "marketing" => 0 },
      OrdexWhatsApp::Plan.cenario_base,
      opts
    )
    expect(plan[:total_quantity]).to eq(0)
    expect(plan[:total_cost_usd]).to eq(0)
  end

  it "by-sessions converte uso em quantidade" do
    plan = described_class.compute(
      OrdexWhatsApp::CENARIO_BASE_VOLUME,
      OrdexWhatsApp::Plan.cenario_base,
      opts.merge(strategy: "by-sessions", sessions_per_cobranca: 10_000)
    )
    by_cat = plan[:categories].to_h { |c| [c[:category], c[:quantity]] }
    expect(by_cat).to eq("auth" => 4, "utility" => 6, "service" => 1, "marketing" => 1)
    expect(plan[:total_quantity]).to eq(12)
  end

  it "by-value parte o custo BRL" do
    plan = described_class.compute(
      OrdexWhatsApp::CENARIO_BASE_VOLUME,
      OrdexWhatsApp::Plan.cenario_base,
      opts.merge(strategy: "by-value", valor_centavos_por_cobranca: 100_000)
    )
    by_cat = plan[:categories].to_h { |c| [c[:category], c] }
    expect(by_cat["auth"][:quantity]).to eq(7)
    expect(by_cat["utility"][:quantity]).to eq(12)
    expect(by_cat["service"][:quantity]).to eq(1)
    expect(by_cat["marketing"][:quantity]).to eq(4)
    expect(by_cat["auth"][:valor_centavos_each].sum).to eq(693_000)
  end

  it "split centavos e referencia deterministica" do
    expect(described_class.split_centavos(100, 3)).to eq([34, 33, 33])
    ref = described_class.external_reference("acme", "2026-09", "utility", 2)
    expect(ref).to eq("wa:acme:2026-09:utility:2")
    expect(described_class.parse_external_reference(ref)).to eq(
      tenant_id: "acme", period: "2026-09", category: "utility", index: 2
    )
    expect(described_class.parse_external_reference("pedido-1001")).to be_nil
  end
end
