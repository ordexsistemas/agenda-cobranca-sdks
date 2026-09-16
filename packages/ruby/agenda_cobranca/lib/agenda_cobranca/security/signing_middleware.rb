# frozen_string_literal: true

require "faraday"

module AgendaCobranca
  module Security
    class SigningMiddleware < Faraday::Middleware
      def initialize(app, signer:, client_id:, api_key:)
        super(app)
        @signer = signer
        @client_id = client_id
        @api_key = api_key
      end

      def call(env)
        assinatura = @signer.sign(
          method: env.method,
          path: caminho_requisicao(env),
          body: env.request_body
        )

        env.request_headers["X-Client-Id"] = @client_id
        env.request_headers["X-Timestamp"] = assinatura[:timestamp]
        env.request_headers["X-Nonce"] = assinatura[:nonce]
        env.request_headers["X-Signature"] = assinatura[:signature]
        env.request_headers["X-Api-Key"] = @api_key

        @app.call(env)
      end

      private

      def caminho_requisicao(env)
        env.url.path
      end
    end
  end
end
