using System.Net.Http.Headers;

namespace HRS.API.Handlers;

/// <summary>
/// Delegating handler that automatically forwards the Authorization header
/// from the current HTTP context to outgoing HTTP requests
/// </summary>
public class AuthorizationHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthorizationHeaderHandler> _logger;

    public AuthorizationHeaderHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthorizationHeaderHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get the current HTTP context
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            // Extract the Authorization header from the incoming request
            var authHeader = httpContext.Request.Headers["Authorization"].ToString();

            if (!string.IsNullOrEmpty(authHeader))
            {
                // Add the Authorization header to the outgoing request
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

                _logger.LogDebug("Authorization header forwarded to {RequestTarget}", SanitizeRequestTarget(request.RequestUri));
            }
            else
            {
                _logger.LogWarning("No Authorization header found in current request context for {RequestTarget}", SanitizeRequestTarget(request.RequestUri));
            }
        }
        else
        {
            _logger.LogWarning("No HttpContext available to extract Authorization header");
        }

        // Continue with the request
        return await base.SendAsync(request, cancellationToken);
    }

    private static string SanitizeRequestTarget(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return "unknown";
        }

        var path = requestUri.GetLeftPart(UriPartial.Path);
        return string.IsNullOrWhiteSpace(path) ? "unknown" : path;
    }
}
