# frozen_string_literal: true

require "spec_helper"

RSpec.describe AgendaCobranca::Resources::License do
  it "chama POST /licenses/verify" do
    AgendaCobranca.configure do |cfg|
      cfg.api_key = "api_key_exemplo"
      cfg.client_id = "client_exemplo"
      cfg.base_url = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
    end

    stub = stub_request(:post, "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo/licenses/verify")
           .with(body: hash_including("client_id" => "client_exemplo"))
           .to_return(
             status: 200,
             body: { "success" => true, "data" => { "valid" => true, "message" => "ok" } }.to_json,
             headers: { "Content-Type" => "application/json" }
           )

    resultado = AgendaCobranca::Client.new.licenses.verify

    expect(stub).to have_been_requested
    expect(resultado[:valid]).to be(true)
    expect(resultado[:message]).to eq("ok")
  end
end
