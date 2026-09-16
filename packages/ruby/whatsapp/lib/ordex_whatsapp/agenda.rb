# frozen_string_literal: true

require "agenda_cobranca"

module OrdexWhatsApp
  # Wraps AgendaCobranca::Client so HMAC/signing stay in the Agenda gem.
  class AgendaSdkAdapter
    def initialize(client)
      @client = client
    end

    def self.from_api_key(api_key, base_url: nil)
      config = AgendaCobranca::Configuration.new
      config.api_key = api_key
      config.base_url = base_url if base_url && !base_url.to_s.strip.empty?
      new(AgendaCobranca::Client.new(config))
    end

    def create(input)
      cob = @client.cobrancas.create(
        external_reference: input[:external_reference],
        valor_centavos: input[:valor_centavos],
        vencimento: input[:vencimento],
        pagador: input[:pagador],
        idempotency_key: input[:idempotency_key]
      )
      map(cob)
    end

    def list(filtros = {})
      result = @client.cobrancas.list(
        external_reference: filtros[:external_reference],
        per_page: filtros[:per_page] || 20
      )
      Array(result[:data]).map { |cob| map(cob) }
    end

    def cancel(id)
      map(@client.cobrancas.cancel(id))
    end

    private

    def map(cob)
      return cob if cob.is_a?(Hash)

      {
        id: cob.id,
        external_reference: cob.external_reference,
        valor_centavos: cob.valor_centavos,
        vencimento: cob.vencimento,
        status: cob.status,
        pagador: cob.pagador&.to_h
      }
    end
  end

  class CobrancaQuantityController
    def initialize(agenda, pagador, vencimento_day: nil)
      @agenda = agenda
      @pagador = pagador
      @vencimento_day = vencimento_day
    end

    def sync(plan)
      items = []
      plan[:categories].each do |category_plan|
        vencimento = Plan.vencimento_for_period(plan[:period], @vencimento_day)
        (1..category_plan[:quantity]).each do |index|
          ref = CobrancaQuantity.external_reference(plan[:tenant_id], plan[:period], category_plan[:category], index)
          existing = find_by_ref(ref)
          if existing && !cancelled?(existing)
            items << item(category_plan[:category], index, ref, "kept", existing)
            next
          end
          valor = category_plan[:valor_centavos_each][index - 1] || 0
          created = @agenda.create(
            external_reference: ref,
            valor_centavos: valor,
            vencimento: vencimento,
            pagador: @pagador,
            idempotency_key: ref
          )
          items << item(category_plan[:category], index, ref, "created", created)
        end
        cancel_extras(plan, category_plan[:category], category_plan[:quantity], items)
      end
      {
        period: plan[:period],
        tenant_id: plan[:tenant_id],
        plan: plan,
        items: items,
        created: items.count { |i| i[:action] == "created" },
        cancelled: items.count { |i| i[:action] == "cancelled" },
        kept: items.count { |i| i[:action] == "kept" }
      }
    end

    private

    def find_by_ref(ref)
      listed = @agenda.list(external_reference: ref, per_page: 20)
      listed.find { |c| c[:external_reference] == ref && !cancelled?(c) }
    end

    def cancel_extras(plan, category, keep, items)
      ((keep + 1)..(keep + 20)).each do |index|
        ref = CobrancaQuantity.external_reference(plan[:tenant_id], plan[:period], category, index)
        existing = find_by_ref(ref)
        break unless existing

        @agenda.cancel(existing[:id])
        items << item(category, index, ref, "cancelled", existing)
      end
    end

    def cancelled?(cobranca)
      status = cobranca[:status].to_s.downcase
      %w[cancelada cancelled canceled].include?(status)
    end

    def item(category, index, ref, action, cobranca)
      { category: category, index: index, external_reference: ref, action: action, cobranca: cobranca }
    end
  end
end
