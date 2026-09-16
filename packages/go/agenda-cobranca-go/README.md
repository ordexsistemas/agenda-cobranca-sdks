# agenda-cobranca-go

Modulo Go (thin client) da API externa Ordex Pay / Agenda Financeira.
No uso basico basta `APIKey` (+ `BaseURL` opcional). HMAC e opcional (`SigningEnabled`, OFF por padrao).

```go
client, err := agendacobranca.NewClient(agendacobranca.Options{
    APIKey:  os.Getenv("ORDEX_PAY_API_KEY"),
    BaseURL: os.Getenv("ORDEX_PAY_BASE_URL"), // opcional; default HML Ordex Pay
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

Toda request envia `chave_api` e `X-Api-Key`. Com `SigningEnabled: true`, tambem envia os headers HMAC.

```bash
go test ./...
```
