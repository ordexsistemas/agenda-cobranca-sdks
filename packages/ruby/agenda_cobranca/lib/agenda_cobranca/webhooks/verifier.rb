# frozen_string_literal: true

module AgendaCobranca
  module Webhooks
    class Verifier
      DEFAULT_METHOD = "POST"
      DEFAULT_PATH = "/v1/webhooks"

      def initialize(client_secret)
        @signer = Security::Signer.new(client_secret)
      end

      def verify(payload:, headers: {}, method: DEFAULT_METHOD, path: DEFAULT_PATH)
        timestamp = valor_header(headers, "X-Timestamp")
        nonce = valor_header(headers, "X-Nonce")
        signature = valor_header(headers, "X-Signature")

        raise SignatureError, "Headers de webhook incompletos" if timestamp.nil? || nonce.nil? || signature.nil?

        @signer.valid_signature?(
          method: method,
          path: path,
          timestamp: timestamp,
          nonce: nonce,
          body: payload,
          signature: signature
        )
      end

      def verify!(payload:, headers: {}, method: DEFAULT_METHOD, path: DEFAULT_PATH)
        return true if verify(payload: payload, headers: headers, method: method, path: path)

        raise SignatureError, "Assinatura de webhook invalida"
      end

      private

      def valor_header(headers, nome)
        return nil if headers.nil?

        headers[nome] ||
          headers[nome.to_s] ||
          headers[nome.downcase] ||
          headers[nome.downcase.tr("-", "_")] ||
          headers[nome.upcase]
      end
    end

    module_function

    def verify(payload, headers, client_secret:, method: Verifier::DEFAULT_METHOD, path: Verifier::DEFAULT_PATH)
      Verifier.new(client_secret).verify(payload: payload, headers: headers, method: method, path: path)
    end
  end
end
