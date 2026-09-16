# Ordex.WhatsApp.Sdk

Add-on **SaaS Ordex Pay** para a API principal do WhatsApp (Meta Cloud API), em .NET 8.

Empresas no plano que inclui o extra WhatsApp usam este pacote para:

1. Enviar templates e mensagens de sessão pela Graph API.
2. Taguear cada envio em `auth` | `utility` | `service` | `marketing`.
3. Conferir entitlement do add-on (plugável contra Ordex Pay; mock para demos).
4. Medir o volume contra as cotas mensais do Cenário Base (~100k transações/mês).
5. Controlar a quantidade de cobranças na [Agenda de Cobranças](../AgendaCobranca.Sdk) a partir desse uso.

A sincronização da agenda **compõe** `AgendaCobranca.Sdk` (`IAgendaCobrancaClient`) — HMAC e headers `chave_api` / `X-Api-Key` ficam no cliente da Agenda, sem duplicar assinatura aqui.

Publicado no **GitHub Packages**. Source e PAT: README na raiz do monorepo.

```bash
dotnet nuget add source https://nuget.pkg.github.com/ordexsistemas/index.json --name github --username USER --password PAT --store-password-in-clear-text
dotnet add package Ordex.WhatsApp.Sdk
```

Não commite tokens reais — use placeholders (`WHATSAPP_ACCESS_TOKEN`, `ORDEX_PAY_API_KEY`, …).

## DI

```csharp
services.AddAgendaCobranca(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("ORDEX_PAY_API_KEY")!;
});

services.AddOrdexWhatsApp(options =>
{
    options.AccessToken = Environment.GetEnvironmentVariable("WHATSAPP_ACCESS_TOKEN")!;
    options.PhoneNumberId = Environment.GetEnvironmentVariable("WHATSAPP_PHONE_NUMBER_ID")!;
    options.WabaId = Environment.GetEnvironmentVariable("WHATSAPP_WABA_ID");
    options.TenantId = Environment.GetEnvironmentVariable("ORDEX_PAY_TENANT_ID")!;
    options.OrdexApiKey = Environment.GetEnvironmentVariable("ORDEX_PAY_API_KEY");
    options.AgendaPagador = new Pagador("12345678901", "Empresa SaaS");
    options.Entitlement = new StaticEntitlementChecker(true); // demo; produção: OrdexPayEntitlementChecker
});
```

Se `IAgendaCobrancaClient` já estiver no container, o WhatsApp SDK o reutiliza.

## Cotas do Cenário Base (configuráveis)

| Categoria | Uso | Volume / mês | Unitário USD | Total USD | Total BRL (câmbio 5,5) |
| --- | --- | ---: | ---: | ---: | ---: |
| `auth` | Login 2FA, saque/PIX OTP | 40.000 sessões | 0,0315 | 1.260 | 6.930 |
| `utility` | PIX recebido/pago, chargeback | 60.000 sessões | 0,0350 | 2.100 | 11.550 |
| `service` | Suporte merchant/pagador | 5.000 (**1.000 free**, 4.000 faturáveis) | 0,0300 | 120 | 660 |
| `marketing` | Reativação, ofertas | 10.000 envios | 0,0625 | 625 | 3.437,50 |

- Service: os primeiros 1.000 sessões do mês são franquia.
- `QuotaMode.Hard` (default): estouro **bloqueia** o envio (`WhatsAppQuotaExceededException`).
- `QuotaMode.Soft`: envia e marca `Metering.Overage = true`.

## Envio e quantidade de cobranças

```csharp
await client.SendTemplateAsync("5511999999999", MessageCategory.Utility, new TemplatePayload("pix_recebido", "pt_BR"));
await client.SendTextAsync("5511999999999", "Seu PIX foi confirmado.", MessageCategory.Service);

var preview = await client.PreviewCobrancaPlanAsync(); // não chama a agenda
var sync = await client.SyncCobrancasAsync();          // create / keep / cancel
```

`CobrancaQuantity.Compute` converte uso em quantidade na Agenda:

| Strategy | Fórmula | Cenário Base |
| --- | --- | --- |
| `PerCategory` (default) | 1 cobrança por categoria faturável | 4 cobranças |
| `BySessions` | `ceil(billable / SessionsPerCobranca)` | 12 com 10.000 sessões/cobrança |
| `ByValue` | `ceil(centavos / ValorCentavosPorCobranca)` | 24 com R$ 1.000 / cobrança |

`external_reference` = `wa:{tenant}:{period}:{categoria}:{indice}`.

## Testes

```bash
cd packages/csharp/Ordex.WhatsApp.Sdk
dotnet test
```
