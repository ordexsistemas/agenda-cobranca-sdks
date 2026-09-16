import { describe, expect, it } from "vitest";
import { SignatureError } from "../src/errors.js";
import { verifyWebhook, verifyWebhookOrThrow, WebhookVerifier } from "../src/webhooks.js";

const SECRET = "test_client_secret";
const PAYLOAD = '{"id":"evt-1"}';
const HEADERS = {
  "X-Timestamp": "1700000000",
  "X-Nonce": "550e8400-e29b-41d4-a716-446655440000",
  "X-Signature": "bcf2bd3d052eaaa22c83a552dc1cdbf33e9613fa34196f6920dcfa02db0052f6",
};

describe("Webhooks.Verify — vetor compartilhado", () => {
  it("valida webhook com a mesma canonical string dos SDKs", () => {
    expect(new WebhookVerifier(SECRET).verify(PAYLOAD, HEADERS)).toBe(true);
    expect(verifyWebhook(PAYLOAD, HEADERS, SECRET)).toBe(true);
  });

  it("rejeita payload adulterado", () => {
    expect(new WebhookVerifier(SECRET).verify('{"id":"evt-2"}', HEADERS)).toBe(false);
  });

  it("aceita headers em caixa baixa", () => {
    expect(
      verifyWebhook(PAYLOAD, {
        "x-timestamp": HEADERS["X-Timestamp"],
        "x-nonce": HEADERS["X-Nonce"],
        "x-signature": HEADERS["X-Signature"],
      }, SECRET),
    ).toBe(true);
  });

  it("levanta SignatureError em verifyOrThrow quando invalido", () => {
    expect(() => verifyWebhookOrThrow("{}", HEADERS, SECRET)).toThrow(SignatureError);
  });
});
