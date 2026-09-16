# Vetores HMAC compartilhados

Use estes valores nos testes das quatro linguagens. Qualquer divergência significa canonical string diferente.

## Constantes

```
CLIENT_SECRET = test_client_secret
TIMESTAMP     = 1700000000
NONCE         = 550e8400-e29b-41d4-a716-446655440000
```

## POST /v1/cobrancas

```
BODY = {"external_reference":"pedido-1","valor_centavos":15000}
HASH_SHA256(BODY) = d453a65109b62169820d03d496180180c9ffd50248be209887b3786f374d0a75

canonical_string =
POST
/v1/cobrancas
1700000000
550e8400-e29b-41d4-a716-446655440000
d453a65109b62169820d03d496180180c9ffd50248be209887b3786f374d0a75

X-Signature = f0334aadd5c24c375365a83cf301e8c39a5686f7680b0be51f84c95836cb60a3
```

## GET /v1/cobrancas/abc (body vazio)

```
HASH_SHA256("") = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
X-Signature     = 19eadbfc80ca376521472ef1297e4513e5f8ef17dedcee5e88cc34a6492349c9
```

## POST /v1/webhooks

```
BODY = {"id":"evt-1"}
HASH_SHA256(BODY) = 1b3b9ad33f5bac2567e961731f4a9af2617ea39aee9d240ec70e45dec92b7371
X-Signature       = bcf2bd3d052eaaa22c83a552dc1cdbf33e9613fa34196f6920dcfa02db0052f6
```
