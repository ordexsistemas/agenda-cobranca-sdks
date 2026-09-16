using Ordex.WhatsApp.Sdk.Cobranca;
using Ordex.WhatsApp.Sdk.Entitlement;
using Ordex.WhatsApp.Sdk.Graph;
using Ordex.WhatsApp.Sdk.Metering;

namespace Ordex.WhatsApp.Sdk;

public interface IWhatsAppClient
{
    MeteringService Metering { get; }
    PlanQuotas Plan { get; }
    Task<UsageSnapshot> UsageAsync(string? period = null, CancellationToken cancellationToken = default);
    CobrancaQuantityPlan CobrancaPlan(UsageCounts counts, string? period = null);
    Task<CobrancaQuantityPlan> PreviewCobrancaPlanAsync(string? period = null, CancellationToken cancellationToken = default);
    Task<CobrancaSyncResult> SyncCobrancasAsync(string? period = null, CancellationToken cancellationToken = default);
    Task<SendMessageResult> SendTemplateAsync(string to, MessageCategory category, TemplatePayload template, string? callbackData = null, CancellationToken cancellationToken = default);
    Task<SendMessageResult> SendTextAsync(string to, string body, MessageCategory? category = null, bool previewUrl = false, string? callbackData = null, CancellationToken cancellationToken = default);
    Task<SendMessageResult> SendMediaAsync(string to, MessageCategory category, MediaPayload media, string? callbackData = null, CancellationToken cancellationToken = default);
    Task<SendMessageResult> SendAsync(SendMessageInput input, CancellationToken cancellationToken = default);
}

public sealed class WhatsAppClient : IWhatsAppClient
{
    private readonly WhatsAppOptions _options;
    private readonly IEntitlementChecker? _entitlement;
    private readonly CobrancaQuantityController? _agendaController;
    private readonly ConversionOptions _conversion;
    private readonly HttpClient _graphHttp;
    private readonly HttpClient _ordexHttp;

    public WhatsAppClient(WhatsAppOptions options)
    {
        options.Validate();
        _options = options;
        Plan = CenarioBase.MergePlan(options.Plan, options.QuotaMode);
        var store = options.UsageStore ?? new InMemoryUsageStore();
        var clock = options.Clock ?? (() => DateTimeOffset.UtcNow);
        Metering = new MeteringService(Plan, store, clock, options.TimeZone);

        var handler = options.HttpMessageHandler ?? new HttpClientHandler();
        var disposeHandler = options.HttpMessageHandler is null;
        var graphRoot = (string.IsNullOrWhiteSpace(options.GraphBaseUrl) ? GraphClient.DefaultBaseUrl : options.GraphBaseUrl).TrimEnd('/');
        var version = string.IsNullOrWhiteSpace(options.GraphVersion) ? GraphClient.DefaultVersion : options.GraphVersion;
        _graphHttp = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri($"{graphRoot}/{version}/"),
            Timeout = options.Timeout
        };
        var ordexRoot = (string.IsNullOrWhiteSpace(options.OrdexBaseUrl)
            ? AgendaCobranca.Sdk.AgendaCobrancaOptions.DefaultBaseUrl
            : options.OrdexBaseUrl).TrimEnd('/') + "/";
        _ordexHttp = new HttpClient(handler, disposeHandler: disposeHandler)
        {
            BaseAddress = new Uri(ordexRoot),
            Timeout = options.Timeout
        };

        Graph = new GraphClient(_graphHttp, options.AccessToken, options.PhoneNumberId, options.WabaId);
        _entitlement = ResolveEntitlement(options, _ordexHttp);
        _conversion = new ConversionOptions
        {
            Strategy = options.QuantityStrategy,
            SessionsPerCobranca = options.SessionsPerCobranca,
            ValorCentavosPorCobranca = options.ValorCentavosPorCobranca
        };

