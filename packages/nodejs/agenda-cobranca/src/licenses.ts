import type { Client } from "./client.js";
import { asRecord, unwrapData } from "./models.js";
import type { LicenseVerifyResult } from "./types.js";

function isTruthy(value: unknown): boolean {
  return value === true || value === "true";
}

export class LicensesResource {
  constructor(private readonly client: Client) {}

  async verify(): Promise<LicenseVerifyResult> {
    const payload = await this.client.request("POST", "licenses/verify", {
      body: { client_id: this.client.options.clientId ?? "" },
    });
    const rec = asRecord(payload);
    const dados = unwrapData(payload);
    const valid = isTruthy(dados.valid) || isTruthy(rec.valid) || isTruthy(rec.success);
    const message =
      dados.message == null
        ? rec.message == null
          ? undefined
          : String(rec.message)
        : String(dados.message);
    return { valid, message, raw: payload };
  }
}
