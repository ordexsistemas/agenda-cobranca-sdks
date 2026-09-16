# frozen_string_literal: true

require_relative "lib/ordex_whatsapp/version"

Gem::Specification.new do |spec|
  spec.name = "ordex_whatsapp"
  spec.version = OrdexWhatsApp::VERSION
  spec.authors = ["Ordex Sistemas"]

  spec.summary = "SDK WhatsApp Cloud API (add-on SaaS Ordex Pay)"
  spec.description = <<~DESC
    SDK Ruby para a API principal do WhatsApp (Meta Cloud API) com medição por
    categoria e controle da quantidade de cobranças na Agenda Financeira.
  DESC
  spec.homepage = "https://github.com/ordexsistemas/agenda-cobranca-sdks"
  spec.license = "MIT"
  spec.required_ruby_version = ">= 3.1.0"
  spec.metadata["homepage_uri"] = spec.homepage
  spec.metadata["source_code_uri"] = "https://github.com/ordexsistemas/agenda-cobranca-sdks"
  spec.metadata["changelog_uri"] = "https://github.com/ordexsistemas/agenda-cobranca-sdks/blob/main/CHANGELOG.md"
  spec.metadata["allowed_push_host"] = "https://rubygems.pkg.github.com/ordexsistemas"

  spec.files = Dir.chdir(__dir__) do
    Dir["lib/**/*", "examples/**/*", "README.md"].select { |path| File.file?(path) }
  end
  spec.require_paths = ["lib"]

  spec.add_dependency "faraday", "~> 2.10"
  spec.add_dependency "agenda_cobranca", ">= 0.2.1"

  spec.add_development_dependency "rake", "~> 13.2"
  spec.add_development_dependency "rspec", "~> 3.13"
  spec.add_development_dependency "webmock", "~> 3.23"
end
