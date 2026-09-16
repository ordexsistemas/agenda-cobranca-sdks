# AgendaCobranca.Sdk

Pacote NuGet (.NET 8) da API externa Ordex Pay / Agenda Financeira.
No uso basico basta `ApiKey` (+ `BaseUrl` opcional). HMAC e opcional (`SigningEnabled`, OFF por padrao).

```bash
dotnet add package AgendaCobranca.Sdk
```

```csharp
services.AddAgendaCobranca(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("ORDEX_PAY_API_KEY")!;
    // options.BaseUrl = Environment.GetEnvironmentVariable("ORDEX_PAY_BASE_URL"); // opcional
});

var cobranca = await client.CreateCobrancaAsync(new CreateCobrancaRequest(
    ValorCentavos: 15_000,
    Vencimento: "2026-10-01",
    Pagador: new Pagador("12345678901", "Maria Silva", "maria@example.com"),
    ExternalReference: "pedido-1001",
    IdempotencyKey: "pedido-1001-cobranca"));
```

Toda request envia `chave_api` e `X-Api-Key`. Com `SigningEnabled = true`, tambem envia os headers HMAC.

```bash
dotnet test
```
