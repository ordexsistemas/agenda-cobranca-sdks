import { EntitlementError, errorFromStatus, NotFoundError } from "./errors.js";
import { asRecord, decodeBody, isTruthy, unwrapData } from "./models.js";
import { DEFAULT_ORDEX_BASE_URL } from "./types.js";
import type { EntitlementChecker, EntitlementResult, FetchLike } from "./types.js";

export class StaticEntitlementChecker implements EntitlementChecker {
  constructor(
    private readonly enabled: boolean,
    private readonly extras: { plan?: string; message?: string } = {},
  ) {}

  async check(tenantId: string): Promise<EntitlementResult> {
    return {
      enabled: this.enabled,
      tenantId,
      addOn: "whatsapp",
      plan: this.extras.plan ?? "saas",
      message: this.extras.message,
    };
  }
}

/**
 * Checagem plugável contra Ordex Pay.
 *
 * 1. `POST /addons/whatsapp/entitlement` (endpoint alvo do add-on SaaS).
 * 2. Se 404, fallback para `POST /licenses/verify` e lê `addons.whatsapp`.
 *
 * TODO(ordex): confirmar path e payload finais da API de add-ons.
 */
export class OrdexPayEntitlementChecker implements EntitlementChecker {
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;
  private readonly timeoutMs: number;

  constructor(
    private readonly apiKey: string,
    options: {
      baseUrl?: string;
      fetch?: FetchLike;
      timeoutMs?: number;
      clientId?: string;
      treatValidLicenseAsAddon?: boolean;
    } = {},
  ) {
    this.baseUrl = (options.baseUrl?.trim() || DEFAULT_ORDEX_BASE_URL).replace(/\/+$/, "") + "/";
    this.fetchImpl = options.fetch ?? fetch;
    this.timeoutMs = options.timeoutMs ?? 30_000;
    this.clientId = options.clientId;
    this.treatValidLicenseAsAddon = options.treatValidLicenseAsAddon ?? false;
  }

  private readonly clientId?: string;
  private readonly treatValidLicenseAsAddon: boolean;

  async check(tenantId: string): Promise<EntitlementResult> {
    try {
      const payload = await this.post("addons/whatsapp/entitlement", {
        tenant_id: tenantId,
        client_id: this.clientId ?? "",
      });
      return this.fromPayload(tenantId, payload, "addons/whatsapp/entitlement");
    } catch (err) {
      if (!(err instanceof NotFoundError)) throw err;
    }

    const license = await this.post("licenses/verify", {
      client_id: this.clientId ?? "",
      tenant_id: tenantId,
    });
    return this.fromLicense(tenantId, license);
  }

  async assertEnabled(tenantId: string): Promise<EntitlementResult> {
    const result = await this.check(tenantId);
    if (!result.enabled) {
      throw new EntitlementError(
        result.message ??
          "Add-on WhatsApp do plano SaaS Ordex Pay nao esta habilitado para este tenant",
        tenantId,
      );
    }
    return result;
  }

  private fromPayload(tenantId: string, payload: unknown, source: string): EntitlementResult {
    const rec = asRecord(payload);
    const dados = unwrapData(payload);
    const enabled =
      isTruthy(dados.enabled) ||
      isTruthy(dados.valid) ||
      isTruthy(rec.enabled) ||
      isTruthy(rec.valid) ||
      addonFlag(dados) ||
      addonFlag(rec);
    const message =
      dados.message == null
        ? rec.message == null
          ? undefined
          : String(rec.message)
        : String(dados.message);
    const plan =
      dados.plan == null ? (rec.plan == null ? "saas" : String(rec.plan)) : String(dados.plan);
    return { enabled, tenantId, addOn: "whatsapp", plan, message: message ?? source, raw: payload };
  }

  private fromLicense(tenantId: string, payload: unknown): EntitlementResult {
    const rec = asRecord(payload);
    const dados = unwrapData(payload);
    const licenseValid = isTruthy(dados.valid) || isTruthy(rec.valid) || isTruthy(rec.success);
    const addon = addonFlag(dados) || addonFlag(rec);
    const enabled = addon || (this.treatValidLicenseAsAddon && licenseValid);
    const message = enabled
      ? "add-on whatsapp autorizado via licenses/verify"
      : "licenses/verify sem add-on whatsapp habilitado";
    return { enabled, tenantId, addOn: "whatsapp", plan: "saas", message, raw: payload };
  }

  private async post(path: string, body: Record<string, unknown>): Promise<unknown> {
    const url = new URL(path.replace(/^\/+/, ""), this.baseUrl);
    const headers = new Headers();
    headers.set("Accept", "application/json");
    headers.set("Content-Type", "application/json");
    headers.set("chave_api", this.apiKey);
    headers.set("X-Api-Key", this.apiKey);

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    let response: Response;
    try {
      response = await this.fetchImpl(url, {
        method: "POST",
        headers,
        body: JSON.stringify(body),
        signal: controller.signal,
      });
    } finally {
      clearTimeout(timer);
    }

    const payload = decodeBody(await response.text());
    if (response.ok) return payload;

    const rec = asRecord(payload);
    const message =
      (typeof rec.message === "string" && rec.message) ||
      (typeof rec.error === "string" && rec.error) ||
      `Erro HTTP ${response.status}`;
    throw errorFromStatus(response.status, message, payload, rec.errors ?? rec.error ?? null);
  }
}

function addonFlag(rec: Record<string, unknown>): boolean {
  if (isTruthy(rec.whatsapp_addon) || isTruthy(rec.addon_whatsapp) || isTruthy(rec.whatsapp)) {
    return true;
  }
  const addons = rec.addons;
  if (addons && typeof addons === "object" && !Array.isArray(addons)) {
    const wa = (addons as Record<string, unknown>).whatsapp;
    if (isTruthy(wa)) return true;
    if (wa && typeof wa === "object") {
      const inner = wa as Record<string, unknown>;
      return isTruthy(inner.enabled) || isTruthy(inner.valid);
    }
  }
  return false;
}

export async function assertEntitlement(
  checker: EntitlementChecker | undefined,
  tenantId: string,
  skip: boolean,
): Promise<EntitlementResult | undefined> {
  if (skip) {
    return { enabled: true, tenantId, addOn: "whatsapp", plan: "demo", message: "skipEntitlementCheck" };
  }
  if (!checker) {
    throw new EntitlementError(
      "EntitlementChecker ausente: injete StaticEntitlementChecker (demo) ou OrdexPayEntitlementChecker",
      tenantId,
    );
  }
  const result = await checker.check(tenantId);
  if (!result.enabled) {
    throw new EntitlementError(
      result.message ??
        "Add-on WhatsApp do plano SaaS Ordex Pay nao esta habilitado para este tenant",
      tenantId,
    );
  }
  return result;
}
