using System;
using HRS.API.Controllers;
using HRS.Shared.Core.Dtos;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HRS.Tests.Controllers;

public class HealthControllerTests
{
    [Fact]
    public void GetHealth_ShouldReturnOkResponse_WithHealthCheckDto()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<HealthCheckDto>>(okResult.Value);

        Assert.Equal("Healthy", apiResponse.Data.Status);
        Assert.NotEqual(default, apiResponse.Data.Timestamp);
        Assert.False(string.IsNullOrWhiteSpace(apiResponse.Data.Environment));
    }
}
