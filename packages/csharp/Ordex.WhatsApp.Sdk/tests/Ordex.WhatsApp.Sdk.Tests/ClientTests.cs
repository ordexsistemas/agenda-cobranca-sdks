using System.Net;
using System.Text;
using System.Text.Json;
using Ordex.WhatsApp.Sdk.Cobranca;
using Ordex.WhatsApp.Sdk.Entitlement;
using Ordex.WhatsApp.Sdk.Metering;

namespace Ordex.WhatsApp.Sdk.Tests;

public class ClientTests
{
    private static readonly Pagador Pagador = new("12345678901", "Empresa SaaS", "fin@example.com");

    [Fact]
    public void Rejeita_configuracao_incompleta()
    {
        var ex = Assert.Throws<WhatsAppConfigurationException>(() =>
            new WhatsAppClient(new WhatsAppOptions()));
        Assert.Contains("AccessToken", ex.Message);
    }

    [Fact]
    public async Task Bloqueia_envio_sem_addon()
    {
        var client = new WhatsAppClient(new WhatsAppOptions
        {
            AccessToken = "token",
            PhoneNumberId = "123",
            TenantId = "acme",
            Entitlement = new StaticEntitlementChecker(false, message: "addon off"),
            HttpMessageHandler = StubHandler.Json(HttpStatusCode.OK, MetaOk)
        });
        await Assert.ThrowsAsync<WhatsAppEntitlementException>(() =>
            client.SendTextAsync("5511999999999", "oi"));
    }

