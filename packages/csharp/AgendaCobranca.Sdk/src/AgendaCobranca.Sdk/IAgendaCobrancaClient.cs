using AgendaCobranca.Sdk.Models;

namespace AgendaCobranca.Sdk;

public interface IAgendaCobrancaClient
{
    Task<Cobranca> CreateCobrancaAsync(CreateCobrancaRequest request, CancellationToken cancellationToken = default);
    Task<Cobranca> FindCobrancaAsync(string id, CancellationToken cancellationToken = default);
    Task<ListCobrancasResponse> ListCobrancasAsync(ListCobrancasRequest? request = null, CancellationToken cancellationToken = default);
    Task<Cobranca> CancelCobrancaAsync(string id, CancellationToken cancellationToken = default);
    Task<LicenseVerifyResponse> VerifyLicenseAsync(CancellationToken cancellationToken = default);
}
