# frozen_string_literal: true

require "date"

module OrdexWhatsApp
  CATEGORIES = %w[auth utility service marketing].freeze
  DEFAULT_ORDEX_BASE_URL = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
  DEFAULT_GRAPH_BASE_URL = "https://graph.facebook.com"
  DEFAULT_GRAPH_VERSION = "v21.0"
  DEFAULT_USD_TO_BRL = 5.5
  SERVICE_FREE_ALLOWANCE = 1_000
  DEFAULT_SESSIONS_PER_COBRANCA = 10_000
  DEFAULT_VALOR_CENTAVOS_POR_COBRANCA = 100_000

  CENARIO_BASE_CATEGORIES = {
    "auth" => { monthly_quota: 40_000, unit_cost_usd: 0.0315, free_allowance: 0 },
    "utility" => { monthly_quota: 60_000, unit_cost_usd: 0.0350, free_allowance: 0 },
    "service" => { monthly_quota: 5_000, unit_cost_usd: 0.0300, free_allowance: SERVICE_FREE_ALLOWANCE },
    "marketing" => { monthly_quota: 10_000, unit_cost_usd: 0.0625, free_allowance: 0 }
  }.freeze

  CENARIO_BASE_VOLUME = {
    "auth" => 40_000,
    "utility" => 60_000,
    "service" => 5_000,
    "marketing" => 10_000
  }.freeze

  module Plan
    module_function

    def cenario_base(quota_mode: "hard")
      {
        categories: deep_dup_categories,
        usd_to_brl: DEFAULT_USD_TO_BRL,
        quota_mode: quota_mode
      }
    end

    def merge(overrides = nil, quota_mode = nil)
      categories = deep_dup_categories
      usd = DEFAULT_USD_TO_BRL
      mode = "hard"
      if overrides
        overrides = stringify(overrides)
        usd = overrides["usd_to_brl"] if overrides.key?("usd_to_brl")
        mode = overrides["quota_mode"] if overrides["quota_mode"]
        (overrides["categories"] || {}).each do |key, patch|
          next unless categories[key.to_s]

          categories[key.to_s] = categories[key.to_s].merge(stringify(patch).transform_keys(&:to_sym))
        end
      end
      mode = quota_mode if quota_mode
      { categories: categories, usd_to_brl: usd, quota_mode: mode || "hard" }
    end

    def quota_for(plan, category)
      plan[:categories][category.to_s] || plan["categories"][category.to_s]
    end

    def billing_period(time = Time.now, time_zone = "America/Sao_Paulo")
      local = time.getlocal(tz_offset(time_zone, time))
      format("%04d-%02d", local.year, local.month)
    rescue ArgumentError
      format("%04d-%02d", time.year, time.month)
    end

    def vencimento_for_period(period, day = nil)
      year, month = period.split("-").map(&:to_i)
      raise ArgumentError, "periodo invalido: #{period}" if year.nil? || month.nil? || year.zero?

      last = Date.new(year, month, -1).day
      chosen = day.nil? || day <= 0 ? last : [day, last].min
      format("%s-%02d", period, chosen)
    end

    def empty_counts
      { "auth" => 0, "utility" => 0, "service" => 0, "marketing" => 0 }
    end

    def deep_dup_categories
      CENARIO_BASE_CATEGORIES.transform_values(&:dup)
    end

    def stringify(hash)
      return {} if hash.nil?

      hash.each_with_object({}) do |(k, v), acc|
        acc[k.to_s] = v.is_a?(Hash) ? stringify(v) : v
      end
    end

    def tz_offset(name, time)
      # America/Sao_Paulo: UTC-3 year-round since 2019 (no DST).
      return -3 * 3600 if name.to_s == "America/Sao_Paulo"

      0
    end
  end
end
