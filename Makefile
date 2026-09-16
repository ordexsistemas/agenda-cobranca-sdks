.PHONY: test test-ruby test-go test-csharp test-node test-whatsapp test-whatsapp-node test-whatsapp-go test-whatsapp-csharp test-whatsapp-ruby test-versionamento

test: test-ruby test-go test-csharp test-node test-whatsapp test-versionamento

test-ruby:
	cd packages/ruby/agenda_cobranca && bundle exec rspec

test-go:
	cd packages/go/agenda-cobranca-go && go test ./...

test-csharp:
	cd packages/csharp/AgendaCobranca.Sdk && dotnet test

test-node:
	cd packages/nodejs/agenda-cobranca && npm test

test-whatsapp: test-whatsapp-node test-whatsapp-go test-whatsapp-csharp test-whatsapp-ruby

test-whatsapp-node:
	cd packages/nodejs/whatsapp-sdk && npm test

test-whatsapp-go:
	cd packages/go/whatsapp-go && go test ./...

test-whatsapp-csharp:
	cd packages/csharp/Ordex.WhatsApp.Sdk && dotnet test

test-whatsapp-ruby:
	cd packages/ruby/whatsapp && bundle exec rspec

test-versionamento:
	python3 -m unittest tools.versionamento.test_files
