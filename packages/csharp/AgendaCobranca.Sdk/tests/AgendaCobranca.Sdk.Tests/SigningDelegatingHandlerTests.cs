using System.Net;
using System.Text;
using AgendaCobranca.Sdk.Models;
using AgendaCobranca.Sdk.Security;
using Microsoft.Extensions.DependencyInjection;
using AgendaCobranca.Sdk.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgendaCobranca.Sdk.Tests;

public class SigningDelegatingHandlerTests
{
    private static readonly DateTimeOffset Frozen = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
    private const string Nonce = "550e8400-e29b-41d4-a716-446655440000";

    [Fact]
    public async Task Handler_injeta_headers_hmac_e_nao_vaza_secret()
    {
        var stub = new CapturingHandler("""
            {"data":{"id":"11111111-1111-4111-8111-111111111111","valor_centavos":15000,"vencimento":"2026-10-01","status":"pendente","pagador":{"documento":"1","nome":"A"},"external_reference":"pedido-1"}}
            """);

        var options = new AgendaCobrancaOptions
        {
            ClientId = "client_exemplo",
            ApiKey = "api_key_exemplo",
            ClientSecret = "test_client_secret",
            BaseUrl = "https://api.agendacobranca.example/v1",
            Clock = () => Frozen,
            NonceGenerator = () => Nonce
        };

        var handler = new SigningDelegatingHandler(options) { InnerHandler = stub };
        var http = new HttpClient(handler) { BaseAddress = AgendaCobrancaClient.BaseAddress(options.BaseUrl) };
        var client = new AgendaCobrancaClient(http, Options.Create(options));

        var cobranca = await client.CreateCobrancaAsync(new CreateCobrancaRequest(
            15000,
            "2026-10-01",
            new Pagador("12345678901", "Maria Silva", "maria@example.com"),
            "pedido-1",
            IdempotencyKey: "idem-1"));

        Assert.Equal("pendente", cobranca.Status);
        Assert.NotNull(stub.LastRequest);
        Assert.Equal("idem-1", stub.LastRequest!.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("client_exemplo", stub.LastRequest.Headers.GetValues("X-Client-Id").Single());
        Assert.Equal("api_key_exemplo", stub.LastRequest.Headers.GetValues("X-Api-Key").Single());
        Assert.Equal("1700000000", stub.LastRequest.Headers.GetValues("X-Timestamp").Single());
        Assert.Equal(Nonce, stub.LastRequest.Headers.GetValues("X-Nonce").Single());

        var signer = new HmacSigner("test_client_secret");
        var esperado = signer.SignatureFor(
            "POST",
            "/v1/cobrancas",
            "1700000000",
            Nonce,
            stub.LastBody);
        Assert.Equal(esperado, stub.LastRequest.Headers.GetValues("X-Signature").Single());
        Assert.DoesNotContain("test_client_secret", Encoding.UTF8.GetString(stub.LastBody));
    }

    [Fact]
    public void AddAgendaCobranca_registra_cliente_tipado()
    {
        var services = new ServiceCollection();
        services.AddAgendaCobranca(options =>
        {
            options.ClientId = "client_exemplo";
            options.ApiKey = "api_key_exemplo";
            options.ClientSecret = "test_client_secret";
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IAgendaCobrancaClient>();
        Assert.NotNull(client);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _response;
        public HttpRequestMessage? LastRequest { get; private set; }
        public byte[] LastBody { get; private set; } = [];

        public CapturingHandler(string response) => _response = response;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
        }
    }
}
