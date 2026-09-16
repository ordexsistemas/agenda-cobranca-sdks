using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ordex.WhatsApp.Sdk.Webhooks;

public sealed record InboundMessage(
    string From,
    string Id,
    string Timestamp,
    string Type,
    string? Text,
    string? CallbackData,
    JsonElement Raw);

public static class MetaWebhooks
{
    public static string VerifyChallenge(
        IReadOnlyDictionary<string, string?> query,
        string verifyToken)
    {
        if (string.IsNullOrWhiteSpace(verifyToken))
        {
            throw new WhatsAppConfigurationException("WHATSAPP_VERIFY_TOKEN e obrigatorio");
        }

        var mode = Get(query, "hub.mode") ?? Get(query, "hub_mode");
        var token = Get(query, "hub.verify_token") ?? Get(query, "hub_verify_token");
        var challenge = Get(query, "hub.challenge") ?? Get(query, "hub_challenge") ?? "";
        if (mode == "subscribe" && token == verifyToken)
        {
            return challenge;
        }

        throw new WhatsAppValidationException("Token de verificacao de webhook invalido", 403);
    }

    public static string SignatureHeader(string rawBody, string appSecret)
    {
        var hex = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();
        return $"sha256={hex}";
    }

    public static bool VerifySignature(string rawBody, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            throw new WhatsAppConfigurationException("WHATSAPP_APP_SECRET e obrigatorio");
        }

        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;
        var expected = SignatureHeader(rawBody, appSecret);
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(signatureHeader);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public static void VerifySignatureOrThrow(string rawBody, string? signatureHeader, string appSecret)
    {
        if (!VerifySignature(rawBody, signatureHeader, appSecret))
        {
            throw new WhatsAppSignatureException("Assinatura X-Hub-Signature-256 invalida");
        }
    }

    public static IReadOnlyList<InboundMessage> ParseInboundMessages(JsonElement payload)
    {
        var output = new List<InboundMessage>();
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("entry", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return output;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("changes", out var changes) ||
                changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (change.ValueKind != JsonValueKind.Object ||
                    !change.TryGetProperty("value", out var value) ||
                    !value.TryGetProperty("messages", out var messages) ||
                    messages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var msg in messages.EnumerateArray())
                {
                    if (msg.ValueKind != JsonValueKind.Object) continue;
                    string? text = null;
                    if (msg.TryGetProperty("text", out var textObj) && textObj.ValueKind == JsonValueKind.Object &&
                        textObj.TryGetProperty("body", out var body))
                    {
                        text = body.GetString();
                    }

                    output.Add(new InboundMessage(
                        msg.TryGetProperty("from", out var from) ? from.GetString() ?? "" : "",
                        msg.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        msg.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? "" : "",
                        msg.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "",
                        text,
                        msg.TryGetProperty("biz_opaque_callback_data", out var cb) ? cb.GetString() : null,
                        msg.Clone()));
                }
            }
        }

        return output;
    }

    private static string? Get(IReadOnlyDictionary<string, string?> query, string key) =>
        query.TryGetValue(key, out var value) ? value : null;
}
