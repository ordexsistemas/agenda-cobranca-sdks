# @ordexsistemas/whatsapp-sdk

Add-on **SaaS Ordex Pay** para a API principal do WhatsApp (Meta Cloud API).

Empresas no plano que inclui o extra WhatsApp usam este pacote para:

1. Enviar templates e mensagens de sessão pela Graph API.
2. **Taguear** cada envio em `auth` | `utility` | `service` | `marketing`.
3. Conferir **entitlement** do add-on (plugável contra Ordex Pay; mock para demos).
4. **Medir** o volume contra as cotas mensais do Cenário Base (~100k transações/mês), configuráveis por tenant/plano.
5. **Controlar a quantidade de cobranças** na [Agenda de Cobranças](../agenda-cobranca) a partir desse uso.

Requer Node.js 18+ (`fetch` nativo). Não commite tokens reais — use o `.env.example`.

## Instalação (clientes SaaS)

Pacote no **GitHub Packages** (scope `ordexsistemas`). `.npmrc`:

```ini
@ordexsistemas:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GITHUB_TOKEN}
```

```bash
npm i @ordexsistemas/whatsapp-sdk
```

Opcional, se já usa o thin client da agenda:

```bash
npm i @ordexsistemas/agenda-cobranca
```

PAT `read:packages`: README na raiz do monorepo (GitHub Packages).

## Variáveis de ambiente

Copie [`/.env.example`](../../.env.example) ou o [`.env.example`](./.env.example) deste pacote:

```bash
WHATSAPP_WABA_ID=your_waba_id
WHATSAPP_PHONE_NUMBER_ID=your_phone_number_id
WHATSAPP_ACCESS_TOKEN=your_meta_access_token
WHATSAPP_APP_SECRET=your_meta_app_secret
WHATSAPP_VERIFY_TOKEN=your_webhook_verify_token

ORDEX_PAY_API_KEY=your_ordex_api_key
ORDEX_PAY_BASE_URL=https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo
ORDEX_PAY_TENANT_ID=your_tenant_id
```

```ts
import { WhatsAppClient, optionsFromEnv, StaticEntitlementChecker } from "@ordexsistemas/whatsapp-sdk";

const client = new WhatsAppClient({
  ...optionsFromEnv(),
  entitlement: new StaticEntitlementChecker(true), // demo; em produção use OrdexPayEntitlementChecker
});
```

## Cotas do Cenário Base (configuráveis)

Valores **default** do plano — não estão cravados para sempre: passe `plan` / `quotaMode` por tenant.

| Categoria | Uso | Volume / mês | Unitário USD | Total USD | Total BRL (câmbio 5,5) |
| --- | --- | ---: | ---: | ---: | ---: |
| `auth` | Login 2FA, saque/PIX OTP | 40.000 sessões | 0,0315 | 1.260 | 6.930 |
| `utility` | PIX recebido/pago, chargeback, liquidação | 60.000 sessões | 0,0350 | 2.100 | 11.550 |
| `service` | Suporte merchant/pagador | 5.000 sessões (**1.000 free**, 4.000 faturáveis) | 0,0300 | 120 | 660 |
| `marketing` | Reativação, ofertas de crédito/taxa | 10.000 envios | 0,0625 | 625 | 3.437,50 |

- **Service:** os primeiros 1.000 sessões do mês são franquia; só o excedente entra no custo e na quantidade de cobranças.
- **`quotaMode: "hard"`** (default): estouro de cota **bloqueia** o envio (`QuotaExceededError`) e não chama a Meta.
- **`quotaMode: "soft"`:** envia mesmo assim e marca `metering.overage = true`.

```ts
const client = new WhatsAppClient({
  accessToken: process.env.WHATSAPP_ACCESS_TOKEN!,
  phoneNumberId: process.env.WHATSAPP_PHONE_NUMBER_ID!,
  wabaId: process.env.WHATSAPP_WABA_ID,
  tenantId: process.env.ORDEX_PAY_TENANT_ID!,
  ordexApiKey: process.env.ORDEX_PAY_API_KEY,
  quotaMode: "hard",
  plan: {
    usdToBrl: 5.5,
    categories: {
      marketing: { monthlyQuota: 12_000, unitCostUsd: 0.0625 },
    },
  },
  entitlement: new StaticEntitlementChecker(true),
});
```

## Envio por categoria

Toda mensagem **obrigatoriamente** leva `category`. O SDK grava `{ category }` em `biz_opaque_callback_data` (correlação no webhook da Meta).

