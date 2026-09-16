using AgendaCobranca.Sdk;
using AgendaCobranca.Sdk.Models;
using AgendaCobranca.Sdk.Security;
using Microsoft.Extensions.Options;
using AgendaPagador = AgendaCobranca.Sdk.Models.Pagador;

namespace Ordex.WhatsApp.Sdk.Cobranca;

/// <summary>
/// Adapta <see cref="IAgendaCobrancaClient"/> (HMAC incluso no Agenda SDK) à superfície
/// mínima usada pelo add-on WhatsApp. Não reimplementa assinatura.
/// </summary>
public sealed class AgendaSdkAdapter : IAgendaCobrancaLike
{
    private readonly IAgendaCobrancaClient _client;

    public AgendaSdkAdapter(IAgendaCobrancaClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<CobrancaRecord> CreateAsync(CreateCobrancaInput input, CancellationToken cancellationToken = default)
    {
        var created = await _client.CreateCobrancaAsync(
            new CreateCobrancaRequest(
                input.ValorCentavos,
                input.Vencimento,
                new AgendaPagador(input.Pagador.Documento, input.Pagador.Nome, input.Pagador.Email),
                input.ExternalReference,
                IdempotencyKey: input.IdempotencyKey),
            cancellationToken).ConfigureAwait(false);
        return Map(created);
    }

    public async Task<IReadOnlyList<CobrancaRecord>> ListAsync(string? externalReference = null, CancellationToken cancellationToken = default)
    {
        var listed = await _client.ListCobrancasAsync(
            new ListCobrancasRequest(ExternalReference: externalReference, PerPage: 20),
            cancellationToken).ConfigureAwait(false);
        return listed.Data.Select(Map).ToArray();
    }

    public async Task<CobrancaRecord> CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        var cancelled = await _client.CancelCobrancaAsync(id, cancellationToken).ConfigureAwait(false);
        return Map(cancelled);
    }

    public static IAgendaCobrancaLike FromApiKey(string apiKey, string? baseUrl = null, HttpMessageHandler? handler = null)
    {
        var options = new AgendaCobrancaOptions
        {
            ApiKey = apiKey,
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? AgendaCobrancaOptions.DefaultBaseUrl : baseUrl
        };
        options.Validate();
        var inner = handler ?? new HttpClientHandler();
        var http = new HttpClient(new SigningDelegatingHandler(options) { InnerHandler = inner }, disposeHandler: handler is null)
        {
            BaseAddress = AgendaCobrancaClient.BaseAddress(options.BaseUrl)
        };
        var client = new AgendaCobrancaClient(http, Options.Create(options));
        return new AgendaSdkAdapter(client);
    }

    private static CobrancaRecord Map(AgendaCobranca.Sdk.Models.Cobranca c) =>
        new(
            c.Id,
            c.ValorCentavos,
            c.Vencimento,
            c.Status,
            c.ExternalReference,
            c.Pagador is null ? null : new Pagador(c.Pagador.Documento, c.Pagador.Nome, c.Pagador.Email));
}

/// <summary>
/// Cria, mantém ou cancela cobranças do período para coincidir com a quantidade
/// calculada a partir do uso WhatsApp. Usa <c>wa:{tenant}:{period}:{category}:{index}</c>.
/// </summary>
public sealed class CobrancaQuantityController
{
    private readonly IAgendaCobrancaLike _agenda;
    private readonly Pagador _pagador;
    private readonly int? _vencimentoDay;

    public CobrancaQuantityController(IAgendaCobrancaLike agenda, Pagador pagador, int? vencimentoDay = null)
    {
        _agenda = agenda;
        _pagador = pagador;
        _vencimentoDay = vencimentoDay;
    }

    public async Task<CobrancaSyncResult> SyncAsync(CobrancaQuantityPlan plan, CancellationToken cancellationToken = default)
    {
        var items = new List<CobrancaSyncItem>();
        foreach (var categoryPlan in plan.Categories)
        {
            var vencimento = CenarioBase.VencimentoForPeriod(plan.Period, _vencimentoDay);
            for (var index = 1; index <= categoryPlan.Quantity; index += 1)
            {
                var reference = CobrancaQuantity.ExternalReference(plan.TenantId, plan.Period, categoryPlan.Category, index);
                var existing = await FindByRefAsync(reference, cancellationToken).ConfigureAwait(false);
                if (existing is not null && !IsCancelled(existing))
                {
                    items.Add(new CobrancaSyncItem(categoryPlan.Category, index, reference, "kept", existing));
                    continue;
                }

                var valor = index - 1 < categoryPlan.ValorCentavosEach.Count
                    ? categoryPlan.ValorCentavosEach[index - 1]
                    : 0;
                var created = await _agenda.CreateAsync(
                    new CreateCobrancaInput(reference, valor, vencimento, _pagador, reference),
                    cancellationToken).ConfigureAwait(false);
                items.Add(new CobrancaSyncItem(categoryPlan.Category, index, reference, "created", created));
            }

            await CancelExtrasAsync(plan, categoryPlan.Category, categoryPlan.Quantity, items, cancellationToken)
                .ConfigureAwait(false);
        }

        return new CobrancaSyncResult(
            plan.Period,
            plan.TenantId,
            plan,
            items,
            items.Count(i => i.Action == "created"),
            items.Count(i => i.Action == "cancelled"),
            items.Count(i => i.Action == "kept"));
    }

    private async Task<CobrancaRecord?> FindByRefAsync(string reference, CancellationToken cancellationToken)
    {
        var listed = await _agenda.ListAsync(reference, cancellationToken).ConfigureAwait(false);
        return listed.FirstOrDefault(c => c.ExternalReference == reference && !IsCancelled(c));
    }

    private async Task CancelExtrasAsync(
        CobrancaQuantityPlan plan,
        MessageCategory category,
        int keep,
        List<CobrancaSyncItem> items,
        CancellationToken cancellationToken)
    {
        var scanUntil = keep + 20;
        for (var index = keep + 1; index <= scanUntil; index += 1)
        {
            var reference = CobrancaQuantity.ExternalReference(plan.TenantId, plan.Period, category, index);
            var existing = await FindByRefAsync(reference, cancellationToken).ConfigureAwait(false);
            if (existing is null) break;
            await _agenda.CancelAsync(existing.Id, cancellationToken).ConfigureAwait(false);
            items.Add(new CobrancaSyncItem(category, index, reference, "cancelled", existing));
        }
    }

    private static bool IsCancelled(CobrancaRecord cobranca)
    {
        var status = (cobranca.Status ?? "").ToLowerInvariant();
        return status is "cancelada" or "cancelled" or "canceled";
    }
}
