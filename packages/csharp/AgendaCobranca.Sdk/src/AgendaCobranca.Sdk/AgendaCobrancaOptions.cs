namespace AgendaCobranca.Sdk;

public sealed class AgendaCobrancaOptions
{
    public const string DefaultBaseUrl = "https://hml-agendafinanceira.ordexpay.com.br/api/v2/externo";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>
    /// When true, requests also include HMAC headers (X-Client-Id, X-Timestamp, X-Nonce, X-Signature).
    /// Requires <see cref="ClientSecret"/>. Default is false (api_key only).
    /// </summary>
    public bool SigningEnabled { get; set; }
    public bool VerifyLicenseOnInitialize { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public Func<DateTimeOffset>? Clock { get; set; }
    public Func<string>? NonceGenerator { get; set; }

    public void Validate()
    {
        var faltando = new List<string>();
        if (string.IsNullOrWhiteSpace(ApiKey)) faltando.Add(nameof(ApiKey));
        if (string.IsNullOrWhiteSpace(BaseUrl)) faltando.Add(nameof(BaseUrl));
        if (SigningEnabled && string.IsNullOrWhiteSpace(ClientSecret))
        {
            faltando.Add(nameof(ClientSecret));
        }

        if (faltando.Count > 0)
        {
            throw new AgendaCobrancaConfigurationException(
                $"Configuracao incompleta: {string.Join(", ", faltando)}");
        }
    }
}
