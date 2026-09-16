namespace Ordex.WhatsApp.Sdk;

public enum MessageCategory
{
    Auth,
    Utility,
    Service,
    Marketing
}

public enum QuotaMode
{
    Hard,
    Soft
}

public enum QuantityStrategy
{
    PerCategory,
    BySessions,
    ByValue
}

public static class MessageCategories
{
    public static readonly MessageCategory[] All =
    [
        MessageCategory.Auth,
        MessageCategory.Utility,
        MessageCategory.Service,
        MessageCategory.Marketing
    ];

    public static string ToApi(this MessageCategory category) => category switch
    {
        MessageCategory.Auth => "auth",
        MessageCategory.Utility => "utility",
        MessageCategory.Service => "service",
        MessageCategory.Marketing => "marketing",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static MessageCategory Parse(string value) => value.Trim().ToLowerInvariant() switch
    {
        "auth" => MessageCategory.Auth,
        "utility" => MessageCategory.Utility,
        "service" => MessageCategory.Service,
        "marketing" => MessageCategory.Marketing,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "categoria invalida")
    };

    public static string ToApi(this QuotaMode mode) => mode == QuotaMode.Soft ? "soft" : "hard";

    public static string ToApi(this QuantityStrategy strategy) => strategy switch
    {
        QuantityStrategy.BySessions => "by-sessions",
        QuantityStrategy.ByValue => "by-value",
        _ => "per-category"
    };
}

public sealed record CategoryQuota(long MonthlyQuota, decimal UnitCostUsd, long FreeAllowance = 0);

public sealed class PlanQuotas
{
    public required IReadOnlyDictionary<MessageCategory, CategoryQuota> Categories { get; init; }
    public decimal UsdToBrl { get; init; } = CenarioBase.DefaultUsdToBrl;
    public QuotaMode QuotaMode { get; init; } = QuotaMode.Hard;

    public CategoryQuota QuotaFor(MessageCategory category) => Categories[category];
}

public sealed class UsageCounts
{
    public long Auth { get; set; }
    public long Utility { get; set; }
    public long Service { get; set; }
    public long Marketing { get; set; }

    public long this[MessageCategory category]
    {
        get => category switch
        {
            MessageCategory.Auth => Auth,
            MessageCategory.Utility => Utility,
            MessageCategory.Service => Service,
            MessageCategory.Marketing => Marketing,
            _ => 0
        };
        set
        {
            switch (category)
            {
                case MessageCategory.Auth: Auth = value; break;
                case MessageCategory.Utility: Utility = value; break;
                case MessageCategory.Service: Service = value; break;
                case MessageCategory.Marketing: Marketing = value; break;
            }
        }
    }

    public UsageCounts Clone() => new()
    {
        Auth = Auth,
        Utility = Utility,
        Service = Service,
        Marketing = Marketing
    };
}

public sealed record UsageSnapshot(string TenantId, string Period, UsageCounts Counts);

public interface IUsageStore
{
    Task<UsageSnapshot> GetAsync(string tenantId, string period, CancellationToken cancellationToken = default);
    Task<UsageSnapshot> IncrementAsync(string tenantId, string period, MessageCategory category, long delta, CancellationToken cancellationToken = default);
}

public sealed record MeteringDecision(
    MessageCategory Category,
    string Period,
    long Used,
    long Quota,
    long Remaining,
    long BillableDelta,
    long FreeDelta,
    bool Overage,
    long OverageDelta,
    bool Allowed,
    QuotaMode QuotaMode);

public sealed record Pagador(string Documento, string Nome, string? Email = null);

public sealed record CobrancaRecord(
    string Id,
    long ValorCentavos,
    string Vencimento,
    string Status,
    string? ExternalReference = null,
    Pagador? Pagador = null);

public sealed record CreateCobrancaInput(
    string? ExternalReference,
    long ValorCentavos,
    string Vencimento,
    Pagador Pagador,
    string? IdempotencyKey = null);

