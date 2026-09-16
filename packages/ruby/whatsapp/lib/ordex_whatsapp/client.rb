# frozen_string_literal: true

module OrdexWhatsApp
  class Client
    attr_reader :options, :metering, :graph

    def initialize(options = {})
      @options = stringify(options)
      missing = []
      missing << "access_token" if blank?(@options["access_token"])
      missing << "phone_number_id" if blank?(@options["phone_number_id"])
      missing << "tenant_id" if blank?(@options["tenant_id"])
      raise ConfigurationError, "Configuracao incompleta: #{missing.join(', ')}" unless missing.empty?

      @plan = Plan.merge(@options["plan"], @options["quota_mode"])
      store = @options["usage_store"] || InMemoryUsageStore.new
      clock = @options["clock"] || -> { Time.now }
      time_zone = @options["time_zone"] || "America/Sao_Paulo"
      @metering = MeteringService.new(@plan, store, clock: clock, time_zone: time_zone)
      @graph = GraphClient.new(
        access_token: @options["access_token"],
        phone_number_id: @options["phone_number_id"],
        waba_id: @options["waba_id"],
        graph_version: @options["graph_version"],
        graph_base_url: @options["graph_base_url"],
        connection: @options["graph_connection"]
      )
      @entitlement = resolve_entitlement
      @conversion = {
        strategy: @options["quantity_strategy"] || "per-category",
        sessions_per_cobranca: @options["sessions_per_cobranca"],
        valor_centavos_por_cobranca: @options["valor_centavos_por_cobranca"]
      }
      agenda = resolve_agenda
      pagador = @options["agenda_pagador"]
      if agenda && pagador
        @agenda_controller = CobrancaQuantityController.new(
          agenda,
          stringify(pagador),
          vencimento_day: @options["agenda_vencimento_day"]
        )
      end
    end

    def plan
      @plan
    end

    def usage(period = nil)
      @metering.snapshot(@options["tenant_id"], period)
    end

    def cobranca_plan(counts, period = nil)
      CobrancaQuantity.compute(stringify_counts(counts), @plan, @conversion.merge(
        period: period || @metering.period,
        tenant_id: @options["tenant_id"]
      ))
    end

    def preview_cobranca_plan(period = nil)
      snap = usage(period)
      cobranca_plan(snap[:counts], snap[:period])
    end

    def sync_cobrancas(period = nil)
      unless @agenda_controller
        raise ConfigurationError,
              "Agenda de Cobrancas nao configurada: informe agenda_client (ou ordex_api_key) e agenda_pagador"
      end

      @agenda_controller.sync(preview_cobranca_plan(period))
    end

    def send_template(to:, category:, template:, callback_data: nil)
      send_message(
        to: to,
        category: category,
        type: "template",
        template: template,
        callback_data: callback_data
      )
    end

    def send_text(to:, body:, category: "service", preview_url: false, callback_data: nil)
      send_message(
        to: to,
        category: category,
        type: "text",
        text: { body: body, preview_url: preview_url },
        callback_data: callback_data
      )
    end

    def send_media(to:, category:, media:, callback_data: nil)
      send_message(
        to: to,
        category: category,
        type: media[:type] || media["type"],
        media: media,
        callback_data: callback_data
      )
    end

    def send_message(input)
      input = stringify(input)
      OrdexPayEntitlementChecker.assert!(
        @entitlement,
        @options["tenant_id"],
        skip: truthy?(@options["skip_entitlement_check"])
      )
      metering = @metering.consume(@options["tenant_id"], input["category"], 1)
      begin
        meta = @graph.send_message(input)
      rescue StandardError
        @metering.rollback(@options["tenant_id"], input["category"], 1)
        raise
      end
      plan = if truthy?(@options["sync_cobrancas_on_send"]) && @agenda_controller
               sync_cobrancas(metering[:period])[:plan]
             else
               snap = usage(metering[:period])
               cobranca_plan(snap[:counts], snap[:period])
             end
      { category: input["category"], to: input["to"], meta: meta, metering: metering, cobranca_plan: plan }
    end

    private

    def resolve_entitlement
      return @options["entitlement"] if @options["entitlement"]
      return StaticEntitlementChecker.new(true, plan: "demo") if truthy?(@options["skip_entitlement_check"])
      if @options["ordex_api_key"].to_s.strip != ""
        return OrdexPayEntitlementChecker.new(
          @options["ordex_api_key"],
          base_url: @options["ordex_base_url"],
          connection: @options["ordex_connection"]
        )
      end

      nil
    end

    def resolve_agenda
      return @options["agenda_client"] if @options["agenda_client"]
      if @options["ordex_api_key"].to_s.strip != ""
        return AgendaSdkAdapter.from_api_key(@options["ordex_api_key"], base_url: @options["ordex_base_url"])
      end

      nil
    end

    def stringify(hash)
      return {} if hash.nil?
      return hash.transform_keys(&:to_s) if hash.is_a?(Hash)

      {}
    end

    def stringify_counts(counts)
      return Plan.empty_counts if counts.nil?

      Plan.empty_counts.merge(stringify(counts))
    end

    def blank?(valor)
      valor.nil? || valor.to_s.strip.empty?
    end

    def truthy?(value)
      value == true || value == "true" || value == 1 || value == "1"
    end
  end
end
