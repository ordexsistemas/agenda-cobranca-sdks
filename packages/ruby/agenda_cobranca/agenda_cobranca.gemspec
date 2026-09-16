# frozen_string_literal: true

require_relative "lib/agenda_cobranca/version"

Gem::Specification.new do |spec|
  spec.name = "agenda_cobranca"
  spec.version = AgendaCobranca::VERSION
  spec.authors = ["Agenda Cobranca"]
  spec.email = ["devs@agendacobranca.example"]

  spec.summary = "Thin client Ruby para a Agenda Cobranca API"
  spec.description = <<~DESC
    SDK Ruby com assinatura HMAC-SHA256 (X-Client-Id, X-Timestamp, X-Nonce, X-Signature)
    para a Agenda Cobranca API. Thin client: HTTP direto para a API ou para um
    Security Gateway configuravel via base_url.
  DESC
  spec.homepage = "https://agendacobranca.example"
  spec.required_ruby_version = ">= 3.1.0"
  spec.metadata["source_code_uri"] = "https://agendacobranca.example"

  spec.files = Dir.chdir(__dir__) do
    Dir["lib/**/*", "examples/**/*", "README.md"].select { |path| File.file?(path) }
  end
  spec.require_paths = ["lib"]

  spec.add_dependency "faraday", "~> 2.10"

  spec.add_development_dependency "rake", "~> 13.2"
  spec.add_development_dependency "rspec", "~> 3.13"
  spec.add_development_dependency "webmock", "~> 3.23"
end
