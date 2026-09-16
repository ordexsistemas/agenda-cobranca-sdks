using AgendaCobranca.Sdk.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgendaCobranca.Sdk.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgendaCobranca(
        this IServiceCollection services,
        Action<AgendaCobrancaOptions> configure)
    {
        services.AddOptions<AgendaCobrancaOptions>()
            .Configure(configure)
            .PostConfigure(options => options.Validate());

        services.AddTransient<SigningDelegatingHandler>();

        services.AddHttpClient<IAgendaCobrancaClient, AgendaCobrancaClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AgendaCobrancaOptions>>().Value;
                client.BaseAddress = AgendaCobrancaClient.BaseAddress(options.BaseUrl);
                client.Timeout = options.Timeout;
            })
            .AddHttpMessageHandler<SigningDelegatingHandler>();

        return services;
    }
}
