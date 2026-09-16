# frozen_string_literal: true

require "faraday"

module AgendaCobranca
  module Security
    class SigningMiddleware < Faraday::Middleware
      def initialize(app, api_key:, signing_enabled: false, signer: nil, client_id: nil)
        super(app)
        @api_key = api_key
        @signing_enabled = signing_enabled
        @signer = signer
        @client_id = client_id
      end

      def call(env)
        # Ordex Pay external API: always send both api-key headers with the same value.
        env.request_headers["chave_api"] = @api_key
        env.request_headers["X-Api-Key"] = @api_key

        if @signing_enabled
          raise AgendaCobranca::ConfigurationError, "signer ausente com signing_enabled" if @signer.nil?

          assinatura = @signer.sign(
            method: env.method,
            path: caminho_requisicao(env),
            body: env.request_body
          )

          env.request_headers["X-Client-Id"] = @client_id.to_s
          env.request_headers["X-Timestamp"] = assinatura[:timestamp]
          env.request_headers["X-Nonce"] = assinatura[:nonce]
          env.request_headers["X-Signature"] = assinatura[:signature]
        end

        @app.call(env)
      end

      private

      def caminho_requisicao(env)
        env.url.path
      end
    end
  end
end
