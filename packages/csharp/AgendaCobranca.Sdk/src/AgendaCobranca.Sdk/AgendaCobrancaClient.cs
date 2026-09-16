using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgendaCobranca.Sdk.Models;
using Microsoft.Extensions.Options;

namespace AgendaCobranca.Sdk;

public sealed class AgendaCobrancaClient : IAgendaCobrancaClient
{
    private readonly HttpClient _http;
    private readonly AgendaCobrancaOptions _options;

    public AgendaCobrancaClient(HttpClient http, IOptions<AgendaCobrancaOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.Timeout = _options.Timeout;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = BaseAddress(_options.BaseUrl);
        }
        if (_http.DefaultRequestHeaders.Accept.Count == 0)
        {
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public static Uri BaseAddress(string baseUrl)
    {
        var normalizado = baseUrl.TrimEnd('/') + "/";
        return new Uri(normalizado);
    }

    public async Task<Cobranca> CreateCobrancaAsync(CreateCobrancaRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new CreateCobrancaBody(
            request.ValorCentavos,
            request.Vencimento,
            request.Pagador,
            request.ExternalReference,
            request.Juros,
            request.Multa);
        var idem = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString()
            : request.IdempotencyKey;

        using var mensagem = new HttpRequestMessage(HttpMethod.Post, "cobrancas")
        {
            Content = JsonContent(payload)
        };
        mensagem.Headers.TryAddWithoutValidation("Idempotency-Key", idem);
        return await SendAndReadCobrancaAsync(mensagem, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Cobranca> FindCobrancaAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new AgendaCobrancaValidationException("id e obrigatorio", 400);
        }

        using var mensagem = new HttpRequestMessage(HttpMethod.Get, $"cobrancas/{id}");
        return await SendAndReadCobrancaAsync(mensagem, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ListCobrancasResponse> ListCobrancasAsync(ListCobrancasRequest? request = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(request?.Status)) query.Add($"status={Uri.EscapeDataString(request.Status)}");
        if (!string.IsNullOrWhiteSpace(request?.ExternalReference)) query.Add($"external_reference={Uri.EscapeDataString(request.ExternalReference)}");
        if (request?.Page is int page) query.Add($"page={page}");
        if (request?.PerPage is int perPage) query.Add($"per_page={perPage}");

        var path = query.Count == 0 ? "cobrancas" : "cobrancas?" + string.Join("&", query);
        using var mensagem = new HttpRequestMessage(HttpMethod.Get, path);
        using var resposta = await _http.SendAsync(mensagem, cancellationToken).ConfigureAwait(false);
        var raw = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        GarantirSucesso(resposta, raw);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        var root = doc.RootElement;
        var itens = new List<Cobranca>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            itens.AddRange(root.Deserialize<List<Cobranca>>(JsonOptions.Default) ?? []);
        }
        else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            itens.AddRange(data.Deserialize<List<Cobranca>>(JsonOptions.Default) ?? []);
        }

        ListMeta? meta = null;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("meta", out var metaEl))
            {
                meta = metaEl.Deserialize<ListMeta>(JsonOptions.Default);
            }
            else
            {
                meta = new ListMeta(
                    root.TryGetProperty("page", out var p) ? p.GetInt32() : null,
                    root.TryGetProperty("per_page", out var pp) ? pp.GetInt32() : null,
                    root.TryGetProperty("total", out var t) ? t.GetInt32() : null);
            }
        }

        return new ListCobrancasResponse(itens, meta);
    }

    public async Task<Cobranca> CancelCobrancaAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new AgendaCobrancaValidationException("id e obrigatorio", 400);
        }

        using var mensagem = new HttpRequestMessage(HttpMethod.Post, $"cobrancas/{id}/cancel");
        return await SendAndReadCobrancaAsync(mensagem, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LicenseVerifyResponse> VerifyLicenseAsync(CancellationToken cancellationToken = default)
    {
        using var mensagem = new HttpRequestMessage(HttpMethod.Post, "licenses/verify")
        {
            Content = JsonContent(new { client_id = _options.ClientId })
        };
        using var resposta = await _http.SendAsync(mensagem, cancellationToken).ConfigureAwait(false);
        var raw = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        GarantirSucesso(resposta, raw);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        var root = doc.RootElement;
        var dados = root.TryGetProperty("data", out var data) ? data : root;
        var valid = (dados.TryGetProperty("valid", out var validEl) && validEl.ValueKind == JsonValueKind.True)
                    || (root.TryGetProperty("success", out var successEl) && successEl.ValueKind == JsonValueKind.True);
        var message = dados.TryGetProperty("message", out var msgEl) ? msgEl.GetString()
            : root.TryGetProperty("message", out var rootMsg) ? rootMsg.GetString() : null;
        return new LicenseVerifyResponse(valid, message);
    }

    private async Task<Cobranca> SendAndReadCobrancaAsync(HttpRequestMessage mensagem, CancellationToken cancellationToken)
    {
        using var resposta = await _http.SendAsync(mensagem, cancellationToken).ConfigureAwait(false);
        var raw = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        GarantirSucesso(resposta, raw);
        return LerCobranca(raw);
    }

    internal static Cobranca LerCobranca(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var dados = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            ? data
            : root;
        return dados.Deserialize<Cobranca>(JsonOptions.Default)
               ?? throw new AgendaCobrancaException("Resposta de cobranca vazia");
    }

    internal static void GarantirSucesso(HttpResponseMessage resposta, string raw)
    {
        if (resposta.IsSuccessStatusCode) return;

        var status = (int)resposta.StatusCode;
        var message = ExtrairMensagem(raw) ?? $"Erro HTTP {status}";
        throw resposta.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AgendaCobrancaAuthenticationException(message, status, raw),
            HttpStatusCode.NotFound => new AgendaCobrancaNotFoundException(message, status, raw),
            HttpStatusCode.UnprocessableEntity => new AgendaCobrancaValidationException(message, status, raw),
            _ => new AgendaCobrancaApiException(message, status, raw)
        };
    }

    private static string? ExtrairMensagem(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("message", out var msg)) return msg.GetString();
            if (doc.RootElement.TryGetProperty("error", out var err)) return err.GetString();
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static StringContent JsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions.Default);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed record CreateCobrancaBody(
        long ValorCentavos,
        string Vencimento,
        Pagador Pagador,
        string? ExternalReference,
        Juros? Juros,
        Multa? Multa);
}
