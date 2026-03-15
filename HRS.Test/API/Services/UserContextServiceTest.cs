using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using HRS.API.Services;
using HRS.Shared.Core.Dtos;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace HRS.Test.API.Services;

public class UserContextServiceTests
{
    private IHttpContextAccessor CreateHttpContextAccessor(IEnumerable<Claim>? claims = null)
    {
        var identity = new ClaimsIdentity(claims ?? new List<Claim>(), "TestAuthType");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext
        {
            User = principal
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return accessor;
    }

    [Fact]
    public void GetEmail_ShouldReturnEmailClaim_WhenExists()
    {
        // Arrange
        var accessor = CreateHttpContextAccessor(new[]
        {
            new Claim(ClaimTypes.Email, "test@example.com")
        });
        var service = new UserContextService(accessor);

        // Act
        var email = service.GetEmail();

        // Assert
        email.Should().Be("test@example.com");
    }

    [Fact]
    public void GetEmail_ShouldReturnNull_WhenEmailClaimMissing()
    {
        var accessor = CreateHttpContextAccessor(); // no claims
        var service = new UserContextService(accessor);

        var email = service.GetEmail();

        email.Should().BeNull();
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("0", 0)]
    [InlineData(null, 0)]
    public void GetUserId_ShouldParseUserIdClaims(string? claimValue, int expected)
    {
        var claims = new List<Claim>();
        if (claimValue != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, claimValue));

        var accessor = CreateHttpContextAccessor(claims);
        var service = new UserContextService(accessor);

        service.GetUserId().Should().Be(expected);
    }

    [Theory]
    [InlineData("15", 15)]
    [InlineData("0", 0)]
    [InlineData("not-a-number", 0)]
    [InlineData(null, 0)]
    public void GetStoreId_ShouldParseStoreIdClaim(string? claimValue, int expected)
    {
        var claims = new List<Claim>();
        if (claimValue != null)
            claims.Add(new Claim("storeId", claimValue));

        var accessor = CreateHttpContextAccessor(claims);
        var service = new UserContextService(accessor);

        service.GetStoreId().Should().Be(expected);
    }

    [Fact]
    public async Task GetUserAsync_ShouldReturnUserResponseDto_WithClaims()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.GivenName, "John"),
            new Claim(ClaimTypes.Surname, "Doe"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var accessor = CreateHttpContextAccessor(claims);
        var service = new UserContextService(accessor);

        // Act
        var user = await service.GetUserAsync();

        // Assert
        user.Should().BeEquivalentTo(new UserResponseDto
        {
            Id = 42,
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = "Admin"
        });
    }

    [Fact]
    public async Task GetUserAsync_ShouldReturnDefaults_WhenClaimsMissing()
    {
        var accessor = CreateHttpContextAccessor(); // no claims
        var service = new UserContextService(accessor);

        var user = await service.GetUserAsync();

        user.Should().BeEquivalentTo(new UserResponseDto
        {
            Id = 0,
            Email = "unknown@example.com",
            FirstName = "Unknown",
            LastName = "User",
            Role = "User"
        });
    }
}
