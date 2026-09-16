using System.Net;
using System.Text;
using System.Text.Json;
using Ordex.WhatsApp.Sdk.Entitlement;
using Ordex.WhatsApp.Sdk.Webhooks;

namespace Ordex.WhatsApp.Sdk.Tests;

public class WebhooksTests
{
    [Fact]
    public void Devolve_hub_challenge_no_get()
    {
        var challenge = MetaWebhooks.VerifyChallenge(
            new Dictionary<string, string?>
            {
                ["hub.mode"] = "subscribe",
                ["hub.verify_token"] = "segredo",
                ["hub.challenge"] = "12345"
            },
            "segredo");
        Assert.Equal("12345", challenge);
    }

    [Fact]
    public void Rejeita_verify_token_errado()
    {
        Assert.Throws<WhatsAppValidationException>(() =>
            MetaWebhooks.VerifyChallenge(
                new Dictionary<string, string?>
                {
                    ["hub.mode"] = "subscribe",
                    ["hub.verify_token"] = "x",
                    ["hub.challenge"] = "1"
                },
                "segredo"));
    }

    [Fact]
    public void Valida_x_hub_signature_256()
    {
        const string body = """{"object":"whatsapp_business_account"}""";
        var header = MetaWebhooks.SignatureHeader(body, "app_secret_exemplo");
        Assert.True(MetaWebhooks.VerifySignature(body, header, "app_secret_exemplo"));
        Assert.False(MetaWebhooks.VerifySignature(body, header, "outro"));
        Assert.Throws<WhatsAppSignatureException>(() =>
            MetaWebhooks.VerifySignatureOrThrow("{}", header, "app_secret_exemplo"));
    }

    [Fact]
    public void Parseia_mensagens_inbound()
    {
        using var doc = JsonDocument.Parse("""
            {
              "entry": [
                {
                  "changes": [
                    {
                      "value": {
                        "messages": [
                          { "from": "5511", "id": "wamid.1", "timestamp": "1", "type": "text", "text": { "body": "pix" } }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """);
        var msgs = MetaWebhooks.ParseInboundMessages(doc.RootElement);
        Assert.Single(msgs);
        Assert.Equal("pix", msgs[0].Text);
        Assert.Equal("5511", msgs[0].From);
    }

    [Fact]
    public async Task Entitlement_usa_addons_quando_existe()
    {
        var handler = new EntitlementStub((_, url) =>
        {
            Assert.Contains("/addons/whatsapp/entitlement", url);
            return Json(HttpStatusCode.OK, new { data = new { enabled = true, plan = "saas" } });
        });
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo/")
        };
        var checker = new OrdexPayEntitlementChecker("chave", http);
        var result = await checker.CheckAsync("acme");
        Assert.True(result.Enabled);
        Assert.Equal("saas", result.Plan);
    }

    [Fact]
    public async Task Entitlement_fallback_licenses_verify()
    {
        var handler = new EntitlementStub((_, url) =>
        {
            if (url.Contains("/addons/whatsapp/entitlement"))
            {
                return Json(HttpStatusCode.NotFound, new { message = "not found" });
            }

            return Json(HttpStatusCode.OK, new
            {
                success = true,
                data = new { valid = true, addons = new { whatsapp = new { enabled = true } } }
            });
        });
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo/")
        };
        var checker = new OrdexPayEntitlementChecker("chave", http);
        var result = await checker.CheckAsync("acme");
        Assert.True(result.Enabled);
    }

    [Fact]
    public async Task Static_checker_demo()
    {
        var on = await new StaticEntitlementChecker(true).CheckAsync("t");
        var off = await new StaticEntitlementChecker(false).CheckAsync("t");
        Assert.True(on.Enabled);
        Assert.False(off.Enabled);
        Assert.Equal("whatsapp", on.AddOn);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private sealed class EntitlementStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _handler;
        public EntitlementStub(Func<HttpRequestMessage, string, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request, request.RequestUri?.ToString() ?? ""));
    }
}
