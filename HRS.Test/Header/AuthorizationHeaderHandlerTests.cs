using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HRS.API.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace HRS.Test.API.Handlers;


public class TestHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    private readonly HttpResponseMessage _response;

    public TestHttpMessageHandler(HttpResponseMessage response)
    {
        _response = response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_response);
    }
}
public class AuthorizationHeaderHandlerTests
{
    [Fact]
    public async Task SendAsync_ForwardsAuthorizationHeader_WhenHeaderExists()
    {
        // Arrange
        var authHeaderValue = "Bearer test-token";

        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = authHeaderValue;

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(context);

        var logger = Substitute.For<ILogger<AuthorizationHeaderHandler>>();

        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        var testHandler = new TestHttpMessageHandler(response);

        var handler = new AuthorizationHeaderHandler(httpContextAccessor, logger)
        {
            InnerHandler = testHandler
        };

        var client = new HttpClient(handler);

        // Act
        var result = await client.GetAsync("http://localhost/test");

        // Assert
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        testHandler.LastRequest!.Headers.Authorization.Should().NotBeNull();
        testHandler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        testHandler.LastRequest.Headers.Authorization!.Parameter.Should().Be("test-token");
    }

    [Fact]
    public async Task SendAsync_DoesNotAddAuthorizationHeader_WhenHeaderMissing()
    {
        // Arrange
        var context = new DefaultHttpContext(); // no Authorization header
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(context);

        var logger = Substitute.For<ILogger<AuthorizationHeaderHandler>>();

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var testHandler = new TestHttpMessageHandler(response);

        var handler = new AuthorizationHeaderHandler(httpContextAccessor, logger)
        {
            InnerHandler = testHandler
        };
        var client = new HttpClient(handler);

        // Act
        var result = await client.GetAsync("http://localhost/test");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        testHandler.LastRequest!.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_DoesNotAddAuthorizationHeader_WhenHttpContextIsNull()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null); // no HttpContext

        var logger = Substitute.For<ILogger<AuthorizationHeaderHandler>>();

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var testHandler = new TestHttpMessageHandler(response);

        var handler = new AuthorizationHeaderHandler(httpContextAccessor, logger)
        {
            InnerHandler = testHandler
        };
        var client = new HttpClient(handler);

        // Act
        var result = await client.GetAsync("http://localhost/test");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        testHandler.LastRequest!.Headers.Authorization.Should().BeNull();
    }


}
