# frozen_string_literal: true

require "spec_helper"

RSpec.describe OrdexWhatsApp::Metering do
  it "nao fatura os primeiros 1000 de service" do
    expect(described_class.billable_units(0, 1_000)).to eq(0)
    expect(described_class.billable_units(1_000, 1_000)).to eq(0)
    expect(described_class.billable_units(1_001, 1_000)).to eq(1)
    expect(described_class.billable_units(5_000, 1_000)).to eq(4_000)
  end

  it "reparte delta que atravessa a franquia" do
    expect(described_class.billable_delta_for_increment(950, 100, 1_000)).to eq(
      billable_delta: 50, free_delta: 50
    )
    expect(described_class.billable_delta_for_increment(0, 500, 1_000)).to eq(
      billable_delta: 0, free_delta: 500
    )
  end
end

RSpec.describe OrdexWhatsApp::MeteringService do
  let(:clock) { -> { Time.utc(2026, 9, 16, 12, 0, 0) } }

  it "acumula por categoria e periodo YYYY-MM" do
    metering = described_class.new(OrdexWhatsApp::Plan.cenario_base, OrdexWhatsApp::InMemoryUsageStore.new, clock: clock)
    d1 = metering.consume("tenant-a", "auth", 3)
    d2 = metering.consume("tenant-a", "utility", 1)
    expect(d1[:period]).to eq("2026-09")
    expect(d1[:used]).to eq(3)
    expect(d1[:billable_delta]).to eq(3)
    expect(d2[:category]).to eq("utility")
    snap = metering.snapshot("tenant-a")
    expect(snap[:counts]).to eq("auth" => 3, "utility" => 1, "service" => 0, "marketing" => 0)
  end

  it "modo hard bloqueia e nao persiste o excedente" do
    plan = OrdexWhatsApp::Plan.merge(
      { quota_mode: "hard", categories: { "auth" => { monthly_quota: 2, unit_cost_usd: 0.0315, free_allowance: 0 } } },
      "hard"
    )
    metering = described_class.new(plan, OrdexWhatsApp::InMemoryUsageStore.new, clock: clock)
    metering.consume("t", "auth", 2)
    expect { metering.consume("t", "auth", 1) }.to raise_error(OrdexWhatsApp::QuotaExceededError)
    expect(metering.snapshot("t")[:counts]["auth"]).to eq(2)
  end

  it "modo soft permite envio e marca overage" do
    plan = OrdexWhatsApp::Plan.merge(
      { quota_mode: "soft", categories: { "marketing" => { monthly_quota: 2, unit_cost_usd: 0.0625, free_allowance: 0 } } },
      "soft"
    )
    metering = described_class.new(plan, OrdexWhatsApp::InMemoryUsageStore.new, clock: clock)
    metering.consume("t", "marketing", 2)
    over = metering.consume("t", "marketing", 1)
    expect(over[:allowed]).to be true
    expect(over[:overage]).to be true
    expect(over[:overage_delta]).to eq(1)
    expect(over[:used]).to eq(3)
  end

  it "service so comeca a faturar apos 1000" do
    metering = described_class.new(OrdexWhatsApp::Plan.cenario_base, OrdexWhatsApp::InMemoryUsageStore.new, clock: clock)
    first = metering.consume("t", "service", 1_000)
    expect(first[:billable_delta]).to eq(0)
    expect(first[:free_delta]).to eq(1_000)
    next_d = metering.consume("t", "service", 5)
    expect(next_d[:billable_delta]).to eq(5)
    expect(next_d[:used]).to eq(1_005)
  end
end
