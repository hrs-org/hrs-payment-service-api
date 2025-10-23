using System;
using FluentAssertions;
using HRS.API.Services.Helpers;
using Xunit;

namespace HRS.Test.API.Services;

public class TokenHelperTests
{
    [Fact]
    public void GenerateRefreshToken_ShouldReturnBase64UrlString_OfExpectedLength()
    {
        // Act
        var token = TokenHelper.GenerateRefreshToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
        // 32 bytes -> base64url ~ 43 chars
        token.Length.Should().BeGreaterThanOrEqualTo(43);
    }

    [Fact]
    public void HashToken_ShouldReturnDifferentHashEachTime_ForSameToken()
    {
        // Arrange
        var token = "my-secret-token";

        // Act
        var (hash1, salt1) = TokenHelper.HashToken(token);
        var (hash2, salt2) = TokenHelper.HashToken(token);

        // Assert
        hash1.Should().NotBe(hash2);
        salt1.Should().NotBe(salt2);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_ForCorrectTokenHashSalt()
    {
        // Arrange
        var token = "my-secret-token";
        var (hash, salt) = TokenHelper.HashToken(token);

        // Act
        var result = TokenHelper.Verify(token, hash, salt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForIncorrectToken()
    {
        // Arrange
        var token = "my-secret-token";
        var (hash, salt) = TokenHelper.HashToken(token);

        // Act
        var result = TokenHelper.Verify("wrong-token", hash, salt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForIncorrectHash()
    {
        // Arrange
        var token = "my-secret-token";
        var (hash, salt) = TokenHelper.HashToken(token);

        // Act
        var result = TokenHelper.Verify(token, hash + "x", salt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForIncorrectSalt()
    {
        // Arrange
        var token = "my-secret-token";
        var (hash, salt) = TokenHelper.HashToken(token);

        // Act
        var result = TokenHelper.Verify(token, hash, salt + "x");

        // Assert
        result.Should().BeFalse();
    }
}
