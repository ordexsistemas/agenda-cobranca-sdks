namespace AgendaCobranca.Sdk;

public sealed class AgendaCobrancaOptions
{
    public const string DefaultBaseUrl = "https://api.agendacobranca.example/v1";

    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public bool VerifyLicenseOnInitialize { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public Func<DateTimeOffset>? Clock { get; set; }
    public Func<string>? NonceGenerator { get; set; }

    public void Validate()
    {
        var faltando = new List<string>();
        if (string.IsNullOrWhiteSpace(ClientId)) faltando.Add(nameof(ClientId));
        if (string.IsNullOrWhiteSpace(ApiKey)) faltando.Add(nameof(ApiKey));
        if (string.IsNullOrWhiteSpace(ClientSecret)) faltando.Add(nameof(ClientSecret));
        if (string.IsNullOrWhiteSpace(BaseUrl)) faltando.Add(nameof(BaseUrl));

        if (faltando.Count > 0)
        {
            throw new AgendaCobrancaConfigurationException(
                $"Configuracao incompleta: {string.Join(", ", faltando)}");
        }
    }
}
