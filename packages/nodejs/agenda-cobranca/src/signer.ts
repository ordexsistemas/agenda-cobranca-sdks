import { createHash, createHmac, randomUUID, timingSafeEqual } from "node:crypto";
import { ConfigurationError } from "./errors.js";
import type { SignedHeaders } from "./types.js";

export type BodyInput = string | Uint8Array | null | undefined;

export function bodyBytes(body: BodyInput): Buffer {
  if (body == null) {
    return Buffer.alloc(0);
  }
  if (typeof body === "string") {
    return Buffer.from(body, "utf8");
  }
  return Buffer.from(body);
}

export function hashBody(body: BodyInput = ""): string {
  return createHash("sha256").update(bodyBytes(body)).digest("hex");
}

export function canonicalString(
  method: string,
  path: string,
  timestamp: string | number,
  nonce: string,
  body: BodyInput = "",
): string {
  return [
    method.toString().toUpperCase(),
    path.toString(),
    timestamp.toString(),
    nonce.toString(),
    hashBody(body),
  ].join("\n");
}

export class Signer {
  private readonly clientSecret: string;
  private readonly clock: () => number;
  private readonly nonceGenerator: () => string;

  constructor(
    clientSecret: string,
    clock: () => number = () => Math.floor(Date.now() / 1000),
    nonceGenerator: () => string = () => randomUUID(),
  ) {
    if (!clientSecret || clientSecret.trim() === "") {
      throw new ConfigurationError("client_secret e obrigatorio");
    }
    this.clientSecret = clientSecret;
    this.clock = clock;
    this.nonceGenerator = nonceGenerator;
  }

  signatureFor(
    method: string,
    path: string,
    timestamp: string | number,
    nonce: string,
    body: BodyInput = "",
  ): string {
    const canonical = canonicalString(method, path, timestamp, nonce, body);
    return createHmac("sha256", this.clientSecret).update(canonical, "utf8").digest("hex");
  }

  sign(method: string, path: string, body: BodyInput = ""): SignedHeaders {
    const timestamp = this.clock().toString();
    const nonce = this.nonceGenerator().toString();
    return {
      timestamp,
      nonce,
      signature: this.signatureFor(method, path, timestamp, nonce, body),
      bodyHash: hashBody(body),
    };
  }

  validSignature(
    method: string,
    path: string,
    timestamp: string,
    nonce: string,
    signature: string,
    body: BodyInput = "",
  ): boolean {
    const expected = this.signatureFor(method, path, timestamp, nonce, body);
    const left = Buffer.from(expected, "utf8");
    const right = Buffer.from(signature ?? "", "utf8");
    if (left.length !== right.length) {
      return false;
    }
    return timingSafeEqual(left, right);
  }
}
