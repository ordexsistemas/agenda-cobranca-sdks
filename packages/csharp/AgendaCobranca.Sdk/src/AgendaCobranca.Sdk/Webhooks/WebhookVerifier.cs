using AgendaCobranca.Sdk.Security;

namespace AgendaCobranca.Sdk.Webhooks;

public static class WebhookVerifier
{
    public const string DefaultPath = "/v1/webhooks";
    public const string DefaultMethod = "POST";

    public static bool Verify(
        string payload,
        IDictionary<string, string> headers,
        string clientSecret,
        string method = DefaultMethod,
        string path = DefaultPath)
    {
        if (!TryHeader(headers, "X-Timestamp", out var timestamp) ||
            !TryHeader(headers, "X-Nonce", out var nonce) ||
            !TryHeader(headers, "X-Signature", out var signature))
        {
            return false;
        }

        return Verify(payload, timestamp, nonce, signature, clientSecret, method, path);
    }

    public static bool Verify(
        string payload,
        string timestamp,
        string nonce,
        string signature,
        string clientSecret,
        string method = DefaultMethod,
        string path = DefaultPath)
    {
        var signer = new HmacSigner(clientSecret);
        return signer.ValidSignature(method, path, timestamp, nonce, signature, System.Text.Encoding.UTF8.GetBytes(payload ?? string.Empty));
    }

    public static void VerifyOrThrow(
        string payload,
        IDictionary<string, string> headers,
        string clientSecret,
        string method = DefaultMethod,
        string path = DefaultPath)
    {
        if (!Verify(payload, headers, clientSecret, method, path))
        {
            throw new AgendaCobrancaSignatureException("Assinatura de webhook invalida");
        }
    }

    private static bool TryHeader(IDictionary<string, string> headers, string name, out string value)
    {
        foreach (var (chave, valor) in headers)
        {
            if (string.Equals(chave, name, StringComparison.OrdinalIgnoreCase))
            {
                value = valor;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
