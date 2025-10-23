using FluentAssertions;
using HRS.API.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace HRS.Test.API.Services;

public class AppConfigurationTests
{
    [Fact]
    public void ShouldLoadAllValuesFromConfiguration()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();

        configuration["FrontendUrl"].Returns("https://frontend.test");
        configuration["Payment:Stripe:ApiKey"].Returns("sk_test_123");
        configuration["Payment:ReturnPath"].Returns("/return");
        configuration["Jwt:Key"].Returns("jwt-key");
        configuration["Jwt:Issuer"].Returns("jwt-issuer");
        configuration["Jwt:Audience"].Returns("jwt-audience");

        // Act
        var appConfig = new AppConfiguration(configuration);

        // Assert
        appConfig.FrontendUrl.Should().Be("https://frontend.test");
        appConfig.StripeApiKey.Should().Be("sk_test_123");
        appConfig.PaymentReturnPath.Should().Be("https://frontend.test/return");
        appConfig.JwtKey.Should().Be("jwt-key");
        appConfig.JwtIssuer.Should().Be("jwt-issuer");
        appConfig.JwtAudience.Should().Be("jwt-audience");
    }

    [Fact]
    public void ShouldSetDefaultsWhenConfigurationMissing()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        // All returns null
        configuration["FrontendUrl"].Returns((string?)null);
        configuration["Payment:Stripe:ApiKey"].Returns((string?)null);
        configuration["Payment:ReturnPath"].Returns((string?)null);
        configuration["Jwt:Key"].Returns((string?)null);
        configuration["Jwt:Issuer"].Returns((string?)null);
        configuration["Jwt:Audience"].Returns((string?)null);

        // Act
        var appConfig = new AppConfiguration(configuration);

        // Assert
        appConfig.FrontendUrl.Should().BeEmpty();
        appConfig.StripeApiKey.Should().BeEmpty();
        appConfig.PaymentReturnPath.Should().BeEmpty();
        appConfig.JwtKey.Should().BeEmpty();
        appConfig.JwtIssuer.Should().BeEmpty();
        appConfig.JwtAudience.Should().BeEmpty();
    }
}
