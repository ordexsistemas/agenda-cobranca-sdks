# Arquitetura — Ecossistema SDK Agenda Cobrança / Ordex Pay

## Camada atual (thin clients)

Ruby (`agenda_cobranca`), Go (`agendacobranca.dev/sdk/go`), C# (`AgendaCobranca.Sdk`) e Node.js (`agenda-cobranca`) falam HTTPS com a API externa Ordex Pay.

```
App do cliente
    → SDK (Ruby | Go | C# | Node.js)
        → HTTPS + headers chave_api / X-Api-Key
            → API (https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo)
```

## Credenciais

| Campo | Obrigatório? | Transporte |
| --- | --- | --- |
| `api_key` | **sim** (uso básico) | headers `chave_api` e `X-Api-Key` (mesmo valor) |
| `base_url` | não (tem default HML) | URL base do cliente HTTP |
| `client_id` / `client_secret` | só se `signing_enabled` | HMAC; `client_secret` **nunca** em header plain |

HMAC (`signing_enabled` / `SigningEnabled`) fica **OFF** por padrão. Sem HMAC, não se enviam `X-Client-Id`, `X-Timestamp`, `X-Nonce` nem `X-Signature`.

## Canonical string HMAC (quando habilitado)

Idêntica nas quatro linguagens:

```
canonical_string = METHOD + "\n" + PATH + "\n" + TIMESTAMP + "\n" + NONCE + "\n" + HASH_SHA256(REQUEST_BODY)
signature        = HMAC_SHA256(canonical_string, CLIENT_SECRET)
```

Regras:

- `METHOD` em maiúsculas (`GET`, `POST`).
- `PATH` é o path absoluto da URL, sem query string.
- `TIMESTAMP` é Unix em segundos (string decimal).
- `NONCE` é UUIDv4.
- `HASH_SHA256(REQUEST_BODY)` é hex minúsculo. Body vazio usa SHA-256 de `""`.
- `X-Signature` é HMAC-SHA256 em hex minúsculo.

Vetores compartilhados: [`docs/hmac-test-vectors.md`](hmac-test-vectors.md).

## Endpoints cobertos no SDK

| Método SDK | HTTP | Path relativo à `base_url` |
| --- | --- | --- |
| `Cobrancas.Create` | `POST` | `/cobrancas` (+ `Idempotency-Key`) |
| `Cobrancas.Find` | `GET` | `/cobrancas/{id}` |
| `Cobrancas.List` | `GET` | `/cobrancas` |
| `Cobrancas.Cancel` | `POST` | `/cobrancas/{id}/cancel` |
| `Licenses.Verify` | `POST` | `/licenses/verify` (opcional na inicialização) |
| `Webhooks.Verify` | — | verificação **local** |

Paths adicionais da API (`/empresa`, `/pagadores`, `/faturas`): ver [`integracao-ordex-pay.md`](integracao-ordex-pay.md).

## Onde cada SDK injeta headers

| SDK | Ponto de injeção |
| --- | --- |
| Ruby | Faraday middleware `Security::SigningMiddleware` |
| Go | `http.RoundTripper` em `security.go` |
| C# | `SigningDelegatingHandler` + `AddAgendaCobranca` |
| Node.js | `Client.request` (headers HMAC quando `signingEnabled`) |

## Integração portal

Guia para o portal Ordex Pay Integração: [`integracao-ordex-pay.md`](integracao-ordex-pay.md).
