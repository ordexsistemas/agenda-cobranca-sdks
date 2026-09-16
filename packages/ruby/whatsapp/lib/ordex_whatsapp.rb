# frozen_string_literal: true

require "ordex_whatsapp/version"
require "ordex_whatsapp/errors"
require "ordex_whatsapp/plan"
require "ordex_whatsapp/metering"
require "ordex_whatsapp/cobranca_quantity"
require "ordex_whatsapp/entitlement"
require "ordex_whatsapp/graph"
require "ordex_whatsapp/agenda"
require "ordex_whatsapp/webhooks"
require "ordex_whatsapp/client"

module OrdexWhatsApp
  class << self
    def new_client(options = {})
      Client.new(options)
    end
  end
end
