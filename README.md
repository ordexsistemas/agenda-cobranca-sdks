# Agenda Cobrança — SDKs (entrega 1)

Monorepo privado com os thin clients da Agenda Cobrança API e o núcleo de assinatura HMAC compartilhado (a mesma *canonical string* em Ruby, Go e C#).

Nesta entrega os SDKs **assinam e falam HTTP direto** com a API (ou com a URL de um gateway, se você configurar `base_url`). O **Security Gateway** (pacote interno vendável: licenças, tokens efêmeros, perímetro HMAC) é a **etapa 2**.

## Pacotes

| Linguagem | Pacote | Caminho |
| --- | --- | --- |
| Ruby | `agenda_cobranca` | `packages/ruby/agenda_cobranca` |
| Go | `agenda-cobranca-go` | `packages/go/agenda-cobranca-go` |
| C# | `AgendaCobranca.Sdk` (.NET 8) | `packages/csharp/AgendaCobranca.Sdk` |

Documentação de arquitetura: [`docs/architecture.md`](docs/architecture.md).  
Vetores HMAC: [`docs/hmac-test-vectors.md`](docs/hmac-test-vectors.md).

## Segurança

Headers em toda request:

- `X-Client-Id`
- `X-Timestamp` (Unix em segundos)
- `X-Nonce` (UUIDv4)
- `X-Signature`
- `X-Api-Key`

```
canonical_string = METHOD + "\n" + PATH + "\n" + TIMESTAMP + "\n" + NONCE + "\n" + HASH_SHA256(REQUEST_BODY)
signature        = HMAC_SHA256(canonical_string, CLIENT_SECRET)
```

`client_secret` **nunca** vai em header em texto plano.  
`Create` envia `Idempotency-Key`.  
`Webhooks.Verify` é verificação local (sem HTTP).

Base URL padrão (configurável): `https://api.agendacobranca.example/v1`.

Não há secrets hardcoded. Exemplos usam placeholders / variáveis de ambiente:

- `AGENDA_COBRANCA_CLIENT_ID`
- `AGENDA_COBRANCA_API_KEY`
- `AGENDA_COBRANCA_CLIENT_SECRET`
- `AGENDA_COBRANCA_BASE_URL`

## Ruby — gem `agenda_cobranca`

```ruby
# Gemfile (path local neste monorepo)
gem "agenda_cobranca", path: "packages/ruby/agenda_cobranca"
```

```ruby
AgendaCobranca.configure do |config|
  config.client_id = ENV.fetch("AGENDA_COBRANCA_CLIENT_ID")
  config.api_key = ENV.fetch("AGENDA_COBRANCA_API_KEY")
  config.client_secret = ENV.fetch("AGENDA_COBRANCA_CLIENT_SECRET")
  config.base_url = ENV.fetch("AGENDA_COBRANCA_BASE_URL", "https://api.agendacobranca.example/v1")
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
client.licenses.verify

AgendaCobranca::Webhooks.verify(payload, headers, client_secret: ENV.fetch("AGENDA_COBRANCA_CLIENT_SECRET"))
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
    ClientID:     os.Getenv("AGENDA_COBRANCA_CLIENT_ID"),
    APIKey:       os.Getenv("AGENDA_COBRANCA_API_KEY"),
    ClientSecret: os.Getenv("AGENDA_COBRANCA_CLIENT_SECRET"),
    BaseURL:      "https://api.agendacobranca.example/v1",
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
    options.ClientId = Environment.GetEnvironmentVariable("AGENDA_COBRANCA_CLIENT_ID")!;
    options.ApiKey = Environment.GetEnvironmentVariable("AGENDA_COBRANCA_API_KEY")!;
    options.ClientSecret = Environment.GetEnvironmentVariable("AGENDA_COBRANCA_CLIENT_SECRET")!;
    options.BaseUrl = "https://api.agendacobranca.example/v1";
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

Somente o que a spec define: CRUD de cobranças, `POST /licenses/verify` e `Webhooks.Verify` local. Nenhum outro recurso.
