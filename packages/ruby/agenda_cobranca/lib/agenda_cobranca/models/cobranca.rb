# frozen_string_literal: true

module AgendaCobranca
  module Models
    class Cobranca
      STATUSES = %w[pendente paga vencida cancelada].freeze

      attr_reader :id,
                  :external_reference,
                  :valor_centavos,
                  :vencimento,
                  :status,
                  :pagador,
                  :juros,
                  :multa,
                  :raw

      def initialize(attrs = {})
        @id = attrs[:id]
        @external_reference = attrs[:external_reference]
        @valor_centavos = attrs[:valor_centavos]
        @vencimento = attrs[:vencimento]
        @status = attrs[:status]
        @pagador = attrs[:pagador]
        @juros = attrs[:juros]
        @multa = attrs[:multa]
        @raw = attrs[:raw]
      end

      def self.from_response(payload)
        dados = unwrap(payload)
        attrs = symbolize(dados)

        new(
          id: attrs[:id],
          external_reference: attrs[:external_reference],
          valor_centavos: attrs[:valor_centavos],
          vencimento: attrs[:vencimento],
          status: attrs[:status],
          pagador: Pagador.from_hash(attrs[:pagador]),
          juros: symbolize_opcional(attrs[:juros]),
          multa: symbolize_opcional(attrs[:multa]),
          raw: dados
        )
      end

      def to_h
        {
          id: id,
          external_reference: external_reference,
          valor_centavos: valor_centavos,
          vencimento: vencimento,
          status: status,
          pagador: pagador&.to_h,
          juros: juros,
          multa: multa
        }.compact
      end

      def self.unwrap(payload)
        return {} if payload.nil?
        return payload unless payload.is_a?(Hash)

        dados = payload["data"] || payload[:data] || payload
        dados.is_a?(Hash) ? dados : payload
      end

      def self.symbolize(dados)
        return {} unless dados.is_a?(Hash)

        dados.each_with_object({}) { |(chave, valor), acc| acc[chave.to_sym] = valor }
      end

      def self.symbolize_opcional(dados)
        return nil if dados.nil?
        return dados unless dados.is_a?(Hash)

        symbolize(dados)
      end
    end
  end
end
