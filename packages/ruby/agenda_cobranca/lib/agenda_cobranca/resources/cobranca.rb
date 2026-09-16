# frozen_string_literal: true

require "securerandom"

module AgendaCobranca
  module Resources
    class Cobranca
      def initialize(client)
        @client = client
      end

      def create(params)
        parametros = stringify_keys(params)
        idempotency_key = parametros.delete("idempotency_key") || SecureRandom.uuid
        corpo = corpo_criacao(parametros)

        payload = @client.request(
          :post,
          "cobrancas",
          body: corpo,
          headers: { "Idempotency-Key" => idempotency_key }
        )
        Models::Cobranca.from_response(payload)
      end

      def find(id)
        raise ArgumentError, "id e obrigatorio" if id.nil? || id.to_s.strip.empty?

        payload = @client.request(:get, "cobrancas/#{id}")
        Models::Cobranca.from_response(payload)
      end

      def list(filtros = {})
        query = query_listagem(filtros)
        payload = @client.request(:get, "cobrancas", query: query)
        montar_listagem(payload)
      end

      def cancel(id)
        raise ArgumentError, "id e obrigatorio" if id.nil? || id.to_s.strip.empty?

        payload = @client.request(:post, "cobrancas/#{id}/cancel")
        Models::Cobranca.from_response(payload)
      end

      private

      def corpo_criacao(parametros)
        pagador = parametros["pagador"] || {}
        {
          "external_reference" => parametros["external_reference"],
          "valor_centavos" => parametros["valor_centavos"],
          "vencimento" => parametros["vencimento"],
          "pagador" => {
            "documento" => valor_aninhado(pagador, "documento"),
            "nome" => valor_aninhado(pagador, "nome"),
            "email" => valor_aninhado(pagador, "email")
          }.compact,
          "juros" => parametros["juros"],
          "multa" => parametros["multa"]
        }.compact
      end

      def query_listagem(filtros)
        parametros = stringify_keys(filtros)
        {
          "status" => parametros["status"],
          "external_reference" => parametros["external_reference"],
          "page" => parametros["page"],
          "per_page" => parametros["per_page"]
        }.compact
      end

      def montar_listagem(payload)
        itens = extrair_itens(payload)
        meta = extrair_meta(payload)

        {
          data: itens.map { |item| Models::Cobranca.from_response(item) },
          meta: meta
        }
      end

      def extrair_itens(payload)
        return [] if payload.nil?
        return payload if payload.is_a?(Array)
        return [] unless payload.is_a?(Hash)

        payload["data"] || payload["cobrancas"] || []
      end

      def extrair_meta(payload)
        return {} unless payload.is_a?(Hash)

        bruto = payload["meta"] || {}
        {
          page: bruto["page"] || payload["page"],
          per_page: bruto["per_page"] || payload["per_page"],
          total: bruto["total"] || payload["total"]
        }.compact
      end

      def stringify_keys(hash)
        return {} if hash.nil?

        hash.each_with_object({}) do |(chave, valor), acc|
          acc[chave.to_s] = valor.is_a?(Hash) ? stringify_keys(valor) : valor
        end
      end

      def valor_aninhado(hash, chave)
        return nil unless hash.is_a?(Hash)

        hash[chave] || hash[chave.to_sym]
      end
    end
  end
end
