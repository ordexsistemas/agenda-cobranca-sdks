# agenda_cobranca

Gem Ruby (thin client) da Agenda Cobrança API. Assina cada request com HMAC-SHA256 e fala HTTP direto com a API — ou com a URL de um Security Gateway, quando configurada em `base_url`.

## Instalação

```ruby
# Gemfile
gem "agenda_cobranca", path: "../../packages/ruby/agenda_cobranca"
```

Ou, após publicar no registry privado:

```ruby
gem "agenda_cobranca", "0.1.0"
```

```bash
bundle install
```

## Configuração

```ruby
AgendaCobranca.configure do |config|
  config.client_id = ENV.fetch("AGENDA_COBRANCA_CLIENT_ID")
  config.api_key = ENV.fetch("AGENDA_COBRANCA_API_KEY")
  config.client_secret = ENV.fetch("AGENDA_COBRANCA_CLIENT_SECRET")
  config.base_url = ENV.fetch("AGENDA_COBRANCA_BASE_URL", "https://api.agendacobranca.example/v1")
end

client = AgendaCobranca::Client.new
```

`client_secret` entra somente no HMAC. Nunca é enviado em header em texto plano.

Initializer Rails: `examples/rails/agenda_cobranca.rb`.

## Criar cobrança

```ruby
cobranca = client.cobrancas.create(
  external_reference: "pedido-1001",
  valor_centavos: 15_000,
  vencimento: "2026-10-01",
  pagador: { documento: "12345678901", nome: "Maria Silva", email: "maria@example.com" },
  idempotency_key: "pedido-1001-cobranca"
)
```

## Testes

```bash
bundle install
bundle exec rspec
```
