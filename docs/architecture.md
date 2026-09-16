# Arquitetura — Ecossistema SDK Agenda Cobrança API

## Duas camadas

1. **Security & Licensing Gateway** (etapa 2, pacote interno vendável)  
   Faturamento, licenças, tokens efêmeros e validação HMAC no perímetro. Ainda não faz parte desta entrega.

2. **SDK Clients (thin clients)** — entrega 1  
   Ruby (`agenda_cobranca`), Go (`agenda-cobranca-go`) e C# (`AgendaCobranca.Sdk`).  
   Cada SDK assina a request e fala HTTPS direto com a API, ou com a URL de um gateway se `base_url` apontar para ele.

```
App do cliente
    → SDK (Ruby | Go | C#)
        → HTTPS + HMAC-SHA256
            → API (https://api.agendacobranca.example/v1)
            → ou Security Gateway (quando configurado)
```

Nesta entrega os SDKs **não** embutem o gateway. Eles só conhecem `base_url`.

## Credenciais

| Campo | Onde vive | Transporte |
| --- | --- | --- |
| `client_id` | configuração | header `X-Client-Id` |
| `api_key` | configuração | header `X-Api-Key` |
| `client_secret` | configuração, só memória | **nunca** em header/plain; só como chave HMAC |

## Canonical string (idêntica nas três linguagens)

```
canonical_string = METHOD + "\n" + PATH + "\n" + TIMESTAMP + "\n" + NONCE + "\n" + HASH_SHA256(REQUEST_BODY)
signature        = HMAC_SHA256(canonical_string, CLIENT_SECRET)
```

Regras:

- `METHOD` em maiúsculas (`GET`, `POST`).
- `PATH` é o path absoluto da URL, sem query string (ex.: `/v1/cobrancas`).
- `TIMESTAMP` é Unix em segundos (string decimal).
- `NONCE` é UUIDv4.
- `HASH_SHA256(REQUEST_BODY)` é hex minúsculo. Body vazio (GET/cancel) usa SHA-256 de `""`.
- `X-Signature` é HMAC-SHA256 em hex minúsculo.
- Tolerância de relógio no servidor: 300s (validação no gateway/API, não no SDK).

Vetores compartilhados: [`docs/hmac-test-vectors.md`](hmac-test-vectors.md).

## Endpoints cobertos (não inventar outros)

| Método SDK | HTTP | Path relativo a `/v1` |
| --- | --- | --- |
| `Cobrancas.Create` | `POST` | `/cobrancas` (+ `Idempotency-Key`) |
| `Cobrancas.Find` | `GET` | `/cobrancas/{id}` |
| `Cobrancas.List` | `GET` | `/cobrancas` |
| `Cobrancas.Cancel` | `POST` | `/cobrancas/{id}/cancel` |
| `Licenses.Verify` | `POST` | `/licenses/verify` (opcional na inicialização) |
| `Webhooks.Verify` | — | verificação **local** da mesma canonical string |

## Domínio Cobrança

- `id` (UUID)
- `external_reference`
- `valor_centavos` (int64)
- `vencimento` (`YYYY-MM-DD`)
- `status`: `pendente` \| `paga` \| `vencida` \| `cancelada`
- `pagador`: `documento`, `nome`, `email`
- `juros`, `multa`

## Onde cada SDK assina

| SDK | Ponto de injeção |
| --- | --- |
| Ruby | Faraday middleware `Security::SigningMiddleware` |
| Go | `http.RoundTripper` em `security.go` |
| C# | `SigningDelegatingHandler` + `AddAgendaCobranca` / `IHttpClientFactory` |

## Etapa 2 (fora desta entrega)

O Security Gateway interno passa a ser o hop obrigatório: emite tokens efêmeros, confere licença e HMAC, e encaminha para `agenda_cobranca_api`. Os thin clients desta entrega já aceitam apontar `base_url` para essa URL quando ela existir.
