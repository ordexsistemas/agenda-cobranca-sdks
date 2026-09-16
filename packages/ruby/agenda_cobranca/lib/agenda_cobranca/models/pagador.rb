# frozen_string_literal: true

module AgendaCobranca
  module Models
    class Pagador
      attr_reader :documento, :nome, :email

      def initialize(documento:, nome:, email: nil)
        @documento = documento
        @nome = nome
        @email = email
      end

      def self.from_hash(dados)
        return nil if dados.nil?

        attrs = symbolize(dados)
        new(
          documento: attrs[:documento],
          nome: attrs[:nome],
          email: attrs[:email]
        )
      end

      def to_h
        {
          documento: documento,
          nome: nome,
          email: email
        }.compact
      end

      def self.symbolize(dados)
        dados.each_with_object({}) { |(chave, valor), acc| acc[chave.to_sym] = valor }
      end
    end
  end
end
