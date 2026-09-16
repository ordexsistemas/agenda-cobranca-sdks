.PHONY: test test-ruby test-go test-csharp test-node test-versionamento

test: test-ruby test-go test-csharp test-node test-versionamento

test-ruby:
	cd packages/ruby/agenda_cobranca && bundle exec rspec

test-go:
	cd packages/go/agenda-cobranca-go && go test ./...

test-csharp:
	cd packages/csharp/AgendaCobranca.Sdk && dotnet test

test-node:
	cd packages/nodejs/agenda-cobranca && npm test

test-versionamento:
	python3 -m unittest tools.versionamento.test_files
