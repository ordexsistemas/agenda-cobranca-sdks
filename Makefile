.PHONY: test test-ruby test-go test-csharp

test: test-ruby test-go test-csharp

test-ruby:
	cd packages/ruby/agenda_cobranca && bundle exec rspec

test-go:
	cd packages/go/agenda-cobranca-go && go test ./...

test-csharp:
	cd packages/csharp/AgendaCobranca.Sdk && dotnet test
