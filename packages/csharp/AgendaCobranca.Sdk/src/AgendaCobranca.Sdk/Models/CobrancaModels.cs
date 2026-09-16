using System.Text.Json.Serialization;

namespace AgendaCobranca.Sdk.Models;

public sealed record Pagador(
    [property: JsonPropertyName("documento")] string Documento,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("email")] string? Email = null);

public sealed record Juros(
    [property: JsonPropertyName("percentual_mes")] decimal? PercentualMes = null);

public sealed record Multa(
    [property: JsonPropertyName("percentual")] decimal? Percentual = null);

public sealed record CreateCobrancaRequest(
    [property: JsonPropertyName("valor_centavos")] long ValorCentavos,
    [property: JsonPropertyName("vencimento")] string Vencimento,
    [property: JsonPropertyName("pagador")] Pagador Pagador,
    [property: JsonPropertyName("external_reference")] string? ExternalReference = null,
    [property: JsonPropertyName("juros")] Juros? Juros = null,
    [property: JsonPropertyName("multa")] Multa? Multa = null,
    string? IdempotencyKey = null);

public sealed record Cobranca(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("valor_centavos")] long ValorCentavos,
    [property: JsonPropertyName("vencimento")] string Vencimento,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("pagador")] Pagador Pagador,
    [property: JsonPropertyName("external_reference")] string? ExternalReference = null,
    [property: JsonPropertyName("juros")] Juros? Juros = null,
    [property: JsonPropertyName("multa")] Multa? Multa = null);

public sealed record ListCobrancasRequest(
    string? Status = null,
    string? ExternalReference = null,
    int? Page = null,
    int? PerPage = null);

public sealed record ListMeta(
    [property: JsonPropertyName("page")] int? Page = null,
    [property: JsonPropertyName("per_page")] int? PerPage = null,
    [property: JsonPropertyName("total")] int? Total = null);

public sealed record ListCobrancasResponse(
    IReadOnlyList<Cobranca> Data,
    ListMeta? Meta = null);

public sealed record LicenseVerifyResponse(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("message")] string? Message = null);