        var agenda = ResolveAgenda(options);
        if (agenda is not null && options.AgendaPagador is not null)
        {
            _agendaController = new CobrancaQuantityController(agenda, options.AgendaPagador, options.AgendaVencimentoDay);
        }
    }

    public WhatsAppOptions Options => _options;
    public MeteringService Metering { get; }
    public GraphClient Graph { get; }
    public PlanQuotas Plan { get; }

    public Task<UsageSnapshot> UsageAsync(string? period = null, CancellationToken cancellationToken = default) =>
        Metering.SnapshotAsync(_options.TenantId, period, cancellationToken);

    public CobrancaQuantityPlan CobrancaPlan(UsageCounts counts, string? period = null) =>
        CobrancaQuantity.Compute(counts, Plan, period ?? Metering.Period(), _options.TenantId, _conversion);

    public async Task<CobrancaQuantityPlan> PreviewCobrancaPlanAsync(string? period = null, CancellationToken cancellationToken = default)
    {
        var snap = await UsageAsync(period, cancellationToken).ConfigureAwait(false);
        return CobrancaPlan(snap.Counts, snap.Period);
    }

    public async Task<CobrancaSyncResult> SyncCobrancasAsync(string? period = null, CancellationToken cancellationToken = default)
    {
        if (_agendaController is null)
        {
            throw new WhatsAppConfigurationException(
                "Agenda de Cobrancas nao configurada: informe AgendaClient (ou OrdexApiKey / AgendaSdkClient) e AgendaPagador");
        }

        var plan = await PreviewCobrancaPlanAsync(period, cancellationToken).ConfigureAwait(false);
        return await _agendaController.SyncAsync(plan, cancellationToken).ConfigureAwait(false);
    }

    public Task<SendMessageResult> SendTemplateAsync(
        string to,
        MessageCategory category,
        TemplatePayload template,
        string? callbackData = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(new SendMessageInput
        {
            To = to,
            Category = category,
            Type = "template",
            Template = template,
            CallbackData = callbackData
        }, cancellationToken);

    public Task<SendMessageResult> SendTextAsync(
        string to,
        string body,
        MessageCategory? category = null,
        bool previewUrl = false,
        string? callbackData = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(new SendMessageInput
        {
            To = to,
            Category = category ?? MessageCategory.Service,
            Type = "text",
            TextBody = body,
            PreviewUrl = previewUrl,
            CallbackData = callbackData
        }, cancellationToken);

    public Task<SendMessageResult> SendMediaAsync(
        string to,
        MessageCategory category,
        MediaPayload media,
        string? callbackData = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(new SendMessageInput
        {
            To = to,
            Category = category,
            Type = media.Type,
            Media = media,
            CallbackData = callbackData
        }, cancellationToken);

    public async Task<SendMessageResult> SendAsync(SendMessageInput input, CancellationToken cancellationToken = default)
    {
        await OrdexPayEntitlementChecker.AssertEnabledAsync(_entitlement, _options.TenantId, _options.SkipEntitlementCheck, cancellationToken)
            .ConfigureAwait(false);

        var metering = await Metering.ConsumeAsync(_options.TenantId, input.Category, 1, cancellationToken)
            .ConfigureAwait(false);

        MetaMessageResult meta;
        try
        {
            meta = await Graph.SendMessageAsync(input, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await Metering.RollbackAsync(_options.TenantId, input.Category, 1, cancellationToken).ConfigureAwait(false);
            throw;
        }

        CobrancaQuantityPlan cobrancaPlan;
        if (_options.SyncCobrancasOnSend && _agendaController is not null)
        {
            var sync = await SyncCobrancasAsync(metering.Period, cancellationToken).ConfigureAwait(false);
            cobrancaPlan = sync.Plan;
        }
        else
        {
            var snap = await UsageAsync(metering.Period, cancellationToken).ConfigureAwait(false);
            cobrancaPlan = CobrancaPlan(snap.Counts, snap.Period);
        }

        return new SendMessageResult(input.Category, input.To, meta, metering, cobrancaPlan);
    }

    private static IEntitlementChecker? ResolveEntitlement(WhatsAppOptions options, HttpClient ordexHttp)
    {
        if (options.Entitlement is not null) return options.Entitlement;
        if (options.SkipEntitlementCheck) return new StaticEntitlementChecker(true, "demo");
        if (!string.IsNullOrWhiteSpace(options.OrdexApiKey))
        {
            return new OrdexPayEntitlementChecker(options.OrdexApiKey, ordexHttp);
        }

        return null;
    }

    private static IAgendaCobrancaLike? ResolveAgenda(WhatsAppOptions options)
    {
        if (options.AgendaClient is not null) return options.AgendaClient;
        if (options.AgendaSdkClient is not null) return new AgendaSdkAdapter(options.AgendaSdkClient);
        if (!string.IsNullOrWhiteSpace(options.OrdexApiKey))
        {
            return AgendaSdkAdapter.FromApiKey(options.OrdexApiKey, options.OrdexBaseUrl);
        }

        return null;
    }
}
