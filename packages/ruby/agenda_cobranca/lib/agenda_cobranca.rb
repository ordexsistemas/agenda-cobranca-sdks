# frozen_string_literal: true

require "agenda_cobranca/version"
require "agenda_cobranca/errors"
require "agenda_cobranca/configuration"
require "agenda_cobranca/security/signer"
require "agenda_cobranca/security/signing_middleware"
require "agenda_cobranca/models/pagador"
require "agenda_cobranca/models/cobranca"
require "agenda_cobranca/resources/cobranca"
require "agenda_cobranca/resources/license"
require "agenda_cobranca/webhooks/verifier"
require "agenda_cobranca/client"

module AgendaCobranca
  class << self
    def configuration
      @configuration ||= Configuration.new
    end

    def configure
      yield configuration
      configuration
    end

    def reset_configuration!
      @configuration = Configuration.new
      @client = nil
    end

    def client
      @client ||= Client.new(configuration)
    end
  end
end
