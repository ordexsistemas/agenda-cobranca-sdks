using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ordex.WhatsApp.Sdk.Graph;

public sealed class GraphClient
{
    public const string DefaultBaseUrl = "https://graph.facebook.com";
    public const string DefaultVersion = "v21.0";

    private readonly HttpClient _http;
    private readonly string _accessToken;
    private readonly string _phoneNumberId;
    private readonly string? _wabaId;

    public GraphClient(HttpClient http, string accessToken, string phoneNumberId, string? wabaId = null)
    {
        _http = http;
        _accessToken = accessToken;
        _phoneNumberId = phoneNumberId;
        _wabaId = wabaId;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri($"{DefaultBaseUrl.TrimEnd('/')}/{DefaultVersion}/");
        }
    }

    public string PhoneNumberId => _phoneNumberId;
    public string? WabaId => _wabaId;

    public async Task<MetaMessageResult> SendMessageAsync(SendMessageInput input, CancellationToken cancellationToken = default)
    {
        var body = BuildMessageBody(input);
        var payload = await RequestAsync(HttpMethod.Post, $"{_phoneNumberId}/messages", body, cancellationToken)
            .ConfigureAwait(false);
        return ParseMessageResult(payload);
    }

    public Task<JsonElement> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_wabaId))
        {
            throw new WhatsAppConfigurationException("wabaId e obrigatorio para listar templates");
        }

        return RequestAsync(HttpMethod.Get, $"{_wabaId}/message_templates", null, cancellationToken);
    }

    internal static Dictionary<string, object?> BuildMessageBody(SendMessageInput input)
    {
        var body = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = input.To,
            ["type"] = input.Type == "template" ? "template" : input.Type
        };

        var callback = BuildCallbackData(input);
        if (callback is not null) body["biz_opaque_callback_data"] = callback;

        if (input.Type == "template")
        {
            if (string.IsNullOrWhiteSpace(input.Template?.Name))
            {
                throw new WhatsAppConfigurationException("template.name e obrigatorio para envio de template");
            }

            body["template"] = new Dictionary<string, object?>
            {
                ["name"] = input.Template!.Name,
                ["language"] = new Dictionary<string, object?> { ["code"] = string.IsNullOrWhiteSpace(input.Template.Language) ? "pt_BR" : input.Template.Language },
                ["components"] = input.Template.Components
            };
            return body;
        }

        if (input.Type == "text")
        {
            if (string.IsNullOrWhiteSpace(input.TextBody))
            {
                throw new WhatsAppConfigurationException("text.body e obrigatorio para mensagem de sessao");
            }

            body["text"] = new Dictionary<string, object?>
            {
                ["preview_url"] = input.PreviewUrl,
                ["body"] = input.TextBody
            };
            return body;
        }

        var media = input.Media;
        if (media is null || (string.IsNullOrWhiteSpace(media.Id) && string.IsNullOrWhiteSpace(media.Link)))
        {
            throw new WhatsAppConfigurationException("media.id ou media.link e obrigatorio");
        }

        var mediaBody = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(media.Id)) mediaBody["id"] = media.Id;
        if (!string.IsNullOrWhiteSpace(media.Link)) mediaBody["link"] = media.Link;
        if (!string.IsNullOrWhiteSpace(media.Caption)) mediaBody["caption"] = media.Caption;
        if (!string.IsNullOrWhiteSpace(media.Filename)) mediaBody["filename"] = media.Filename;
        body[input.Type] = mediaBody;
        return body;
    }

    private static string? BuildCallbackData(SendMessageInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.CallbackData))
        {
            return input.CallbackData.Length <= 512 ? input.CallbackData : input.CallbackData[..512];
        }

        var payload = JsonSerializer.Serialize(new { category = input.Category.ToApi() });
        return payload.Length <= 512 ? payload : null;
    }

    internal static MetaMessageResult ParseMessageResult(JsonElement payload)
    {
        var contacts = new List<MetaContact>();
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("contacts", out var contactsEl) &&
            contactsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in contactsEl.EnumerateArray())
            {
                contacts.Add(new MetaContact(
                    c.TryGetProperty("input", out var input) ? input.GetString() : null,
                    c.TryGetProperty("wa_id", out var wa) ? wa.GetString() : null));
            }
        }

        var messages = new List<MetaMessage>();
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("messages", out var messagesEl) &&
            messagesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in messagesEl.EnumerateArray())
            {
                messages.Add(new MetaMessage(
                    m.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    m.TryGetProperty("message_status", out var st) ? st.GetString() : null));
            }
        }

        var product = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("messaging_product", out var p)
            ? p.GetString()
            : null;
        return new MetaMessageResult(product, contacts, messages, payload.Clone());
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
        {
            message.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions.Default), Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var payload = Parse(raw);
        if (response.IsSuccessStatusCode) return payload;

        var err = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("error", out var errorEl)
            ? errorEl
            : payload;
        var msg = err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var m)
            ? m.GetString()
            : payload.TryGetProperty("message", out var rootMsg) ? rootMsg.GetString() : null;
        throw ErrorFromStatus.Create(
            (int)response.StatusCode,
            msg ?? $"Erro HTTP {(int)response.StatusCode} na Graph API",
            raw);
    }

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
