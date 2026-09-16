using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgendaCobranca.Sdk.Security;

public sealed class HmacSigner
{
    private readonly byte[] _clientSecret;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<string> _nonceGenerator;

    public HmacSigner(
        string clientSecret,
        Func<DateTimeOffset>? clock = null,
        Func<string>? nonceGenerator = null)
    {
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new AgendaCobrancaConfigurationException("client_secret e obrigatorio");
        }

        _clientSecret = Encoding.UTF8.GetBytes(clientSecret);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _nonceGenerator = nonceGenerator ?? (() => Guid.NewGuid().ToString());
    }

    public static string HashBody(ReadOnlySpan<byte> body)
    {
        var hash = SHA256.HashData(body.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string CanonicalString(string method, string path, string timestamp, string nonce, ReadOnlySpan<byte> body)
    {
        return string.Join('\n',
            method.ToUpperInvariant(),
            path,
            timestamp,
            nonce,
            HashBody(body));
    }

    public string SignatureFor(string method, string path, string timestamp, string nonce, ReadOnlySpan<byte> body)
    {
        var canonical = CanonicalString(method, path, timestamp, nonce, body);
        var mac = HMACSHA256.HashData(_clientSecret, Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    public SignedHeaders Sign(string method, string path, ReadOnlySpan<byte> body)
    {
        var timestamp = _clock().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = _nonceGenerator();
        return new SignedHeaders(
            timestamp,
            nonce,
            SignatureFor(method, path, timestamp, nonce, body),
            HashBody(body));
    }

    public bool ValidSignature(string method, string path, string timestamp, string nonce, string signature, ReadOnlySpan<byte> body)
    {
        var esperado = SignatureFor(method, path, timestamp, nonce, body);
        var left = Encoding.UTF8.GetBytes(esperado);
        var right = Encoding.UTF8.GetBytes(signature);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public readonly record struct SignedHeaders(string Timestamp, string Nonce, string Signature, string BodyHash);
