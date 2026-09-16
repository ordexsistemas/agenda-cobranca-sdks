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
    public async Task Handler_envia_api_key_sem_hmac_por_padrao()
    {
        var stub = new CapturingHandler("""
            {"data":{"id":"11111111-1111-4111-8111-111111111111","valor_centavos":15000,"vencimento":"2026-10-01","status":"pendente","pagador":{"documento":"1","nome":"A"},"external_reference":"pedido-1"}}
            """);

        var options = new AgendaCobrancaOptions
        {
            ApiKey = "api_key_exemplo",
            BaseUrl = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo"
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
        Assert.Equal("api_key_exemplo", stub.LastRequest.Headers.GetValues("chave_api").Single());
        Assert.Equal("api_key_exemplo", stub.LastRequest.Headers.GetValues("X-Api-Key").Single());
        Assert.False(stub.LastRequest.Headers.Contains("X-Client-Id"));
        Assert.False(stub.LastRequest.Headers.Contains("X-Timestamp"));
        Assert.False(stub.LastRequest.Headers.Contains("X-Nonce"));
        Assert.False(stub.LastRequest.Headers.Contains("X-Signature"));
    }

    [Fact]
    public async Task Handler_injeta_headers_hmac_quando_SigningEnabled()
    {
        var stub = new CapturingHandler("""
            {"data":{"id":"11111111-1111-4111-8111-111111111111","valor_centavos":15000,"vencimento":"2026-10-01","status":"pendente","pagador":{"documento":"1","nome":"A"},"external_reference":"pedido-1"}}
            """);

        var options = new AgendaCobrancaOptions
        {
            ClientId = "client_exemplo",
            ApiKey = "api_key_exemplo",
            ClientSecret = "test_client_secret",
            SigningEnabled = true,
            BaseUrl = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo",
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
        Assert.Equal("api_key_exemplo", stub.LastRequest!.Headers.GetValues("chave_api").Single());
        Assert.Equal("api_key_exemplo", stub.LastRequest.Headers.GetValues("X-Api-Key").Single());
        Assert.Equal("client_exemplo", stub.LastRequest.Headers.GetValues("X-Client-Id").Single());
        Assert.Equal("1700000000", stub.LastRequest.Headers.GetValues("X-Timestamp").Single());
        Assert.Equal(Nonce, stub.LastRequest.Headers.GetValues("X-Nonce").Single());

        var signer = new HmacSigner("test_client_secret");
        var esperado = signer.SignatureFor(
            "POST",
            "/api/v2/externo/cobrancas",
            "1700000000",
            Nonce,
            stub.LastBody);
        Assert.Equal(esperado, stub.LastRequest.Headers.GetValues("X-Signature").Single());
        Assert.DoesNotContain("test_client_secret", Encoding.UTF8.GetString(stub.LastBody));
    }

    [Fact]
    public void AddAgendaCobranca_registra_cliente_tipado_com_api_key()
    {
        var services = new ServiceCollection();
        services.AddAgendaCobranca(options =>
        {
            options.ApiKey = "api_key_exemplo";
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IAgendaCobrancaClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void Validate_exige_ClientSecret_quando_SigningEnabled()
    {
        var options = new AgendaCobrancaOptions
        {
            ApiKey = "api_key_exemplo",
            SigningEnabled = true
        };
        Assert.Throws<AgendaCobrancaConfigurationException>(() => options.Validate());
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
