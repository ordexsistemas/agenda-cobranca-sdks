# whatsapp-go

Add-on **SaaS Ordex Pay** para a API principal do WhatsApp (Meta Cloud API), em Go.

Empresas no plano que inclui o extra WhatsApp usam este módulo para:

1. Enviar templates e mensagens de sessão pela Graph API.
2. Taguear cada envio em `auth` | `utility` | `service` | `marketing`.
3. Conferir entitlement do add-on (plugável contra Ordex Pay).
4. Medir o volume contra as cotas mensais do Cenário Base (~100k transações/mês).
5. Controlar a quantidade de cobranças na [Agenda de Cobranças](../agenda-cobranca-go) a partir desse uso.

A sincronização da agenda **compõe** `agendacobranca.dev/sdk/go` (`AgendaSDKAdapter`) — HMAC e headers `chave_api` / `X-Api-Key` ficam no cliente da Agenda.

Módulo: `agendacobranca.dev/sdk/whatsapp` (diretório `packages/go/whatsapp-go`).

Após um release `sdk/vX.Y.Z`, o workflow de publish cria o tag `packages/go/whatsapp-go/vX.Y.Z`.

```bash
go get agendacobranca.dev/sdk/whatsapp@v0.2.1
```

Sem vanity DNS, use `replace` (mesmo padrão da Agenda):

```go
require agendacobranca.dev/sdk/whatsapp v0.2.1

replace agendacobranca.dev/sdk/whatsapp => github.com/ordexsistemas/agenda-cobranca-sdks/packages/go/whatsapp-go v0.2.1
```

Não commite tokens reais — use placeholders de ambiente.

```go
client, err := whatsapp.NewClient(whatsapp.Options{
    AccessToken:   os.Getenv("WHATSAPP_ACCESS_TOKEN"),
    PhoneNumberID: os.Getenv("WHATSAPP_PHONE_NUMBER_ID"),
    WabaID:        os.Getenv("WHATSAPP_WABA_ID"),
    TenantID:      os.Getenv("ORDEX_PAY_TENANT_ID"),
    OrdexAPIKey:   os.Getenv("ORDEX_PAY_API_KEY"),
    AgendaPagador: &whatsapp.Pagador{Documento: "12345678901", Nome: "Empresa SaaS"},
    Entitlement:   whatsapp.StaticEntitlementChecker{Enabled: true}, // demo
})

_, err = client.SendTemplate("5511999999999", whatsapp.CategoryUtility, whatsapp.TemplatePayload{
    Name: "pix_recebido", Language: "pt_BR",
}, "")
_, err = client.SyncCobrancas("")
```

## Cotas do Cenário Base (configuráveis)

| Categoria | Volume / mês | Unitário USD | Total USD | Total BRL (câmbio 5,5) |
| --- | ---: | ---: | ---: | ---: |
| `auth` | 40.000 | 0,0315 | 1.260 | 6.930 |
| `utility` | 60.000 | 0,0350 | 2.100 | 11.550 |
| `service` | 5.000 (**1.000 free**) | 0,0300 | 120 | 660 |
| `marketing` | 10.000 | 0,0625 | 625 | 3.437,50 |

`ComputeCobrancaQuantity` converte uso em quantidade na Agenda (`per-category` | `by-sessions` | `by-value`).  
`external_reference` = `wa:{tenant}:{period}:{categoria}:{indice}`.

## Testes

```bash
cd packages/go/whatsapp-go
go test ./...
```
