import { describe, expect, it } from "vitest";
import { canonicalString, hashBody, Signer } from "../src/signer.js";
import { ConfigurationError } from "../src/errors.js";

const SECRET = "test_client_secret";
const TIMESTAMP = 1_700_000_000;
const NONCE = "550e8400-e29b-41d4-a716-446655440000";
const POST_BODY = '{"external_reference":"pedido-1","valor_centavos":15000}';

function signer(): Signer {
  return new Signer(SECRET, () => TIMESTAMP, () => NONCE);
}

describe("Signer — vetores HMAC compartilhados", () => {
  it("monta METHOD, PATH, TIMESTAMP, NONCE e HASH_SHA256 do body", () => {
    const canonical = canonicalString("POST", "/v1/cobrancas", TIMESTAMP, NONCE, POST_BODY);
    expect(canonical).toBe(
      "POST\n/v1/cobrancas\n1700000000\n550e8400-e29b-41d4-a716-446655440000\n" +
        "d453a65109b62169820d03d496180180c9ffd50248be209887b3786f374d0a75",
    );
    expect(hashBody(POST_BODY)).toBe("d453a65109b62169820d03d496180180c9ffd50248be209887b3786f374d0a75");
  });

  it("usa SHA256 de string vazia quando o body e nil", () => {
    expect(hashBody("")).toBe("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    expect(hashBody(null)).toBe("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    const canonical = canonicalString("GET", "/v1/cobrancas/abc", TIMESTAMP, NONCE, null);
    expect(canonical.endsWith("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")).toBe(true);
  });

  it("produz a assinatura HMAC-SHA256 do vetor POST /v1/cobrancas", () => {
    const resultado = signer().sign("post", "/v1/cobrancas", POST_BODY);
    expect(resultado.timestamp).toBe("1700000000");
    expect(resultado.nonce).toBe(NONCE);
    expect(resultado.signature).toBe("f0334aadd5c24c375365a83cf301e8c39a5686f7680b0be51f84c95836cb60a3");
  });

  it("assina GET sem body com o vetor compartilhado", () => {
    const resultado = signer().sign("GET", "/v1/cobrancas/abc", "");
    expect(resultado.signature).toBe("19eadbfc80ca376521472ef1297e4513e5f8ef17dedcee5e88cc34a6492349c9");
  });

  it("aceita assinatura valida e rejeita adulteracao", () => {
    const s = signer();
    const assinatura = s.signatureFor("POST", "/v1/cobrancas", TIMESTAMP, NONCE, POST_BODY);
    expect(s.validSignature("POST", "/v1/cobrancas", String(TIMESTAMP), NONCE, assinatura, POST_BODY)).toBe(true);
    expect(
      s.validSignature("POST", "/v1/cobrancas", String(TIMESTAMP), NONCE, assinatura, '{"valor_centavos":1}'),
    ).toBe(false);
  });

  it("nao aceita secret vazio", () => {
    expect(() => new Signer("")).toThrow(ConfigurationError);
  });
});
