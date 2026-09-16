# frozen_string_literal: true

$LOAD_PATH.unshift File.expand_path("../lib", __dir__)

require "json"
require "webmock/rspec"
require "agenda_cobranca"

RSpec.configure do |config|
  config.expect_with :rspec do |expectations|
    expectations.include_chain_clauses_in_custom_matcher_descriptions = true
  end

  config.mock_with :rspec do |mocks|
    mocks.verify_partial_doubles = true
  end

  config.shared_context_metadata_behavior = :apply_to_host_groups
  config.filter_run_when_matching :focus
  config.example_status_persistence_file_path = "spec/examples.txt"
  config.disable_monkey_patching!
  config.order = :random
  Kernel.srand config.seed

  config.before do
    AgendaCobranca.reset_configuration!
    AgendaCobranca.configure do |cfg|
      cfg.api_key = "api_key_exemplo"
      cfg.base_url = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
    end
  end
end
