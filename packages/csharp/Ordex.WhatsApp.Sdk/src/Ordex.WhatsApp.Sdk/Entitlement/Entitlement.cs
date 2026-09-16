using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ordex.WhatsApp.Sdk.Entitlement;

public sealed class StaticEntitlementChecker : IEntitlementChecker
{
    private readonly bool _enabled;
    private readonly string? _plan;
    private readonly string? _message;

    public StaticEntitlementChecker(bool enabled, string? plan = "saas", string? message = null)
    {
        _enabled = enabled;
        _plan = plan;
        _message = message;
    }

    public Task<EntitlementResult> CheckAsync(string tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new EntitlementResult(_enabled, tenantId, "whatsapp", _plan ?? "saas", _message));
}

/// <summary>
/// Checagem plugável contra Ordex Pay.
/// 1. POST /addons/whatsapp/entitlement
/// 2. Se 404, fallback POST /licenses/verify e lê addons.whatsapp.
/// </summary>
public sealed class OrdexPayEntitlementChecker : IEntitlementChecker
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly bool _treatValidLicenseAsAddon;
    private readonly string? _clientId;

    public OrdexPayEntitlementChecker(
        string apiKey,
        HttpClient http,
        string? clientId = null,
        bool treatValidLicenseAsAddon = false)
    {
        _apiKey = apiKey;
        _http = http;
        _clientId = clientId;
        _treatValidLicenseAsAddon = treatValidLicenseAsAddon;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(AgendaCobranca.Sdk.AgendaCobrancaOptions.DefaultBaseUrl.TrimEnd('/') + "/");
        }
    }

    public async Task<EntitlementResult> CheckAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await PostAsync("addons/whatsapp/entitlement", new
            {
                tenant_id = tenantId,
                client_id = _clientId ?? ""
            }, cancellationToken).ConfigureAwait(false);
            return FromPayload(tenantId, payload, "addons/whatsapp/entitlement");
        }
        catch (WhatsAppNotFoundException)
        {
        }

        var license = await PostAsync("licenses/verify", new
        {
            client_id = _clientId ?? "",
            tenant_id = tenantId
        }, cancellationToken).ConfigureAwait(false);
        return FromLicense(tenantId, license);
    }

    internal static async Task<EntitlementResult> AssertEnabledAsync(
        IEntitlementChecker? checker,
        string tenantId,
        bool skip,
        CancellationToken cancellationToken = default)
    {
        if (skip)
        {
            return new EntitlementResult(true, tenantId, "whatsapp", "demo", "skipEntitlementCheck");
        }

        if (checker is null)
        {
            throw new WhatsAppEntitlementException(
                "EntitlementChecker ausente: injete StaticEntitlementChecker (demo) ou OrdexPayEntitlementChecker",
                tenantId);
        }

        var result = await checker.CheckAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!result.Enabled)
        {
            throw new WhatsAppEntitlementException(
                result.Message ??
                "Add-on WhatsApp do plano SaaS Ordex Pay nao esta habilitado para este tenant",
                tenantId);
        }

        return result;
    }

    private async Task<JsonElement> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions.Default), Encoding.UTF8, "application/json")
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.TryAddWithoutValidation("chave_api", _apiKey);
        message.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return Parse(raw);
        }

        var rec = Parse(raw);
        var msg = rec.TryGetProperty("message", out var m) ? m.GetString()
            : rec.TryGetProperty("error", out var e) ? e.GetString()
            : $"Erro HTTP {(int)response.StatusCode}";
        throw ErrorFromStatus.Create((int)response.StatusCode, msg ?? $"Erro HTTP {(int)response.StatusCode}", raw);
    }

    private static EntitlementResult FromPayload(string tenantId, JsonElement payload, string source)
    {
        var dados = Unwrap(payload);
        var enabled = IsTruthy(dados, "enabled") || IsTruthy(dados, "valid") ||
                      IsTruthy(payload, "enabled") || IsTruthy(payload, "valid") ||
                      AddonFlag(dados) || AddonFlag(payload);
        var message = GetString(dados, "message") ?? GetString(payload, "message") ?? source;
        var plan = GetString(dados, "plan") ?? GetString(payload, "plan") ?? "saas";
        return new EntitlementResult(enabled, tenantId, "whatsapp", plan, message, payload.Clone());
    }

    private EntitlementResult FromLicense(string tenantId, JsonElement payload)
    {
        var dados = Unwrap(payload);
        var licenseValid = IsTruthy(dados, "valid") || IsTruthy(payload, "valid") || IsTruthy(payload, "success");
        var addon = AddonFlag(dados) || AddonFlag(payload);
        var enabled = addon || (_treatValidLicenseAsAddon && licenseValid);
        var message = enabled
            ? "add-on whatsapp autorizado via licenses/verify"
            : "licenses/verify sem add-on whatsapp habilitado";
        return new EntitlementResult(enabled, tenantId, "whatsapp", "saas", message, payload.Clone());
    }

    private static JsonElement Unwrap(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object)
        {
            return data;
        }

        return payload;
    }

    private static bool AddonFlag(JsonElement rec)
    {
        if (rec.ValueKind != JsonValueKind.Object) return false;
        if (IsTruthy(rec, "whatsapp_addon") || IsTruthy(rec, "addon_whatsapp") || IsTruthy(rec, "whatsapp"))
        {
            return true;
        }

        if (!rec.TryGetProperty("addons", out var addons) || addons.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!addons.TryGetProperty("whatsapp", out var wa)) return false;
        if (IsTruthyValue(wa)) return true;
        if (wa.ValueKind == JsonValueKind.Object)
        {
            return IsTruthy(wa, "enabled") || IsTruthy(wa, "valid");
        }

        return false;
    }

    private static bool IsTruthy(JsonElement rec, string name) =>
        rec.ValueKind == JsonValueKind.Object && rec.TryGetProperty(name, out var el) && IsTruthyValue(el);

    private static bool IsTruthyValue(JsonElement el) =>
        el.ValueKind == JsonValueKind.True ||
        (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n) && n == 1) ||
        (el.ValueKind == JsonValueKind.String && (el.GetString() is "true" or "1"));

    private static string? GetString(JsonElement rec, string name) =>
        rec.ValueKind == JsonValueKind.Object && rec.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static JsonElement Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return JsonDocument.Parse("{}").RootElement.Clone();
        try
        {
            return JsonDocument.Parse(raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse($"{{\"raw\":{JsonSerializer.Serialize(raw)}}}").RootElement.Clone();
        }
    }
}
