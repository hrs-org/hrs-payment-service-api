using System.Security.Claims;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using HRS.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace HRS.Test.API.Middleware;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsBadRequest_WhenValidationExceptionThrown()
    {
        // Arrange
        var logger = new TestLogger<ExceptionMiddleware>();
        RequestDelegate next = _ => throw new ValidationException(new[]
        {
            new ValidationFailure("Amount", "must be greater than 0")
        });
        var middleware = new ExceptionMiddleware(next, logger);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);
        var body = await ReadBodyAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/json");
        body.Should().Contain("Amount: must be greater than 0");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsUnauthorized_WhenUnauthorizedAccessExceptionThrown()
    {
        // Arrange
        var logger = new TestLogger<ExceptionMiddleware>();
        RequestDelegate next = _ => throw new UnauthorizedAccessException("denied");
        var middleware = new ExceptionMiddleware(next, logger);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);
        var body = await ReadBodyAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.ContentType.Should().StartWith("application/json");
        body.Should().Contain("Email or password is incorrect");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsInternalServerError_WhenUnhandledExceptionThrown()
    {
        // Arrange
        var logger = new TestLogger<ExceptionMiddleware>();
        RequestDelegate next = _ => throw new Exception("boom");
        var middleware = new ExceptionMiddleware(next, logger);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);
        var body = await ReadBodyAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().StartWith("application/json");
        body.Should().Contain("An unexpected error occurred.");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}

public class SecurityLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsCorrelationIdHeader_WhenMissing()
    {
        // Arrange
        var logger = new TestLogger<SecurityLoggingMiddleware>();
        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        var middleware = new SecurityLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.TryGetValue("X-Correlation-Id", out var correlationId).Should().BeTrue();
        correlationId.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_UsesIncomingCorrelationId_WhenProvided()
    {
        // Arrange
        var logger = new TestLogger<SecurityLoggingMiddleware>();
        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        var middleware = new SecurityLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "corr-123";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("corr-123");
    }

    [Fact]
    public async Task InvokeAsync_LogsAuthorizationDenied_WhenResponseIs401()
    {
        // Arrange
        var logger = new TestLogger<SecurityLoggingMiddleware>();
        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        var middleware = new SecurityLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "42")
        }));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains("payment.authorization_denied", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_LogsRequiredPermissionFromAuthorizePolicy()
    {
        // Arrange
        var logger = new TestLogger<SecurityLoggingMiddleware>();
        RequestDelegate next = context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        var middleware = new SecurityLoggingMiddleware(next, logger);

        var context = new DefaultHttpContext();
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute { Policy = "payments.read" }),
            "payment-endpoint");
        context.SetEndpoint(endpoint);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("payments.read", StringComparison.Ordinal));
    }
}

public sealed class TestLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry
        {
            Level = logLevel,
            Message = formatter(state, exception)
        });
    }

    public sealed class LogEntry
    {
        public LogLevel Level { get; init; }

        public string Message { get; init; } = string.Empty;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
