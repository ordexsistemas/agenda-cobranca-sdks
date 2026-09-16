# Agenda Cobrança — SDKs (Ordex Pay)

Monorepo com thin clients da API externa Ordex Pay / Agenda Financeira (Ruby, Go e C#).

**Uso básico:** só `api_key` (+ `base_url` opcional). HMAC é opcional e fica **OFF** por padrão.

Guia de integração para o portal: [`docs/integracao-ordex-pay.md`](docs/integracao-ordex-pay.md).  
Repositório: [https://github.com/ordexsistemas/agenda-cobranca-sdks](https://github.com/ordexsistemas/agenda-cobranca-sdks)

## Pacotes

| Linguagem | Pacote | Caminho |
| --- | --- | --- |
| Ruby | `agenda_cobranca` | `packages/ruby/agenda_cobranca` |
| Go | `agenda-cobranca-go` | `packages/go/agenda-cobranca-go` |
| C# | `AgendaCobranca.Sdk` (.NET 8) | `packages/csharp/AgendaCobranca.Sdk` |

Documentação de arquitetura: [`docs/architecture.md`](docs/architecture.md).  
Vetores HMAC: [`docs/hmac-test-vectors.md`](docs/hmac-test-vectors.md).

## Autenticação (padrão)

Headers em toda request:

- `chave_api` — valor da `api_key`
- `X-Api-Key` — mesmo valor

Base URL padrão (HML):

```
https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo
```

Plano pausado / chave inválida → **HTTP 403** (erro de autenticação no SDK).

Não há secrets hardcoded. Exemplos usam:

- `ORDEX_PAY_API_KEY`
- `ORDEX_PAY_BASE_URL` (opcional)

### HMAC opcional

Com `signing_enabled` / `SigningEnabled = true` (+ `client_secret`):

- `X-Client-Id`
- `X-Timestamp` (Unix em segundos)
- `X-Nonce` (UUIDv4)
- `X-Signature`

```
canonical_string = METHOD + "\n" + PATH + "\n" + TIMESTAMP + "\n" + NONCE + "\n" + HASH_SHA256(REQUEST_BODY)
signature        = HMAC_SHA256(canonical_string, CLIENT_SECRET)
```

`client_secret` **nunca** vai em header em texto plano.  
Sem HMAC, os headers `X-Client-Id` / `X-Timestamp` / `X-Nonce` / `X-Signature` **não** são enviados.

`Create` envia `Idempotency-Key`.  
`Webhooks.Verify` é verificação local (sem HTTP).

## Ruby — gem `agenda_cobranca`

```ruby
AgendaCobranca.configure do |config|
  config.api_key = ENV.fetch("ORDEX_PAY_API_KEY")
  config.base_url = ENV.fetch("ORDEX_PAY_BASE_URL", "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo") # optional
end

client = AgendaCobranca::Client.new

cobranca = client.cobrancas.create(
  external_reference: "pedido-1001",
  valor_centavos: 15_000,
  vencimento: "2026-10-01",
  pagador: { documento: "12345678901", nome: "Maria Silva", email: "maria@example.com" },
  idempotency_key: "pedido-1001-cobranca"
)

client.cobrancas.find(cobranca.id)
client.cobrancas.list(status: "pendente")
client.cobrancas.cancel(cobranca.id)
```

Initializer Rails: `packages/ruby/agenda_cobranca/examples/rails/agenda_cobranca.rb`.

```bash
cd packages/ruby/agenda_cobranca
bundle install
bundle exec rspec
```

## Go — módulo `agenda-cobranca-go`

```go
import agendacobranca "agendacobranca.dev/sdk/go"

client, err := agendacobranca.NewClient(agendacobranca.Options{
    APIKey:  os.Getenv("ORDEX_PAY_API_KEY"),
    // BaseURL: opcional
})
if err != nil {
    log.Fatal(err)
}

cobranca, err := client.CreateCobranca(ctx, agendacobranca.CreateCobrancaInput{
    ExternalReference: "pedido-1001",
    ValorCentavos:     15000,
    Vencimento:        "2026-10-01",
    Pagador: agendacobranca.Pagador{
        Documento: "12345678901",
        Nome:      "Maria Silva",
        Email:     "maria@example.com",
    },
    IdempotencyKey: "pedido-1001-cobranca",
})
```

```bash
cd packages/go/agenda-cobranca-go
go test ./...
```

## C# — NuGet `AgendaCobranca.Sdk` (.NET 8)

```csharp
services.AddAgendaCobranca(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("ORDEX_PAY_API_KEY")!;
    // options.BaseUrl = "..."; // opcional
});
```

```csharp
var cobranca = await client.CreateCobrancaAsync(new CreateCobrancaRequest(
    ValorCentavos: 15_000,
    Vencimento: "2026-10-01",
    Pagador: new Pagador("12345678901", "Maria Silva", "maria@example.com"),
    ExternalReference: "pedido-1001",
    IdempotencyKey: "pedido-1001-cobranca"));
```

```bash
cd packages/csharp/AgendaCobranca.Sdk
dotnet test
```

## Testar os três de uma vez

```bash
make test
```

## Endpoints

Cobranças (CRUD + cancel), `POST /licenses/verify` e `Webhooks.Verify` local.  
Paths adicionais da API (`/empresa`, `/pagadores`, `/faturas`) estão documentados em [`docs/integracao-ordex-pay.md`](docs/integracao-ordex-pay.md).
