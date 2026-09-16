# frozen_string_literal: true

require "digest"
require "openssl"
require "securerandom"

module AgendaCobranca
  module Security
    class Signer
      def initialize(client_secret, clock: -> { Time.now.to_i }, nonce_generator: -> { SecureRandom.uuid })
        raise ConfigurationError, "client_secret e obrigatorio" if client_secret.nil? || client_secret.to_s.empty?

        @client_secret = client_secret.to_s
        @clock = clock
        @nonce_generator = nonce_generator
      end

      def sign(method:, path:, body: "")
        timestamp = @clock.call.to_i.to_s
        nonce = @nonce_generator.call.to_s
        signature = signature_for(method: method, path: path, timestamp: timestamp, nonce: nonce, body: body)

        {
          timestamp: timestamp,
          nonce: nonce,
          signature: signature,
          body_hash: hash_corpo(body)
        }
      end

      def signature_for(method:, path:, timestamp:, nonce:, body: "")
        canonical = canonical_string(
          method: method,
          path: path,
          timestamp: timestamp,
          nonce: nonce,
          body: body
        )
        hmac_hex(canonical)
      end

      def canonical_string(method:, path:, timestamp:, nonce:, body: "")
        [
          method.to_s.upcase,
          path.to_s,
          timestamp.to_s,
          nonce.to_s,
          hash_corpo(body)
        ].join("\n")
      end

      def hash_corpo(body)
        Digest::SHA256.hexdigest(corpo_bytes(body))
      end

      def valid_signature?(method:, path:, timestamp:, nonce:, body:, signature:)
        esperado = signature_for(
          method: method,
          path: path,
          timestamp: timestamp,
          nonce: nonce,
          body: body
        )
        comparar_seguro(esperado, signature.to_s)
      end

      private

      def hmac_hex(canonical)
        OpenSSL::HMAC.hexdigest("SHA256", @client_secret, canonical)
      end

      def corpo_bytes(body)
        return "" if body.nil?

        body.is_a?(String) ? body : body.to_s
      end

      def comparar_seguro(esperado, recebido)
        return false if esperado.bytesize != recebido.bytesize

        OpenSSL.fixed_length_secure_compare(esperado, recebido)
      end
    end
  end
end