```ts
await client.sendTemplate({
  to: "5511999999999",
  category: "utility",
  template: { name: "pix_recebido", language: "pt_BR" },
});

await client.sendTemplate({
  to: "5511999999999",
  category: "auth",
  template: { name: "otp_pix", language: "pt_BR", components: [/* ... */] },
});

await client.sendText({
  to: "5511999999999",
  category: "service", // default
  body: "Seu PIX foi confirmado. Precisa de algo mais?",
});

await client.send({
  to: "5511999999999",
  category: "marketing",
  type: "template",
  template: { name: "oferta_taxa", language: "pt_BR" },
});
```

O retorno inclui `metering` (usado, franquia, remaining, overage) e um `cobrancaPlan` prévio da quantidade a agendar.

## Entitlement (add-on SaaS)

Antes de cada envio o SDK exige o extra WhatsApp habilitado para o tenant.

```ts
import { OrdexPayEntitlementChecker, StaticEntitlementChecker } from "@ordexsistemas/whatsapp-sdk";

// Produção — POST /addons/whatsapp/entitlement (404 → fallback POST /licenses/verify)
entitlement: new OrdexPayEntitlementChecker(process.env.ORDEX_PAY_API_KEY!, {
  baseUrl: process.env.ORDEX_PAY_BASE_URL,
});

// Demo / testes
entitlement: new StaticEntitlementChecker(true);
```

`OrdexPayEntitlementChecker` envia `chave_api` + `X-Api-Key` como os outros SDKs. Se a API de add-ons ainda não existir, o fallback lê `addons.whatsapp` em `licenses/verify`.

## Como o uso vira quantidade de cobranças

Função pura (testada) `computeCobrancaQuantity(counts, plan, options)`:

1. Aplica franquia (service: 1.000).
2. Calcula `costUsd = billable * unitCostUsd` e `costBrlCentavos` com `usdToBrl` do plano.
3. Converte em **quantidade** na Agenda:

| `quantityStrategy` | Fórmula | Cenário Base |
| --- | --- | --- |
| `per-category` (default) | 1 cobrança por categoria faturável | 4 cobranças (auth, utility, service, marketing) |
| `by-sessions` | `ceil(billable / sessionsPerCobranca)` | 12 com 10.000 sessões/cobrança |
| `by-value` | `ceil(centavos / valorCentavosPorCobranca)` | 24 com R$ 1.000 / cobrança |

`syncCobrancas()` cria as faltantes e **cancela extras** (`external_reference` = `wa:{tenant}:{period}:{categoria}:{indice}`), limitando a agenda ao uso medido.

```ts
const client = new WhatsAppClient({
  /* Meta + tenant ... */
  ordexApiKey: process.env.ORDEX_PAY_API_KEY,
  ordexBaseUrl: process.env.ORDEX_PAY_BASE_URL,
  agendaPagador: { documento: "12345678901", nome: "Empresa SaaS" },
  quantityStrategy: "by-sessions",
  sessionsPerCobranca: 10_000,
  entitlement: new StaticEntitlementChecker(true),
});

await client.sendTemplate({ /* ... */ });
const preview = await client.previewCobrancaPlan(); // não chama a agenda
const sync = await client.syncCobrancas();          // create / keep / cancel

// Quem já usa @ordexsistemas/agenda-cobranca pode injetar o client:
// agendaClient: agenda, agendaPagador: { ... }
```

`syncCobrancasOnSend: true` ajusta a agenda a cada envio (útil em demos; em produção prefira um job no fechamento do mês).

Store de uso: `InMemoryUsageStore` (processo único). Em cluster, injete `usageStore` compartilhado (Redis/Ordex) implementando `UsageStore`.

## Webhooks da Meta

```ts
import {
  verifyWebhookChallenge,
  verifyMetaSignatureOrThrow,
  parseInboundMessages,
} from "@ordexsistemas/whatsapp-sdk";

// GET de verificação
const challenge = verifyWebhookChallenge(req.query, process.env.WHATSAPP_VERIFY_TOKEN!);

// POST
verifyMetaSignatureOrThrow(rawBody, req.headers["x-hub-signature-256"], process.env.WHATSAPP_APP_SECRET!);
const messages = parseInboundMessages(JSON.parse(rawBody));
```

## Testes e build

```bash
cd packages/nodejs/whatsapp-sdk
npm install
npm test
npm run build
```

Exemplo local (fetch mock, sem secrets): [`examples/send-and-meter.ts`](./examples/send-and-meter.ts).
