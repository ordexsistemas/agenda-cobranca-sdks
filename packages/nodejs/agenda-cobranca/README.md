# agenda-cobranca

Pacote npm (thin client TypeScript) da API externa Ordex Pay / Agenda Financeira.
No uso básico basta `apiKey` (+ `baseUrl` opcional). HMAC é opcional (`signingEnabled`, **OFF** por padrão).

## Instalação

```bash
npm install agenda-cobranca
```

Requer Node.js 18+ (usa `fetch` nativo).

## Configuração (só `api_key`)

```ts
import { Client } from "agenda-cobranca";

const client = new Client({
  apiKey: process.env.ORDEX_PAY_API_KEY!,
  baseUrl: process.env.ORDEX_PAY_BASE_URL, // opcional; default HML Ordex Pay
});
```

Toda request envia `chave_api` e `X-Api-Key` com o mesmo valor.

### HMAC opcional

```ts
const client = new Client({
  apiKey: process.env.ORDEX_PAY_API_KEY!,
  signingEnabled: true,
  clientId: process.env.ORDEX_PAY_CLIENT_ID,
  clientSecret: process.env.ORDEX_PAY_CLIENT_SECRET,
});
```

Com `signingEnabled: true`, o SDK também envia `X-Client-Id`, `X-Timestamp`, `X-Nonce` e `X-Signature`.
`clientSecret` nunca vai em header em texto plano.

## Cobranças

```ts
const cobranca = await client.cobrancas.create({
  externalReference: "pedido-1001",
  valorCentavos: 15_000,
  vencimento: "2026-10-01",
  pagador: { documento: "12345678901", nome: "Maria Silva", email: "maria@example.com" },
  idempotencyKey: "pedido-1001-cobranca",
});

await client.cobrancas.find(cobranca.id);
await client.cobrancas.list({ status: "pendente" });
await client.cobrancas.cancel(cobranca.id);
```

`create` sempre envia `Idempotency-Key` (UUID gerado se o chamador não informar).

Plano pausado / chave inválida → HTTP 403 mapeado para `AuthenticationError`.

## Webhooks (verificação local)

```ts
import { verifyWebhook, verifyWebhookOrThrow } from "agenda-cobranca";

const ok = verifyWebhook(rawBody, headers, process.env.ORDEX_PAY_CLIENT_SECRET!);
verifyWebhookOrThrow(rawBody, headers, process.env.ORDEX_PAY_CLIENT_SECRET!);
```

Não faz HTTP — só confere HMAC do payload.

## Testes

```bash
npm install
npm test
npm run build
```
