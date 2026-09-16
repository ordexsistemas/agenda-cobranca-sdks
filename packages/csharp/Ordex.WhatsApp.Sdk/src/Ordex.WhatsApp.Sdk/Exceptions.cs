namespace Ordex.WhatsApp.Sdk;

public class WhatsAppSdkException : Exception
{
    public WhatsAppSdkException(string message) : base(message) { }
    public WhatsAppSdkException(string message, Exception inner) : base(message, inner) { }
}

public sealed class WhatsAppConfigurationException : WhatsAppSdkException
{
    public WhatsAppConfigurationException(string message) : base(message) { }
}

public sealed class WhatsAppSignatureException : WhatsAppSdkException
{
    public WhatsAppSignatureException(string message) : base(message) { }
}

public sealed class WhatsAppEntitlementException : WhatsAppSdkException
{
    public string TenantId { get; }

    public WhatsAppEntitlementException(string message, string tenantId) : base(message)
    {
        TenantId = tenantId;
    }
}

public sealed class WhatsAppQuotaExceededException : WhatsAppSdkException
{
    public string Category { get; }
    public long Used { get; }
    public long Quota { get; }
    public string Period { get; }

    public WhatsAppQuotaExceededException(string message, string category, long used, long quota, string period)
        : base(message)
    {
        Category = category;
        Used = used;
        Quota = quota;
        Period = period;
    }
}

public class WhatsAppApiException : WhatsAppSdkException
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }
    public object? Errors { get; }

    public WhatsAppApiException(string message, int statusCode, string? responseBody = null, object? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Errors = errors;
    }
}

public sealed class WhatsAppAuthenticationException : WhatsAppApiException
{
    public WhatsAppAuthenticationException(string message, int statusCode, string? body = null)
        : base(message, statusCode, body) { }
}

public sealed class WhatsAppNotFoundException : WhatsAppApiException
{
    public WhatsAppNotFoundException(string message, int statusCode, string? body = null)
        : base(message, statusCode, body) { }
}

public sealed class WhatsAppValidationException : WhatsAppApiException
{
    public WhatsAppValidationException(string message, int statusCode, string? body = null)
        : base(message, statusCode, body) { }
}

public sealed class WhatsAppRateLimitException : WhatsAppApiException
{
    public WhatsAppRateLimitException(string message, int statusCode, string? body = null)
        : base(message, statusCode, body) { }
}

internal static class ErrorFromStatus
{
    public static WhatsAppApiException Create(int status, string message, string? body = null, object? errors = null)
    {
        return status switch
        {
            401 or 403 => new WhatsAppAuthenticationException(message, status, body),
            404 => new WhatsAppNotFoundException(message, status, body),
            422 => new WhatsAppValidationException(message, status, body),
            429 => new WhatsAppRateLimitException(message, status, body),
            _ => new WhatsAppApiException(message, status, body, errors)
        };
    }
}
