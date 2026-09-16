# frozen_string_literal: true

module OrdexWhatsApp
  module CobrancaQuantity
    module_function

    def split_centavos(total, quantity)
      return [] if quantity <= 0
      return Array.new(quantity, 0) if total <= 0

      base = total / quantity
      remainder = total - (base * quantity)
      Array.new(quantity) { |i| base + (i < remainder ? 1 : 0) }
    end

    def cost_brl_centavos(billable, unit_cost_usd, usd_to_brl)
      (billable * unit_cost_usd * usd_to_brl * 100).round
    end

    def cost_usd(billable, unit_cost_usd)
      (billable * unit_cost_usd).round(4)
    end

    def compute(counts, plan, options)
      strategy = (options[:strategy] || "per-category").to_s
      categories = CATEGORIES.map do |category|
        quota = Plan.quota_for(plan, category)
        used = [0, (counts[category] || counts[category.to_sym]).to_i].max
        billable = Metering.billable_units(used, quota[:free_allowance])
        free = [used, [0, quota[:free_allowance]].max].min
        overage = [0, used - quota[:monthly_quota]].max
        usd = cost_usd(billable, quota[:unit_cost_usd])
        brl = cost_brl_centavos(billable, quota[:unit_cost_usd], plan[:usd_to_brl])
        quantity = quantity_for(strategy, category, billable, brl, options)
        {
          category: category,
          used: used,
          billable: billable,
          free: free,
          quota: quota[:monthly_quota],
          overage: overage,
          unit_cost_usd: quota[:unit_cost_usd],
          cost_usd: usd,
          cost_brl_centavos: brl,
          quantity: quantity,
          valor_centavos_each: split_centavos(brl, quantity)
        }
      end
      {
        period: options[:period],
        tenant_id: options[:tenant_id],
        categories: categories,
        total_quantity: categories.sum { |c| c[:quantity] },
        total_cost_usd: categories.sum { |c| c[:cost_usd] }.round(4),
        total_cost_brl_centavos: categories.sum { |c| c[:cost_brl_centavos] },
        strategy: strategy
      }
    end

    def external_reference(tenant_id, period, category, index)
      "wa:#{tenant_id}:#{period}:#{category}:#{index}"
    end

    def parse_external_reference(ref)
      match = /\Awa:([^:]+):(\d{4}-\d{2}):(auth|utility|service|marketing):(\d+)\z/.match(ref.to_s)
      return nil unless match

      { tenant_id: match[1], period: match[2], category: match[3], index: match[4].to_i }
    end

    def quantity_for(strategy, category, billable, cost_cents, options)
      if billable <= 0 && cost_cents <= 0
        return options[:include_zero_categories] ? 1 : 0
      end

      case strategy
      when "by-sessions"
        per = sessions_per(category, options)
        [1, (billable.to_f / per).ceil].max
      when "by-value"
        per = options[:valor_centavos_por_cobranca] || DEFAULT_VALOR_CENTAVOS_POR_COBRANCA
        return 1 if per <= 0

        [1, (cost_cents.to_f / per).ceil].max
      else
        1
      end
    end

    def sessions_per(category, options)
      raw = options[:sessions_per_cobranca]
      return raw if raw.is_a?(Numeric) && raw.positive?
      if raw.is_a?(Hash)
        n = raw[category] || raw[category.to_sym]
        return n if n && n.positive?
      end
      DEFAULT_SESSIONS_PER_COBRANCA
    end
  end
end
