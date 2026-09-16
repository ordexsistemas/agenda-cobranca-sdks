# Agenda Cobrança — SDKs (Ordex Pay)

Monorepo com thin clients da API externa Ordex Pay / Agenda Financeira (Ruby, Go, C# e Node.js).

**Uso básico:** só `api_key` (+ `base_url` opcional). HMAC é opcional e fica **OFF** por padrão.

Guia de integração e vetores HMAC ficam fora do git (`docs/` é local).  
Repositório: [https://github.com/ordexsistemas/agenda-cobranca-sdks](https://github.com/ordexsistemas/agenda-cobranca-sdks)

## Pacotes

| Linguagem | Pacote | Caminho |
| --- | --- | --- |
| Ruby | `agenda_cobranca` (GitHub Packages) | `packages/ruby/agenda_cobranca` |
| Go | `agendacobranca.dev/sdk/go` | `packages/go/agenda-cobranca-go` |
| C# | `AgendaCobranca.Sdk` (GitHub Packages, .NET 8) | `packages/csharp/AgendaCobranca.Sdk` |
| Node.js | `@ordexsistemas/agenda-cobranca` (GitHub Packages) | `packages/nodejs/agenda-cobranca` |

Documentação de arquitetura e vetores HMAC: pasta local `docs/` (não versionada).  
Instalação via GitHub Packages: seção [GitHub Packages](#github-packages) abaixo.

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

## Node.js — npm `@ordexsistemas/agenda-cobranca`

```ts
import { Client } from "@ordexsistemas/agenda-cobranca";

const client = new Client({
  apiKey: process.env.ORDEX_PAY_API_KEY!,
  // baseUrl: process.env.ORDEX_PAY_BASE_URL, // opcional
});

const cobranca = await client.cobrancas.create({
  externalReference: "pedido-1001",
  valorCentavos: 15_000,
  vencimento: "2026-10-01",
  pagador: { documento: "12345678901", nome: "Maria Silva", email: "maria@example.com" },
  idempotencyKey: "pedido-1001-cobranca",
});
```

```bash
cd packages/nodejs/agenda-cobranca
npm install
npm test
```

## Testar todos de uma vez

```bash
make test
```

## Endpoints

Cobranças (CRUD + cancel), `POST /licenses/verify` e `Webhooks.Verify` local.  
Paths adicionais da API (`/empresa`, `/pagadores`, `/faturas`) existem no contrato Ordex Pay; os thin clients não expõem helpers dedicados para todos eles.

## Versionamento

- `VERSION.yml` — component `sdk`, product "Ordex Pay SDKs"
- `CHANGELOG.md` — Keep a Changelog (PT)
- CLI: `./bin/versionamento` (Python stdlib em `tools/versionamento/`)
- Actions: `.github/workflows/versionamento.yml` (`workflow_dispatch`, bump auto|patch|minor|major)

Tag anotada: `sdk/vX.Y.Z`. Detalhes: [`tools/versionamento/README.md`](tools/versionamento/README.md).  
Após a tag, [`.github/workflows/publish.yml`](.github/workflows/publish.yml) publica gem / nupkg / npm no **GitHub Packages** e cria o tag Go de subdiretório. Instalação: [GitHub Packages](#github-packages).

## GitHub Packages

Não publicamos em nuget.org, npmjs.com nem RubyGems.org. O workflow usa `GITHUB_TOKEN` com `contents: write` e `packages: write` — sem `NPM_TOKEN` / `NUGET_API_KEY` / `RUBYGEMS_API_KEY`.

Os pacotes **herdam a visibilidade do repositório**. Se o repo for privado, o consumidor precisa de um PAT com `read:packages` (e `repo` se o pacote estiver ligado a um repositório privado).

### npm — `@ordexsistemas/agenda-cobranca`

`.npmrc`:

```ini
@ordexsistemas:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=SEU_PAT
```

```bash
npm i @ordexsistemas/agenda-cobranca
```

### NuGet — `AgendaCobranca.Sdk`

```bash
dotnet nuget add source https://nuget.pkg.github.com/ordexsistemas/index.json \
  --name github --username SEU_USUARIO --password SEU_PAT --store-password-in-clear-text
dotnet add package AgendaCobranca.Sdk
```

### RubyGems — `agenda_cobranca`

`~/.gem/credentials` (chmod 0600): `:github: Bearer SEU_PAT`

```ruby
source "https://rubygems.pkg.github.com/ordexsistemas" do
  gem "agenda_cobranca"
end
```

### Go — `agendacobranca.dev/sdk/go`

Go não usa card do GitHub Packages. Após o release, o workflow cria o tag `packages/go/agenda-cobranca-go/vX.Y.Z`.

```bash
go get agendacobranca.dev/sdk/go@v0.2.0
```

Sem vanity DNS:

```go
require agendacobranca.dev/sdk/go v0.2.0
replace agendacobranca.dev/sdk/go => github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/agenda-cobranca-go v0.2.0
```
