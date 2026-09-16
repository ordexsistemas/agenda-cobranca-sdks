namespace AgendaCobranca.Sdk;

public class AgendaCobrancaException : Exception
{
    public AgendaCobrancaException(string message) : base(message) { }
    public AgendaCobrancaException(string message, Exception inner) : base(message, inner) { }
}

public sealed class AgendaCobrancaConfigurationException : AgendaCobrancaException
{
    public AgendaCobrancaConfigurationException(string message) : base(message) { }
}

public class AgendaCobrancaApiException : AgendaCobrancaException
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public AgendaCobrancaApiException(string message, int statusCode, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

public sealed class AgendaCobrancaAuthenticationException : AgendaCobrancaApiException
{
    public AgendaCobrancaAuthenticationException(string message, int statusCode, string? body = null)
        : base(message, statusCode, body) { }
}

public sealed class AgendaCobrancaNotFoundException : AgendaCobrancaApiException
{
    public AgendaCobrancaNotFoundException(string message, int statusCode, string? body = null)
        : base(message, statusCode, body) { }
}

public sealed class AgendaCobrancaValidationException : AgendaCobrancaApiException
{
    public AgendaCobrancaValidationException(string message, int statusCode, string? body = null)
        : base(message, statusCode, body) { }
}

public sealed class AgendaCobrancaSignatureException : AgendaCobrancaException
{
    public AgendaCobrancaSignatureException(string message) : base(message) { }
}
