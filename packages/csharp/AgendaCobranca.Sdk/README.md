# AgendaCobranca.Sdk

Pacote NuGet (.NET 8) da Agenda Cobrança API. Um `DelegatingHandler` assina cada request; `AddAgendaCobranca` registra o cliente no `IHttpClientFactory`.

```csharp
services.AddAgendaCobranca(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("AGENDA_COBRANCA_CLIENT_ID")!;
    options.ApiKey = Environment.GetEnvironmentVariable("AGENDA_COBRANCA_API_KEY")!;
    options.ClientSecret = Environment.GetEnvironmentVariable("AGENDA_COBRANCA_CLIENT_SECRET")!;
    options.BaseUrl = "https://api.agendacobranca.example/v1";
});

var cobranca = await client.CreateCobrancaAsync(new CreateCobrancaRequest(
    ValorCentavos: 15_000,
    Vencimento: "2026-10-01",
    Pagador: new Pagador("12345678901", "Maria Silva", "maria@example.com"),
    ExternalReference: "pedido-1001",
    IdempotencyKey: "pedido-1001-cobranca"));
```

```bash
dotnet test
```
