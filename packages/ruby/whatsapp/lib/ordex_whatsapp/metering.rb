# frozen_string_literal: true

require "thread"

module OrdexWhatsApp
  module Metering
    module_function

    def billable_units(used, free_allowance)
      [0, used.to_i - [0, free_allowance.to_i].max].max
    end

    def billable_delta_for_increment(used_before, delta, free_allowance)
      if delta <= 0
        before = billable_units(used_before, free_allowance)
        after = billable_units(used_before + delta, free_allowance)
        return { billable_delta: after - before, free_delta: 0 }
      end
      before = billable_units(used_before, free_allowance)
      after = billable_units(used_before + delta, free_allowance)
      billable = after - before
      { billable_delta: billable, free_delta: delta - billable }
    end

    def evaluate_usage(used, quota, quota_mode, period, category, delta)
      remaining = [0, quota[:monthly_quota] - used].max
      overage_delta = [0, used - quota[:monthly_quota]].max
      used_before = used - delta
      parts = billable_delta_for_increment(used_before, delta, quota[:free_allowance])
      overage = used > quota[:monthly_quota]
      allowed = quota_mode.to_s == "soft" || used <= quota[:monthly_quota]
      {
        category: category,
        period: period,
        used: used,
        quota: quota[:monthly_quota],
        remaining: remaining,
        billable_delta: parts[:billable_delta],
        free_delta: parts[:free_delta],
        overage: overage,
        overage_delta: overage_delta,
        allowed: allowed,
        quota_mode: quota_mode
      }
    end
  end

  class InMemoryUsageStore
    def initialize(initial = [])
      @data = {}
      @mutex = Mutex.new
      initial.each { |snap| @data[key_of(snap[:tenant_id], snap[:period])] = clone_snap(snap) }
    end

    def get(tenant_id, period)
      @mutex.synchronize do
        found = @data[key_of(tenant_id, period)]
        return clone_snap(found) if found

        { tenant_id: tenant_id, period: period, counts: Plan.empty_counts }
      end
    end

    def increment(tenant_id, period, category, delta)
      @mutex.synchronize do
        current = @data[key_of(tenant_id, period)] || { tenant_id: tenant_id, period: period, counts: Plan.empty_counts }
        counts = current[:counts].dup
        counts[category.to_s] = [0, counts[category.to_s].to_i + delta].max
        current = { tenant_id: tenant_id, period: period, counts: counts }
        @data[key_of(tenant_id, period)] = current
        clone_snap(current)
      end
    end

    private

    def key_of(tenant_id, period)
      "#{tenant_id}:#{period}"
    end

    def clone_snap(snap)
      { tenant_id: snap[:tenant_id], period: snap[:period], counts: snap[:counts].dup }
    end
  end

  class MeteringService
    def initialize(plan, store, clock: nil, time_zone: "America/Sao_Paulo")
      @plan = plan
      @store = store
      @clock = clock || -> { Time.now }
      @time_zone = time_zone
    end

    def period(at = nil)
      Plan.billing_period(at || @clock.call, @time_zone)
    end

    def snapshot(tenant_id, period = nil)
      @store.get(tenant_id, period || self.period)
    end

    def consume(tenant_id, category, delta = 1)
      raise ArgumentError, "delta de consumo deve ser positivo" if delta <= 0

      period = self.period
      quota = Plan.quota_for(@plan, category)
      snap = @store.increment(tenant_id, period, category, delta)
      used = snap[:counts][category.to_s]
      decision = Metering.evaluate_usage(used, quota, @plan[:quota_mode], period, category.to_s, delta)
      unless decision[:allowed]
        @store.increment(tenant_id, period, category, -delta)
        raise QuotaExceededError.new(
          "Cota #{category} excedida no periodo #{period} (#{used}/#{quota[:monthly_quota]}, modo hard)",
          category: category.to_s, used: used, quota: quota[:monthly_quota], period: period
        )
      end
      decision
    end

    def rollback(tenant_id, category, delta = 1)
      @store.increment(tenant_id, period, category, -delta.abs)
    end
  end
end
