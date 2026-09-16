# frozen_string_literal: true

require "faraday"
require "json"

module AgendaCobranca
  class Client
    attr_reader :configuration, :signer

    def initialize(configuration = nil)
      @configuration = configuration || AgendaCobranca.configuration.dup
      @configuration.validate!
      @signer = Security::Signer.new(@configuration.client_secret)
      @connection = montar_conexao

      verificar_licenca_se_solicitado
    end

    def cobrancas
      @cobrancas ||= Resources::Cobranca.new(self)
    end

    def licenses
      @licenses ||= Resources::License.new(self)
    end

    def request(method, path, body: nil, query: nil, headers: {})
      resposta = @connection.run_request(method.to_sym, path, body, headers) do |req|
        req.params.update(query) if query && !query.empty?
      end

      tratar_resposta(resposta)
    end

    private

    def montar_conexao
      signer = @signer
      client_id = @configuration.client_id
      api_key = @configuration.api_key

      Faraday.new(url: @configuration.base_url) do |faraday|
        faraday.request :json
        faraday.use Security::SigningMiddleware, signer: signer, client_id: client_id, api_key: api_key
        faraday.options.timeout = @configuration.timeout
        faraday.options.open_timeout = @configuration.open_timeout
        faraday.adapter Faraday.default_adapter
      end
    end

    def tratar_resposta(resposta)
      payload = decodificar(resposta.body)
      return payload if resposta.success?

      mensagem = extrair_mensagem(payload, resposta)
      erros = extrair_erros(payload)
      raise classe_erro(resposta.status).new(
        mensagem,
        status: resposta.status,
        body: payload,
        errors: erros
      )
    end

    def decodificar(corpo)
      return {} if corpo.nil? || corpo.to_s.strip.empty?

      JSON.parse(corpo)
    rescue JSON::ParserError
      { "raw" => corpo }
    end

    def extrair_mensagem(payload, resposta)
      if payload.is_a?(Hash)
        payload["message"] || payload["error"] || "Erro HTTP #{resposta.status}"
      else
        "Erro HTTP #{resposta.status}"
      end
    end

    def extrair_erros(payload)
      return nil unless payload.is_a?(Hash)

      payload["errors"] || payload["error"]
    end

    def classe_erro(status)
      case status
      when 401, 403 then AuthenticationError
      when 404 then NotFoundError
      when 422 then ValidationError
      when 429 then RateLimitError
      else ApiError
      end
    end

    def verificar_licenca_se_solicitado
      return unless @configuration.verify_license_on_initialize

      licenses.verify
    end
  end
end
