# frozen_string_literal: true

module AgendaCobranca
  class Configuration
    DEFAULT_BASE_URL = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"

    attr_accessor :api_key,
                  :base_url,
                  :client_id,
                  :client_secret,
                  :signing_enabled,
                  :timeout,
                  :open_timeout,
                  :verify_license_on_initialize

    def initialize
      @base_url = DEFAULT_BASE_URL
      @signing_enabled = false
      @timeout = 30
      @open_timeout = 10
      @verify_license_on_initialize = false
    end

    def validate!
      faltando = []
      faltando << "api_key" if blank?(api_key)
      faltando << "base_url" if blank?(base_url)
      if signing_enabled
        faltando << "client_secret" if blank?(client_secret)
      end

      return if faltando.empty?

      raise ConfigurationError, "Configuracao incompleta: #{faltando.join(', ')}"
    end

    private

    def blank?(valor)
      valor.nil? || valor.to_s.strip.empty?
    end
  end
end
