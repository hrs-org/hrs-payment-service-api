using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using HRS.API.Common;
using HRS.API.Contracts.DTOs;

namespace HRS.API.Middleware;

public class ExceptionMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            LogException(context, ex, "validation.failed", LogLevel.Warning);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.FailResponse(
                string.Join(", ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))
            );

            await context.Response.WriteAsJsonAsync(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogException(context, ex, "authentication.failed", LogLevel.Warning);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            var response = ApiResponse<string>.FailResponse(
                "Email or password is incorrect",
                []
            );

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonDefaults.Options));
        }
        catch (InvalidOperationException ex)
        {
            LogException(context, ex, "operation.failed", LogLevel.Warning);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            var response = ApiResponse<string>.FailResponse(
                "Request could not be processed.",
                []
            );

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonDefaults.Options));
        }
        catch (Exception ex)
        {
            LogException(context, ex, "server.error", LogLevel.Error);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = ApiResponse<string>.FailResponse(
                "An unexpected error occurred.",
                []
            );

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonDefaults.Options));
        }
    }

    private void LogException(HttpContext context, Exception ex, string eventType, LogLevel level)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault() ??
                            context.TraceIdentifier;
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userId = HashIdentifier(context.User.FindFirst("sub")?.Value ?? "anonymous");

        _logger.Log(
            level,
            ex,
            "event_type={EventType} method={Method} path={Path} correlation_id={CorrelationId} source_ip={SourceIp} user_id={UserId}",
            eventType,
            context.Request.Method,
            context.Request.Path.Value,
            correlationId,
            sourceIp,
            userId);
    }

    private static string HashIdentifier(string value)
    {
        var inputBytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes)[..16];
    }
}
