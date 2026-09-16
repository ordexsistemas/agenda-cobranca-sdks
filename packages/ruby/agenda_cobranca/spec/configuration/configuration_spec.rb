# frozen_string_literal: true

require "spec_helper"

RSpec.describe AgendaCobranca::Configuration do
  it "aceita apenas api_key no modo padrao" do
    AgendaCobranca.reset_configuration!
    AgendaCobranca.configure do |cfg|
      cfg.api_key = "chave"
    end

    expect { AgendaCobranca::Client.new }.not_to raise_error
  end

  it "exige client_secret quando signing_enabled" do
    AgendaCobranca.reset_configuration!
    AgendaCobranca.configure do |cfg|
      cfg.api_key = "chave"
      cfg.signing_enabled = true
    end

    expect { AgendaCobranca::Client.new }.to raise_error(AgendaCobranca::ConfigurationError, /client_secret/)
  end
end
