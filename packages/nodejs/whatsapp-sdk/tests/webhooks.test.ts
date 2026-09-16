import { describe, expect, it } from "vitest";
import { SignatureError, ValidationError } from "../src/errors.js";
import { OrdexPayEntitlementChecker, StaticEntitlementChecker } from "../src/entitlement.js";
import {
  metaSignatureHeader,
  parseInboundMessages,
  verifyMetaSignature,
  verifyMetaSignatureOrThrow,
  verifyWebhookChallenge,
} from "../src/webhooks.js";
import type { FetchLike } from "../src/types.js";

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("webhooks Meta", () => {
  it("devolve hub.challenge no GET de verificacao", () => {
    const challenge = verifyWebhookChallenge(
      { "hub.mode": "subscribe", "hub.verify_token": "segredo", "hub.challenge": "12345" },
      "segredo",
    );
    expect(challenge).toBe("12345");
  });

  it("rejeita verify_token errado", () => {
    expect(() =>
      verifyWebhookChallenge({ "hub.mode": "subscribe", "hub.verify_token": "x", "hub.challenge": "1" }, "segredo"),
    ).toThrow(ValidationError);
  });

  it("valida X-Hub-Signature-256", () => {
    const body = '{"object":"whatsapp_business_account"}';
    const header = metaSignatureHeader(body, "app_secret_exemplo");
    expect(verifyMetaSignature(body, header, "app_secret_exemplo")).toBe(true);
    expect(verifyMetaSignature(body, header, "outro")).toBe(false);
    expect(() => verifyMetaSignatureOrThrow("{}", header, "app_secret_exemplo")).toThrow(SignatureError);
  });

  it("parseia mensagens inbound", () => {
    const payload = {
      entry: [
        {
          changes: [
            {
              value: {
                messages: [{ from: "5511", id: "wamid.1", timestamp: "1", type: "text", text: { body: "pix" } }],
              },
            },
          ],
        },
      ],
    };
    const msgs = parseInboundMessages(payload);
    expect(msgs).toHaveLength(1);
    expect(msgs[0]?.text).toBe("pix");
    expect(msgs[0]?.from).toBe("5511");
  });
});

describe("OrdexPayEntitlementChecker", () => {
  it("usa POST /addons/whatsapp/entitlement quando existe", async () => {
    const fetchImpl: FetchLike = (async (input) => {
      const url = new URL(String(input));
      expect(url.pathname).toContain("/addons/whatsapp/entitlement");
      return jsonResponse(200, { data: { enabled: true, plan: "saas" } });
    }) as FetchLike;
    const checker = new OrdexPayEntitlementChecker("chave", { fetch: fetchImpl });
    const result = await checker.check("acme");
    expect(result.enabled).toBe(true);
    expect(result.plan).toBe("saas");
  });

  it("faz fallback para licenses/verify e le addons.whatsapp", async () => {
    const fetchImpl: FetchLike = (async (input) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith("/addons/whatsapp/entitlement")) {
        return jsonResponse(404, { message: "not found" });
      }
      return jsonResponse(200, { success: true, data: { valid: true, addons: { whatsapp: { enabled: true } } } });
    }) as FetchLike;
    const checker = new OrdexPayEntitlementChecker("chave", { fetch: fetchImpl });
    const result = await checker.check("acme");
    expect(result.enabled).toBe(true);
  });

  it("StaticEntitlementChecker cobre o mock de demo", async () => {
    const on = await new StaticEntitlementChecker(true).check("t");
    const off = await new StaticEntitlementChecker(false).check("t");
    expect(on.enabled).toBe(true);
    expect(off.enabled).toBe(false);
    expect(on.addOn).toBe("whatsapp");
  });
});
