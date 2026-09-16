# SDKs e API externa — Ordex Pay

Documentação para a página **Integração** do painel. O integrador só precisa da **chave de API** gerada abaixo. Dados bancários e gateway ficam no Ordex Pay — não vão no SDK.

**Repositório dos SDKs:** [github.com/ordexsistemas/agenda-cobranca-sdks](https://github.com/ordexsistemas/agenda-cobranca-sdks)

---

## Como funciona

1. Gere a **chave de API** da empresa neste painel.
2. Instale o SDK da linguagem do ERP (Rails / .NET / etc.).
3. Configure **apenas** a chave (e, se precisar, a URL do ambiente).
4. Chame a API. A chave já identifica a empresa — **não envie `empresa_id` na URL**.

Em toda requisição o cliente envia:

| Header | Valor |
| --- | --- |
| `chave_api` | sua chave de API |
| `X-Api-Key` | a mesma chave |

**URL base (HML):**

```text
https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo
```

Todas as rotas começam com `/api/v2/externo`. Não use JWT: a autenticação é a chave.

Se o plano da empresa estiver **pausado** ou a chave for inválida/revogada, a API responde **HTTP 403**. O SDK só propaga o erro — baixar o pacote não libera a API.

---

## Ruby / Rails

### Instalar

No `Gemfile` (desenvolvimento com monorepo local):

```ruby
gem "agenda_cobranca", path: "/caminho/para/agenda-cobranca-sdks/packages/ruby/agenda_cobranca"
```

Ou, após publicar a gem:

```ruby
gem "agenda_cobranca", "~> 0.2"
```

```bash
bundle install
```

### Configurar

`config/initializers/agenda_cobranca.rb`:

```ruby
AgendaCobranca.configure do |config|
  config.api_key = ENV.fetch("ORDEX_PAY_API_KEY")
  # opcional — default já é HML:
  # config.base_url = ENV.fetch("ORDEX_PAY_BASE_URL")
end
```

Variável de ambiente:

```bash
export ORDEX_PAY_API_KEY="sua_chave_gerada_aqui"
```

### Exemplo — criar cobrança

```ruby
client = AgendaCobranca::Client.new

cobranca = client.cobrancas.create(
  external_reference: "pedido-1001",
  valor_centavos: 15_000,
  vencimento: "2026-10-01",
  pagador: {
    documento: "12345678901",
    nome: "Maria Silva",
    email: "maria@example.com"
  },
  idempotency_key: "pedido-1001-cobranca"
)

client.cobrancas.find(cobranca.id)
client.cobrancas.list
# client.cobrancas.cancel(cobranca.id)
```

No `rails console`, use o mesmo bloco para smoke test.

---

## C# / .NET 8

### Instalar

Referência local:

```bash
dotnet add reference /caminho/para/agenda-cobranca-sdks/packages/csharp/AgendaCobranca.Sdk/src/AgendaCobranca.Sdk/AgendaCobranca.Sdk.csproj
```

Ou pacote NuGet (quando publicado):

```bash
dotnet add package AgendaCobranca.Sdk
```

### Configurar (`Program.cs`)

```csharp
using AgendaCobranca.Sdk.DependencyInjection;

builder.Services.AddAgendaCobranca(options =>
{
    options.ApiKey = builder.Configuration["ORDEX_PAY_API_KEY"]
        ?? Environment.GetEnvironmentVariable("ORDEX_PAY_API_KEY")
        ?? throw new InvalidOperationException("ORDEX_PAY_API_KEY ausente");
});
```

### Exemplo — criar cobrança

```csharp
app.MapPost("/teste-cobranca", async (IAgendaCobrancaClient client) =>
{
    var cobranca = await client.CreateCobrancaAsync(new CreateCobrancaRequest(
        ValorCentavos: 15_000,
        Vencimento: "2026-10-01",
        Pagador: new Pagador("12345678901", "Maria Silva", "maria@example.com"),
        ExternalReference: "pedido-1001",
        IdempotencyKey: "pedido-1001-cobranca"));

    return Results.Ok(cobranca);
});
```

Outros métodos: `FindCobrancaAsync`, `ListCobrancasAsync`, `CancelCobrancaAsync`.

---

## Outras linguagens

| Linguagem | Pacote | Status |
| --- | --- | --- |
| Go | `packages/go/agenda-cobranca-go` | disponível no monorepo |
| Python / Node / Java | — | em breve |

Exemplos Go: ver o README do repositório.

---

## Endpoints (relativos à URL base)

| Ação | Método | Path |
| --- | --- | --- |
| Criar cobrança | `POST` | `/cobrancas` |
| Buscar cobrança | `GET` | `/cobrancas/{id}` |
| Listar cobranças | `GET` | `/cobrancas` |
| Cancelar cobrança | `POST` | `/cobrancas/{id}/cancel` |

A API externa também expõe recursos do painel (empresa, pagadores, faturas, contas, cobrança). Consulte as abas de endpoints nesta página para cURL e contratos. A chave já identifica a empresa.

---

## Erros comuns

| HTTP | Situação |
| --- | --- |
| 401 / 403 | Chave inválida, revogada ou plano pausado |
| 422 | Payload inválido (documento, vencimento, etc.) |
| 404 | Recurso / rota inexistente |

---

## O que o integrador NÃO precisa

- `client_id` / `client_secret`
- Dados bancários ou credenciais de gateway
- JWT ou OAuth no fluxo básico
- `empresa_id` na URL

Só a **chave de API** deste painel.
