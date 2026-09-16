using Microsoft.Extensions.Options;

namespace AgendaCobranca.Sdk.Security;

public sealed class SigningDelegatingHandler : DelegatingHandler
{
    private readonly AgendaCobrancaOptions _options;
    private readonly HmacSigner _signer;

    public SigningDelegatingHandler(IOptions<AgendaCobrancaOptions> options)
        : this(options.Value)
    {
    }

    public SigningDelegatingHandler(AgendaCobrancaOptions options, HmacSigner? signer = null)
    {
        _options = options;
        _signer = signer ?? new HmacSigner(options.ClientSecret, options.Clock, options.NonceGenerator);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = Array.Empty<byte>();
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var reset = new ByteArrayContent(body);
            foreach (var header in request.Content.Headers)
            {
                reset.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            request.Content = reset;
        }

        var path = ResolverCaminho(request);
        var method = request.Method.Method;
        var assinatura = _signer.Sign(method, path, body);

        request.Headers.Remove("X-Client-Id");
        request.Headers.Remove("X-Timestamp");
        request.Headers.Remove("X-Nonce");
        request.Headers.Remove("X-Signature");
        request.Headers.Remove("X-Api-Key");

        request.Headers.TryAddWithoutValidation("X-Client-Id", _options.ClientId);
        request.Headers.TryAddWithoutValidation("X-Timestamp", assinatura.Timestamp);
        request.Headers.TryAddWithoutValidation("X-Nonce", assinatura.Nonce);
        request.Headers.TryAddWithoutValidation("X-Signature", assinatura.Signature);
        request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal static string ResolverCaminho(HttpRequestMessage request)
    {
        var uri = request.RequestUri ?? throw new AgendaCobrancaException("RequestUri ausente");
        return uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString.Split('?')[0];
    }
}