/// <summary>
/// Superfície mínima da Agenda de Cobranças. Compatível com
/// <c>AgendaCobranca.Sdk.IAgendaCobrancaClient</c> via <see cref="Cobranca.AgendaSdkAdapter"/>.
/// </summary>
public interface IAgendaCobrancaLike
{
    Task<CobrancaRecord> CreateAsync(CreateCobrancaInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CobrancaRecord>> ListAsync(string? externalReference = null, CancellationToken cancellationToken = default);
    Task<CobrancaRecord> CancelAsync(string id, CancellationToken cancellationToken = default);
}

public sealed record TemplateComponent(
    string Type,
    string? SubType = null,
    string? Index = null,
    IReadOnlyList<IReadOnlyDictionary<string, object?>>? Parameters = null);

public sealed record TemplatePayload(
    string Name,
    string Language,
    IReadOnlyList<TemplateComponent>? Components = null);

public sealed record MediaPayload(
    string Type,
    string? Id = null,
    string? Link = null,
    string? Caption = null,
    string? Filename = null);

public sealed class SendMessageInput
{
    public required string To { get; init; }
    public required MessageCategory Category { get; init; }
    public required string Type { get; init; }
    public TemplatePayload? Template { get; init; }
    public string? TextBody { get; init; }
    public bool PreviewUrl { get; init; }
    public MediaPayload? Media { get; init; }
    public string? CallbackData { get; init; }
}

public sealed record MetaContact(string? Input, string? WaId);
public sealed record MetaMessage(string Id, string? MessageStatus);

public sealed record MetaMessageResult(
    string? MessagingProduct,
    IReadOnlyList<MetaContact>? Contacts,
    IReadOnlyList<MetaMessage>? Messages,
    object? Raw);

public sealed record SendMessageResult(
    MessageCategory Category,
    string To,
    MetaMessageResult Meta,
    MeteringDecision Metering,
    CobrancaQuantityPlan? CobrancaPlan);

public sealed record CategoryCobrancaPlan(
    MessageCategory Category,
    long Used,
    long Billable,
    long Free,
    long Quota,
    long Overage,
    decimal UnitCostUsd,
    decimal CostUsd,
    long CostBrlCentavos,
    int Quantity,
    IReadOnlyList<long> ValorCentavosEach);

public sealed record CobrancaQuantityPlan(
    string Period,
    string TenantId,
    IReadOnlyList<CategoryCobrancaPlan> Categories,
    int TotalQuantity,
    decimal TotalCostUsd,
    long TotalCostBrlCentavos,
    QuantityStrategy Strategy);

public sealed class ConversionOptions
{
    public QuantityStrategy Strategy { get; init; } = QuantityStrategy.PerCategory;
    public long SessionsPerCobranca { get; init; } = CobrancaQuantity.DefaultSessionsPerCobranca;
    public IReadOnlyDictionary<MessageCategory, long>? SessionsPerCobrancaByCategory { get; init; }
    public long ValorCentavosPorCobranca { get; init; } = CobrancaQuantity.DefaultValorCentavosPorCobranca;
    public bool IncludeZeroCategories { get; init; }
}

public sealed record CobrancaSyncItem(
    MessageCategory Category,
    int Index,
    string ExternalReference,
    string Action,
    CobrancaRecord? Cobranca);

public sealed record CobrancaSyncResult(
    string Period,
    string TenantId,
    CobrancaQuantityPlan Plan,
    IReadOnlyList<CobrancaSyncItem> Items,
    int Created,
    int Cancelled,
    int Kept);

public sealed class CategoryQuotaOverride
{
    public long? MonthlyQuota { get; init; }
    public decimal? UnitCostUsd { get; init; }
    public long? FreeAllowance { get; init; }
}

public sealed class PlanOverride
{
    public decimal? UsdToBrl { get; init; }
    public QuotaMode? QuotaMode { get; init; }
    public IReadOnlyDictionary<MessageCategory, CategoryQuotaOverride>? Categories { get; init; }
}

public sealed record EntitlementResult(
    bool Enabled,
    string TenantId,
    string AddOn = "whatsapp",
    string? Plan = null,
    string? Message = null,
    object? Raw = null);

public interface IEntitlementChecker
{
    Task<EntitlementResult> CheckAsync(string tenantId, CancellationToken cancellationToken = default);
}