    [Fact]
    public async Task Envia_template_utility_marca_categoria_e_mede_uso()
    {
        var stub = StubHandler.Json(HttpStatusCode.OK, MetaOk);
        var client = new WhatsAppClient(new WhatsAppOptions
        {
            AccessToken = "token",
            PhoneNumberId = "555",
            TenantId = "acme",
            Entitlement = new StaticEntitlementChecker(true),
            Clock = () => DateTimeOffset.Parse("2026-09-16T15:00:00Z"),
            HttpMessageHandler = stub
        });

        var result = await client.SendTemplateAsync(
            "5511999999999",
            MessageCategory.Utility,
            new TemplatePayload("pix_recebido", "pt_BR"));

        Assert.Equal(MessageCategory.Utility, result.Category);
        Assert.Equal("wamid.TEST", result.Meta.Messages![0].Id);
        Assert.Equal(1, result.Metering.Used);
        Assert.Equal(1, result.Metering.BillableDelta);
        Assert.Equal(1, result.CobrancaPlan?.TotalQuantity);

        Assert.Single(stub.Calls);
        var (request, body) = stub.Calls[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v21.0/555/messages", request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer token", request.Headers.Authorization?.ToString());
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("template", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("pix_recebido", doc.RootElement.GetProperty("template").GetProperty("name").GetString());
        var callback = JsonDocument.Parse(doc.RootElement.GetProperty("biz_opaque_callback_data").GetString()!);
        Assert.Equal("utility", callback.RootElement.GetProperty("category").GetString());
        Assert.DoesNotContain("\"token\"", body);
    }

    [Fact]
    public async Task Rollback_da_cota_se_a_graph_falhar()
    {
        var store = new InMemoryUsageStore();
        var client = new WhatsAppClient(new WhatsAppOptions
        {
            AccessToken = "token",
            PhoneNumberId = "555",
            TenantId = "acme",
            Entitlement = new StaticEntitlementChecker(true),
            UsageStore = store,
            HttpMessageHandler = StubHandler.Json(HttpStatusCode.BadRequest, new
            {
                error = new { message = "invalid to", type = "OAuthException", code = 100 }
            })
        });
        var ex = await Assert.ThrowsAsync<WhatsAppApiException>(() => client.SendTextAsync("1", "oi"));
        Assert.Equal(400, ex.StatusCode);
        var snap = await store.GetAsync("acme", client.Metering.Period());
        Assert.Equal(0, snap.Counts.Service);
    }

    [Fact]
    public async Task Modo_hard_impede_post_a_meta()
    {
        var stub = StubHandler.Json(HttpStatusCode.OK, MetaOk);
        var client = new WhatsAppClient(new WhatsAppOptions
        {
            AccessToken = "token",
            PhoneNumberId = "555",
            TenantId = "acme",
            Entitlement = new StaticEntitlementChecker(true),
            QuotaMode = QuotaMode.Hard,
            Plan = new PlanOverride
            {
                Categories = new Dictionary<MessageCategory, CategoryQuotaOverride>
                {
                    [MessageCategory.Service] = new() { MonthlyQuota = 1 }
                }
            },
            HttpMessageHandler = stub
        });
        await client.SendTextAsync("5511", "1");
        await Assert.ThrowsAsync<WhatsAppQuotaExceededException>(() => client.SendTextAsync("5511", "2"));
        Assert.Single(stub.Calls);
    }

    [Fact]
    public async Task Sync_cria_cobrancas_e_cancela_extras()
    {
        var agenda = new MemoryAgenda();
        var controller = new CobrancaQuantityController(agenda, Pagador, 10);
        var full = CobrancaQuantity.Compute(
            new UsageCounts { Auth = 20_000 },
            CenarioBase.Plan,
            "2026-09",
            "acme",
            new ConversionOptions { Strategy = QuantityStrategy.BySessions, SessionsPerCobranca = 10_000 });
        Assert.Equal(2, full.TotalQuantity);

        var first = await controller.SyncAsync(full);
        Assert.Equal(2, first.Created);
        Assert.Equal(0, first.Cancelled);
        Assert.Equal(2, agenda.Items.Values.Count(c => c.Status == "pendente"));
        Assert.Equal("2026-09-10", first.Items[0].Cobranca?.Vencimento);

        var reduced = CobrancaQuantity.Compute(
            new UsageCounts { Auth = 10_000 },
            CenarioBase.Plan,
            "2026-09",
            "acme",
            new ConversionOptions { Strategy = QuantityStrategy.BySessions, SessionsPerCobranca = 10_000 });
        var second = await controller.SyncAsync(reduced);
        Assert.Equal(1, second.Kept);
        Assert.Equal(1, second.Cancelled);
        Assert.Equal(0, second.Created);
        var pending = agenda.Items.Values.Where(c => c.Status == "pendente").ToList();
        Assert.Single(pending);
        Assert.Equal("wa:acme:2026-09:auth:1", pending[0].ExternalReference);
    }

    private static readonly object MetaOk = new
    {
        messaging_product = "whatsapp",
        contacts = new[] { new { input = "5511999999999", wa_id = "5511999999999" } },
        messages = new[] { new { id = "wamid.TEST" } }
    };
}

internal sealed class MemoryAgenda : IAgendaCobrancaLike
{
    public Dictionary<string, CobrancaRecord> Items { get; } = new();

    public Task<CobrancaRecord> CreateAsync(CreateCobrancaInput input, CancellationToken cancellationToken = default)
    {
        var rec = new CobrancaRecord(
            $"cob-{input.ExternalReference}",
            input.ValorCentavos,
            input.Vencimento,
            "pendente",
            input.ExternalReference,
            input.Pagador);
        Items[rec.Id] = rec;
        return Task.FromResult(rec);
    }

    public Task<IReadOnlyList<CobrancaRecord>> ListAsync(string? externalReference = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CobrancaRecord> data = Items.Values
            .Where(c => externalReference is null || c.ExternalReference == externalReference)
            .ToArray();
        return Task.FromResult(data);
    }

    public Task<CobrancaRecord> CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        var rec = Items[id];
        var cancelled = rec with { Status = "cancelada" };
        Items[id] = cancelled;
        return Task.FromResult(cancelled);
    }
}

internal sealed class StubHandler : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _handler;

    public StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> handler) => _handler = handler;

    public static StubHandler Json(HttpStatusCode status, object body) =>
        new((_, _) =>
        {
            var json = JsonSerializer.Serialize(body);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add((request, body));
        return _handler(request, body);
    }
}
