using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ordex.WhatsApp.Sdk.Entitlement;
using Ordex.WhatsApp.Sdk.Metering;

namespace Ordex.WhatsApp.Sdk.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrdexWhatsApp(
        this IServiceCollection services,
        Action<WhatsAppOptions> configure)
    {
        services.AddOptions<WhatsAppOptions>()
            .Configure(configure)
            .PostConfigure(options => options.Validate());

        services.AddSingleton<IUsageStore>(sp =>
            sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value.UsageStore ?? new InMemoryUsageStore());

        services.AddTransient<IWhatsAppClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
            if (options.UsageStore is null)
            {
                options.UsageStore = sp.GetRequiredService<IUsageStore>();
            }

            if (options.Entitlement is null && options.SkipEntitlementCheck == false &&
                string.IsNullOrWhiteSpace(options.OrdexApiKey) == false)
            {
                // OrdexPayEntitlementChecker is created inside WhatsAppClient.
            }

            if (options.AgendaSdkClient is null)
            {
                var agenda = sp.GetService<AgendaCobranca.Sdk.IAgendaCobrancaClient>();
                if (agenda is not null)
                {
                    options.AgendaSdkClient = agenda;
                }
            }

            return new WhatsAppClient(options);
        });

        return services;
    }
}
