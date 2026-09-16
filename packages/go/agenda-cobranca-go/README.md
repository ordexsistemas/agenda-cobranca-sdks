# agenda-cobranca-go

Módulo Go (thin client) da Agenda Cobrança API. O `http.RoundTripper` injeta `X-Client-Id`, `X-Timestamp`, `X-Nonce` e `X-Signature` em toda request.

```go
client, err := agendacobranca.NewClient(agendacobranca.Options{
    ClientID:     os.Getenv("AGENDA_COBRANCA_CLIENT_ID"),
    APIKey:       os.Getenv("AGENDA_COBRANCA_API_KEY"),
    ClientSecret: os.Getenv("AGENDA_COBRANCA_CLIENT_SECRET"),
    BaseURL:      "https://api.agendacobranca.example/v1",
})

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
go test ./...
```
