# agenda_cobranca

Gem Ruby (thin client) da API externa Ordex Pay / Agenda Financeira.
No uso basico basta `api_key` (+ `base_url` opcional). HMAC e opcional e fica OFF por padrao.

## Instalacao

Publicado no **GitHub Packages**. Host e PAT: ver [`docs/publishing.md`](../../../docs/publishing.md).

```ruby
# Gemfile
source "https://rubygems.pkg.github.com/ordexsistemas" do
  gem "agenda_cobranca"
end
```

No monorepo:

```ruby
gem "agenda_cobranca", path: "../../packages/ruby/agenda_cobranca"
```

```bash
bundle install
```

## Configuracao (api_key only)

```ruby
AgendaCobranca.configure do |config|
  config.api_key = ENV.fetch("ORDEX_PAY_API_KEY")
  config.base_url = ENV.fetch(
    "ORDEX_PAY_BASE_URL",
    "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
  ) # opcional
end

client = AgendaCobranca::Client.new
```

Toda request envia `chave_api` e `X-Api-Key` com o mesmo valor.

### HMAC opcional

```ruby
AgendaCobranca.configure do |config|
  config.api_key = ENV.fetch("ORDEX_PAY_API_KEY")
  config.signing_enabled = true
  config.client_id = ENV.fetch("ORDEX_PAY_CLIENT_ID")
  config.client_secret = ENV.fetch("ORDEX_PAY_CLIENT_SECRET")
end
```

Com `signing_enabled: true`, o SDK tambem envia `X-Client-Id`, `X-Timestamp`, `X-Nonce` e `X-Signature`.
`client_secret` nunca vai em header em texto plano.

Initializer Rails: `examples/rails/agenda_cobranca.rb`.

## Criar cobranca

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
