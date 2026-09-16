using System.Text;
using AgendaCobranca.Sdk.Security;
using AgendaCobranca.Sdk.Webhooks;

namespace AgendaCobranca.Sdk.Tests;

public class HmacSignerTests
{
    private static readonly DateTimeOffset Frozen = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
    private const string Nonce = "550e8400-e29b-41d4-a716-446655440000";
    private const string Secret = "test_client_secret";

    private static HmacSigner Signer() =>
        new(Secret, () => Frozen, () => Nonce);

    [Fact]
    public void CanonicalString_segue_vetor_compartilhado()
    {
        var body = Encoding.UTF8.GetBytes("""{"external_reference":"pedido-1","valor_centavos":15000}""");
        var canonical = HmacSigner.CanonicalString("POST", "/v1/cobrancas", "1700000000", Nonce, body);

        Assert.Equal(
            "POST\n/v1/cobrancas\n1700000000\n550e8400-e29b-41d4-a716-446655440000\nd453a65109b62169820d03d496180180c9ffd50248be209887b3786f374d0a75",
            canonical);
    }

    [Fact]
    public void Sign_produz_hmac_do_vetor_compartilhado()
    {
        var body = Encoding.UTF8.GetBytes("""{"external_reference":"pedido-1","valor_centavos":15000}""");
        var assinado = Signer().Sign("POST", "/v1/cobrancas", body);

        Assert.Equal("1700000000", assinado.Timestamp);
        Assert.Equal(Nonce, assinado.Nonce);
        Assert.Equal("f0334aadd5c24c375365a83cf301e8c39a5686f7680b0be51f84c95836cb60a3", assinado.Signature);
    }

    [Fact]
    public void Sign_get_sem_body_usa_sha256_vazio()
    {
        var assinado = Signer().Sign("GET", "/v1/cobrancas/abc", ReadOnlySpan<byte>.Empty);
        Assert.Equal("19eadbfc80ca376521472ef1297e4513e5f8ef17dedcee5e88cc34a6492349c9", assinado.Signature);
    }

    [Fact]
    public void WebhookVerifier_valida_vetor_compartilhado()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Timestamp"] = "1700000000",
            ["X-Nonce"] = Nonce,
            ["X-Signature"] = "bcf2bd3d052eaaa22c83a552dc1cdbf33e9613fa34196f6920dcfa02db0052f6"
        };

        Assert.True(WebhookVerifier.Verify("""{"id":"evt-1"}""", headers, Secret));
        Assert.False(WebhookVerifier.Verify("""{"id":"evt-2"}""", headers, Secret));
    }
}
