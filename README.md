# Agenda Cobrança — SDKs (Ordex Pay)

Monorepo com thin clients da API externa Ordex Pay / Agenda Financeira (Ruby, Go, C# e Node.js).

**Uso básico:** só `api_key` (+ `base_url` opcional). HMAC é opcional e fica **OFF** por padrão.

Guia de integração e vetores HMAC ficam fora do git (`docs/` é local).  
Repositório: [https://github.com/ordexsistemas/agenda-cobranca-sdks](https://github.com/ordexsistemas/agenda-cobranca-sdks)

## Pacotes

| Linguagem | Pacote | Caminho |
| --- | --- | --- |
| Ruby | `agenda_cobranca` (GitHub Packages) | `packages/ruby/agenda_cobranca` |
| Ruby | `ordex_whatsapp` (add-on SaaS WhatsApp Cloud API) | `packages/ruby/whatsapp` |
| Go | `agendacobranca.dev/sdk/go` | `packages/go/agenda-cobranca-go` |
| Go | `agendacobranca.dev/sdk/whatsapp` | `packages/go/whatsapp-go` |
| C# | `AgendaCobranca.Sdk` (GitHub Packages, .NET 8) | `packages/csharp/AgendaCobranca.Sdk` |
| C# | `Ordex.WhatsApp.Sdk` (add-on SaaS WhatsApp Cloud API) | `packages/csharp/Ordex.WhatsApp.Sdk` |
| Node.js | `@ordexsistemas/agenda-cobranca` (GitHub Packages) | `packages/nodejs/agenda-cobranca` |
| Node.js | `@ordexsistemas/whatsapp-sdk` (add-on SaaS WhatsApp Cloud API) | `packages/nodejs/whatsapp-sdk` |

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

## Node.js — npm `@ordexsistemas/whatsapp-sdk` (add-on SaaS)

Thin client da **API principal do WhatsApp** (Meta Cloud API) para tenants Ordex Pay com o extra WhatsApp. Mede envios por categoria (`auth` / `utility` / `service` / `marketing`) contra as cotas do Cenário Base (~100k transações/mês) e converte o uso na **quantidade de cobranças** da Agenda Financeira.

Os quatro SDKs WhatsApp (Node, C#, Go, Ruby) compartilham as mesmas regras. Detalhes e exemplos: READMEs em `packages/*/…`.

```ts
import { StaticEntitlementChecker, WhatsAppClient } from "@ordexsistemas/whatsapp-sdk";

const client = new WhatsAppClient({
  accessToken: process.env.WHATSAPP_ACCESS_TOKEN!,
  phoneNumberId: process.env.WHATSAPP_PHONE_NUMBER_ID!,
  wabaId: process.env.WHATSAPP_WABA_ID,
  tenantId: process.env.ORDEX_PAY_TENANT_ID!,
  ordexApiKey: process.env.ORDEX_PAY_API_KEY,
  agendaPagador: { documento: "12345678901", nome: "Empresa SaaS" },
  entitlement: new StaticEntitlementChecker(true), // demo; produção: OrdexPayEntitlementChecker
});

await client.sendTemplate({
  to: "5511999999999",
  category: "utility",
  template: { name: "pix_recebido", language: "pt_BR" },
});

await client.syncCobrancas(); // cria/limita cobranças do período conforme o volume medido
```

### Como o uso vira quantidade de cobranças

1. Aplica franquia (service: 1.000 sessões/mês).
2. `costUsd = billable * unitCostUsd`; `costBrlCentavos` com `usdToBrl` (default 5,5).
3. Converte em quantidade na Agenda (`external_reference` = `wa:{tenant}:{period}:{categoria}:{indice}`):

| Strategy | Fórmula | Cenário Base (~100k/mês) |
| --- | --- | --- |
| `per-category` (default) | 1 cobrança por categoria faturável | **4** (auth, utility, service, marketing) |
| `by-sessions` | `ceil(billable / sessionsPerCobranca)` | **12** com 10.000 sessões/cobrança |
| `by-value` | `ceil(centavos / valorCentavosPorCobranca)` | **24** com R$ 1.000 / cobrança |

`syncCobrancas()` cria as faltantes e **cancela extras** (`create` / `keep` / `cancel`). Prefere o cliente Agenda de cada linguagem (HMAC fica no SDK da Agenda).

Cotas default: auth 40k @ $0,0315 · utility 60k @ $0,0350 · service 5k (4k faturáveis após 1k free) @ $0,0300 · marketing 10k @ $0,0625. Modos `hard`/`soft`.

```bash
cd packages/nodejs/whatsapp-sdk && npm test
cd packages/csharp/Ordex.WhatsApp.Sdk && dotnet test
cd packages/go/whatsapp-go && go test ./...
cd packages/ruby/whatsapp && bundle exec rspec
```

## C# — NuGet `Ordex.WhatsApp.Sdk`

```csharp
services.AddOrdexWhatsApp(options =>
{
    options.AccessToken = Environment.GetEnvironmentVariable("WHATSAPP_ACCESS_TOKEN")!;
    options.PhoneNumberId = Environment.GetEnvironmentVariable("WHATSAPP_PHONE_NUMBER_ID")!;
    options.TenantId = Environment.GetEnvironmentVariable("ORDEX_PAY_TENANT_ID")!;
    options.OrdexApiKey = Environment.GetEnvironmentVariable("ORDEX_PAY_API_KEY");
    options.AgendaPagador = new Pagador("12345678901", "Empresa SaaS");
    options.Entitlement = new StaticEntitlementChecker(true);
});
```

Reutiliza `IAgendaCobrancaClient` se já estiver no DI. README: [`packages/csharp/Ordex.WhatsApp.Sdk/README.md`](packages/csharp/Ordex.WhatsApp.Sdk/README.md).

## Go — módulo `agendacobranca.dev/sdk/whatsapp`

```go
client, err := whatsapp.NewClient(whatsapp.Options{
    AccessToken:   os.Getenv("WHATSAPP_ACCESS_TOKEN"),
    PhoneNumberID: os.Getenv("WHATSAPP_PHONE_NUMBER_ID"),
    TenantID:      os.Getenv("ORDEX_PAY_TENANT_ID"),
    OrdexAPIKey:   os.Getenv("ORDEX_PAY_API_KEY"),
    AgendaPagador: &whatsapp.Pagador{Documento: "12345678901", Nome: "Empresa SaaS"},
    Entitlement:   whatsapp.StaticEntitlementChecker{Enabled: true},
})
```

README: [`packages/go/whatsapp-go/README.md`](packages/go/whatsapp-go/README.md).

## Ruby — gem `ordex_whatsapp`

```ruby
client = OrdexWhatsApp::Client.new(
  access_token: ENV.fetch("WHATSAPP_ACCESS_TOKEN"),
  phone_number_id: ENV.fetch("WHATSAPP_PHONE_NUMBER_ID"),
  tenant_id: ENV.fetch("ORDEX_PAY_TENANT_ID"),
  ordex_api_key: ENV["ORDEX_PAY_API_KEY"],
  agenda_pagador: { documento: "12345678901", nome: "Empresa SaaS" },
  entitlement: OrdexWhatsApp::StaticEntitlementChecker.new(true)
)
```

Initializer Rails: `packages/ruby/whatsapp/examples/rails/ordex_whatsapp.rb`.  
README: [`packages/ruby/whatsapp/README.md`](packages/ruby/whatsapp/README.md).

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

### npm — `@ordexsistemas/agenda-cobranca` e `@ordexsistemas/whatsapp-sdk`

`.npmrc`:

```ini
@ordexsistemas:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=SEU_PAT
```

```bash
npm i @ordexsistemas/agenda-cobranca
npm i @ordexsistemas/whatsapp-sdk
```

### NuGet — `AgendaCobranca.Sdk` e `Ordex.WhatsApp.Sdk`

```bash
dotnet nuget add source https://nuget.pkg.github.com/ordexsistemas/index.json \
  --name github --username SEU_USUARIO --password SEU_PAT --store-password-in-clear-text
dotnet add package AgendaCobranca.Sdk
dotnet add package Ordex.WhatsApp.Sdk
```

### RubyGems — `agenda_cobranca` e `ordex_whatsapp`

`~/.gem/credentials` (chmod 0600): `:github: Bearer SEU_PAT`

```ruby
source "https://rubygems.pkg.github.com/ordexsistemas" do
  gem "agenda_cobranca"
  gem "ordex_whatsapp"
end
```

### Go — `agendacobranca.dev/sdk/go` e `agendacobranca.dev/sdk/whatsapp`

Go não usa card do GitHub Packages. Após o release, o workflow cria as tags `packages/go/agenda-cobranca-go/vX.Y.Z` e `packages/go/whatsapp-go/vX.Y.Z`.

```bash
go get agendacobranca.dev/sdk/go@v0.2.1
go get agendacobranca.dev/sdk/whatsapp@v0.2.1
```

Sem vanity DNS:

```go
require agendacobranca.dev/sdk/go v0.2.1
replace agendacobranca.dev/sdk/go => github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/agenda-cobranca-go v0.2.1

require agendacobranca.dev/sdk/whatsapp v0.2.1
replace agendacobranca.dev/sdk/whatsapp => github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/whatsapp-go v0.2.1
```
