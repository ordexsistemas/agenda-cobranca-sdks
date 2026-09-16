# frozen_string_literal: true

module AgendaCobranca
  module Resources
    class License
      def initialize(client)
        @client = client
      end

      def verify
        payload = @client.request(
          :post,
          "licenses/verify",
          body: {
            "client_id" => @client.configuration.client_id
          }
        )
        normalizar(payload)
      end

      private

      def normalizar(payload)
        dados = payload.is_a?(Hash) ? (payload["data"] || payload) : {}
        {
          valid: verdade?(dados["valid"] || dados[:valid] || payload.is_a?(Hash) && payload["success"]),
          message: dados["message"] || dados[:message] || (payload.is_a?(Hash) ? payload["message"] : nil),
          raw: payload
        }
      end

      def verdade?(valor)
        valor == true || valor.to_s == "true"
      end
    end
  end
end
