import { randomUUID } from "node:crypto";
import type { Client } from "./client.js";
import { cobrancaFromResponse, compact, listFromResponse } from "./models.js";
import { ValidationError } from "./errors.js";
import type { Cobranca, CreateCobrancaInput, ListCobrancasInput, ListCobrancasResult } from "./types.js";

export class CobrancasResource {
  constructor(private readonly client: Client) {}

  async create(input: CreateCobrancaInput): Promise<Cobranca> {
    const idempotencyKey = input.idempotencyKey?.trim() || randomUUID();
    const body = compact({
      external_reference: input.externalReference,
      valor_centavos: input.valorCentavos,
      vencimento: input.vencimento,
      pagador: compact({
        documento: input.pagador?.documento,
        nome: input.pagador?.nome,
        email: input.pagador?.email,
      }),
      juros:
        input.juros == null
          ? undefined
          : compact({ percentual_mes: input.juros.percentualMes }),
      multa: input.multa == null ? undefined : compact({ percentual: input.multa.percentual }),
    });

    const payload = await this.client.request("POST", "cobrancas", {
      body,
      headers: { "Idempotency-Key": idempotencyKey },
    });
    return cobrancaFromResponse(payload);
  }

  async find(id: string): Promise<Cobranca> {
    if (!id || id.trim() === "") {
      throw new ValidationError("id e obrigatorio", 400);
    }
    const payload = await this.client.request("GET", `cobrancas/${id}`);
    return cobrancaFromResponse(payload);
  }

  async list(filtros: ListCobrancasInput = {}): Promise<ListCobrancasResult> {
    const query: Record<string, string> = {};
    if (filtros.status) query.status = filtros.status;
    if (filtros.externalReference) query.external_reference = filtros.externalReference;
    if (filtros.page != null) query.page = String(filtros.page);
    if (filtros.perPage != null) query.per_page = String(filtros.perPage);

    const payload = await this.client.request("GET", "cobrancas", { query });
    return listFromResponse(payload);
  }

  async cancel(id: string): Promise<Cobranca> {
    if (!id || id.trim() === "") {
      throw new ValidationError("id e obrigatorio", 400);
    }
    const payload = await this.client.request("POST", `cobrancas/${id}/cancel`);
    return cobrancaFromResponse(payload);
  }
}
