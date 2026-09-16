# Integração Ordex Pay — Agenda Financeira (API externa)

Guia para o portal **Ordex Pay Integração**: gerar a chave, instalar o SDK e fazer a primeira chamada só com `api_key`.

Repositório: [https://github.com/ordexsistemas/agenda-cobranca-sdks](https://github.com/ordexsistemas/agenda-cobranca-sdks)

## 1. Gerar a API key no painel

1. Acesse o painel Ordex Pay / Agenda Financeira.
2. Abra a área de **Integração** / **API externa**.
3. Gere (ou copie) a **chave de API** (`api_key`).
4. Guarde a chave em variável de ambiente — nunca em código-fonte nem em repositório.

Não é necessário `client_id`, `client_secret` nem dados bancários para o uso básico.

## 2. Autenticação

Em **toda** request o SDK envia os mesmos headers:

| Header | Valor |
| --- | --- |
| `chave_api` | sua `api_key` |
| `X-Api-Key` | sua `api_key` (mesmo valor) |

**Base URL padrão (HML):**

```
https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo
```

Sobrescreva com `base_url` / `BaseURL` / `BaseUrl` se apontar para outro ambiente.

### Plano pausado = 403

Se o plano da empresa estiver pausado ou a chave inválida/revogada, a API responde **HTTP 403**. O SDK mapeia isso para erro de autenticação (`AuthenticationError` / `AuthenticationException`).

### HMAC (opcional)

HMAC fica **desligado** por padrão. Só ative se o contrato/integração exigir:

- Ruby: `config.signing_enabled = true` (+ `client_secret`)
- Go: `SigningEnabled: true` (+ `ClientSecret`)
- C#: `SigningEnabled = true` (+ `ClientSecret`)
- Node.js: `signingEnabled: true` (+ `clientSecret`)

Nesse modo também são enviados `X-Client-Id`, `X-Timestamp`, `X-Nonce` e `X-Signature`. Sem HMAC, esses headers **não** são enviados.

## 3. Instalar

### Ruby

```ruby
# Gemfile
gem "agenda_cobranca"
# ou, no monorepo:
# gem "agenda_cobranca", path: "packages/ruby/agenda_cobranca"
```

```bash
bundle install
```

### Go

```bash
# Vanity (quando agendacobranca.dev responder go-get=1):
go get agendacobranca.dev/sdk/go@v0.2.0

# Sem vanity: use replace para o caminho GitHub — ver docs/publishing.md
# go get github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/agenda-cobranca-go@v0.2.0
```

### C# (.NET 8)

```bash
dotnet add package AgendaCobranca.Sdk
# ou referência de projeto: packages/csharp/AgendaCobranca.Sdk
```

### Node.js

```bash
npm install agenda-cobranca
```

## 4. Exemplos mínimos (só `api_key`)

### Ruby

```ruby
AgendaCobranca.configure do |config|
  config.api_key = ENV.fetch("ORDEX_PAY_API_KEY")
  config.base_url = ENV.fetch(
    "ORDEX_PAY_BASE_URL",
    "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
  ) # opcional
end

client = AgendaCobranca::Client.new

cobranca = client.cobrancas.create(
  external_reference: "pedido-1001",
  valor_centavos: 15_000,
  vencimento: "2026-10-01",
  pagador: { documento: "12345678901", nome: "Maria Silva", email: "maria@example.com" },
  idempotency_key: "pedido-1001-cobranca"
)
```

### Go

```go
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

### C#

```csharp
services.AddAgendaCobranca(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("ORDEX_PAY_API_KEY")!;
    // options.BaseUrl = "..."; // opcional
});

var cobranca = await client.CreateCobrancaAsync(new CreateCobrancaRequest(
    ValorCentavos: 15_000,
    Vencimento: "2026-10-01",
    Pagador: new Pagador("12345678901", "Maria Silva", "maria@example.com"),
    ExternalReference: "pedido-1001",
    IdempotencyKey: "pedido-1001-cobranca"));
```

### Node.js

```ts
import { Client } from "agenda-cobranca";

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

## 5. Endpoints relativos a `base_url`

Os SDKs chamam paths relativos à base configurada. Cobranças já estão implementadas:

| Recurso | Método | Path |
| --- | --- | --- |
| Criar cobrança | `POST` | `/cobrancas` (+ header `Idempotency-Key`) |
| Buscar cobrança | `GET` | `/cobrancas/{id}` |
| Listar cobranças | `GET` | `/cobrancas` |
| Cancelar cobrança | `POST` | `/cobrancas/{id}/cancel` |
| Verificar licença (opcional) | `POST` | `/licenses/verify` |

Paths adicionais da API externa (úteis para evolução / HTTP direto; thin clients ainda não expõem helpers dedicados em todas as linguagens):

| Recurso | Path típico |
| --- | --- |
| Empresa | `/empresa` |
| Pagadores | `/pagadores` |
| Faturas | `/faturas` |

Confirme os contratos exatos no OpenAPI / portal Ordex Pay do ambiente em uso.

Webhooks: `Webhooks.Verify` é verificação **local** (HMAC do payload), sem HTTP.

## 6. O que não pedir ao integrador

Para o fluxo básico **não** solicite:

- `client_id` / `client_secret`
- dados bancários da conta
- certificados ou tokens OAuth

Basta a **API key** gerada no painel e, se necessário, a URL do ambiente.

## 7. Links

- SDKs: [https://github.com/ordexsistemas/agenda-cobranca-sdks](https://github.com/ordexsistemas/agenda-cobranca-sdks)
- Arquitetura HMAC (opcional): [`architecture.md`](architecture.md)
- Vetores de teste HMAC: [`hmac-test-vectors.md`](hmac-test-vectors.md)
