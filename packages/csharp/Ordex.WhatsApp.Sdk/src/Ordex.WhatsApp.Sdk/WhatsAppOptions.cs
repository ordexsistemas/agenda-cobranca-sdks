namespace Ordex.WhatsApp.Sdk;

public sealed class WhatsAppOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string? WabaId { get; set; }
    public string GraphVersion { get; set; } = Graph.GraphClient.DefaultVersion;
    public string GraphBaseUrl { get; set; } = Graph.GraphClient.DefaultBaseUrl;
    public string? AppSecret { get; set; }

    public string TenantId { get; set; } = string.Empty;
    public string? OrdexApiKey { get; set; }
    public string OrdexBaseUrl { get; set; } = AgendaCobranca.Sdk.AgendaCobrancaOptions.DefaultBaseUrl;

    public PlanOverride? Plan { get; set; }
    public QuotaMode QuotaMode { get; set; } = QuotaMode.Hard;
    public IUsageStore? UsageStore { get; set; }
    public Func<DateTimeOffset>? Clock { get; set; }
    public string TimeZone { get; set; } = "America/Sao_Paulo";

    public IEntitlementChecker? Entitlement { get; set; }
    public bool SkipEntitlementCheck { get; set; }

    public IAgendaCobrancaLike? AgendaClient { get; set; }
    public AgendaCobranca.Sdk.IAgendaCobrancaClient? AgendaSdkClient { get; set; }
    public Pagador? AgendaPagador { get; set; }
    public int? AgendaVencimentoDay { get; set; }
    public bool SyncCobrancasOnSend { get; set; }
    public QuantityStrategy QuantityStrategy { get; set; } = QuantityStrategy.PerCategory;
    public long SessionsPerCobranca { get; set; } = CobrancaQuantity.DefaultSessionsPerCobranca;
    public long ValorCentavosPorCobranca { get; set; } = CobrancaQuantity.DefaultValorCentavosPorCobranca;

    /// <summary>Handler compartilhado (Graph + entitlement). Testes injetam um stub.</summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(AccessToken)) missing.Add(nameof(AccessToken));
        if (string.IsNullOrWhiteSpace(PhoneNumberId)) missing.Add(nameof(PhoneNumberId));
        if (string.IsNullOrWhiteSpace(TenantId)) missing.Add(nameof(TenantId));
        if (missing.Count > 0)
        {
            throw new WhatsAppConfigurationException($"Configuracao incompleta: {string.Join(", ", missing)}");
        }
    }

    public static WhatsAppOptions FromEnvironment(IDictionary<string, string?>? env = null)
    {
        string? Get(string key)
        {
            if (env is not null && env.TryGetValue(key, out var value)) return value;
            return Environment.GetEnvironmentVariable(key);
        }

        return new WhatsAppOptions
        {
            AccessToken = Get("WHATSAPP_ACCESS_TOKEN") ?? Get("META_ACCESS_TOKEN") ?? "",
            PhoneNumberId = Get("WHATSAPP_PHONE_NUMBER_ID") ?? "",
            WabaId = Get("WHATSAPP_WABA_ID"),
            GraphVersion = Get("WHATSAPP_GRAPH_VERSION") ?? Graph.GraphClient.DefaultVersion,
            AppSecret = Get("WHATSAPP_APP_SECRET"),
            TenantId = Get("ORDEX_PAY_TENANT_ID") ?? Get("ORDEX_TENANT_ID") ?? "",
            OrdexApiKey = Get("ORDEX_PAY_API_KEY"),
            OrdexBaseUrl = Get("ORDEX_PAY_BASE_URL") ?? AgendaCobranca.Sdk.AgendaCobrancaOptions.DefaultBaseUrl,
            QuotaMode = Get("ORDEX_WHATSAPP_QUOTA_MODE") == "soft" ? QuotaMode.Soft : QuotaMode.Hard
        };
    }
}
