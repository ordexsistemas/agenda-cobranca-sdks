# frozen_string_literal: true

module AgendaCobranca
  class Configuration
    DEFAULT_BASE_URL = "https://api.agendacobranca.example/v1"

    attr_accessor :client_id,
                  :api_key,
                  :client_secret,
                  :base_url,
                  :timeout,
                  :open_timeout,
                  :verify_license_on_initialize

    def initialize
      @base_url = DEFAULT_BASE_URL
      @timeout = 30
      @open_timeout = 10
      @verify_license_on_initialize = false
    end

    def validate!
      faltando = []
      faltando << "client_id" if blank?(client_id)
      faltando << "api_key" if blank?(api_key)
      faltando << "client_secret" if blank?(client_secret)
      faltando << "base_url" if blank?(base_url)

      return if faltando.empty?

      raise ConfigurationError, "Configuracao incompleta: #{faltando.join(', ')}"
    end

    private

    def blank?(valor)
      valor.nil? || valor.to_s.strip.empty?
    end
  end
end
