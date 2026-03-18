using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace HRS.API.Middleware;

public sealed class SecurityLoggingMiddleware
{
  private const string CorrelationIdHeader = "X-Correlation-Id";

  private readonly RequestDelegate _next;
  private readonly ILogger<SecurityLoggingMiddleware> _logger;

  public SecurityLoggingMiddleware(RequestDelegate next, ILogger<SecurityLoggingMiddleware> logger)
  {
    _next = next;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    var correlationId = GetOrCreateCorrelationId(context);
    context.Response.Headers[CorrelationIdHeader] = correlationId;

    var timer = Stopwatch.StartNew();
    await _next(context);
    timer.Stop();

    var userId = HashIdentifier(context.User.FindFirst("sub")?.Value ?? "anonymous");
    var roles = context.User.FindAll("role").Select(c => c.Value).ToArray();
    var permissions = context.User.FindAll("permissions").Select(c => c.Value).ToArray();
    var requiredPermission = ResolveRequiredPermission(context);
    var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    _logger.LogInformation(
        "event_type={EventType} method={Method} path={Path} status={StatusCode} duration_ms={DurationMs} correlation_id={CorrelationId} user_id={UserId} role={Role} permissions={Permissions} required_permission={RequiredPermission} resource={Resource} source_ip={SourceIp}",
        "payment.request",
        context.Request.Method,
        context.Request.Path.Value,
        context.Response.StatusCode,
        timer.ElapsedMilliseconds,
        correlationId,
        userId,
        string.Join(",", roles),
        string.Join(",", permissions),
        requiredPermission,
        "payment",
        sourceIp);

    if (context.Response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
    {
      _logger.LogWarning(
          "event_type={EventType} status={StatusCode} method={Method} path={Path} correlation_id={CorrelationId} user_id={UserId} required_permission={RequiredPermission} source_ip={SourceIp}",
          "payment.authorization_denied",
          context.Response.StatusCode,
          context.Request.Method,
          context.Request.Path.Value,
          correlationId,
          userId,
          requiredPermission,
          sourceIp);
    }
  }

  private static string GetOrCreateCorrelationId(HttpContext context)
  {
    var incoming = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
    return string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString("N") : incoming;
  }

  private static string ResolveRequiredPermission(HttpContext context)
  {
    var policy = context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>()?.Policy;
    if (!string.IsNullOrWhiteSpace(policy))
    {
      return policy;
    }

    return context.Request.Method switch
    {
      "GET" => "read:payment",
      "POST" => "write:payment",
      "PUT" or "PATCH" => "update:payment",
      _ => "unknown"
    };
  }

  private static string HashIdentifier(string value)
  {
    var inputBytes = Encoding.UTF8.GetBytes(value);
    var hashBytes = SHA256.HashData(inputBytes);
    return Convert.ToHexString(hashBytes)[..16];
  }
}