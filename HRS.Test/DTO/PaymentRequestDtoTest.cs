using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HRS.API.Contracts.DTOs.Payment;
using HRS.Domain.Enums;
using Xunit;

namespace HRS.Tests.DTOs;

public class PaymentRequestDtoTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, results, true);
        return results;
    }

    [Fact]
    public void PaymentRequestDto_ShouldBeValid_WhenOrderIdIsSet()
    {
        var dto = new PaymentRequestDto
        {
            OrderId = "ORD123"
        };

        var results = ValidateModel(dto);

        Assert.Empty(results);
    }

    [Fact]
    public void PaymentRequestDto_ShouldBeInvalid_WhenOrderIdMissing()
    {
        var dto = new PaymentRequestDto();

        var results = ValidateModel(dto);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PaymentRequestDto.OrderId)));
    }

    [Fact]
    public void VerifyPaymentRequestDto_ShouldBeInvalid_WhenSecretKeyMissing()
    {
        var dto = new VerifyPaymentRequestDto();

        var results = ValidateModel(dto);

        Assert.Single(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(VerifyPaymentRequestDto.SecretKey)));
    }

    [Fact]
    public void CreatePaymentRequestDto_ShouldBeValid_WhenAllRequiredFieldsAreSet()
    {
        var dto = new CreatePaymentRequestDto
        {
            OrderId = "ORD999",
            Amount = 2000,
            SessionId = "sess_123",
            PaymentType = PaymentType.Cash,
            Status = PaymentStatus.Completed
        };

        var results = ValidateModel(dto);

        Assert.Empty(results);
    }

    [Fact]
    public void CreatePaymentRequestDto_ShouldBeInvalid_WhenMissingFields()
    {
        var dto = new CreatePaymentRequestDto();

        var results = ValidateModel(dto);

        Assert.True(results.Count >= 1);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreatePaymentRequestDto.OrderId)));
        // Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreatePaymentRequestDto.Amount)));
    }

    [Fact]
    public void ApprovePaymentRequestDto_ShouldBeInvalid_WhenSessionIdMissing()
    {
        var dto = new ApprovePaymentRequestDto
        {
            Amount = 1000
        };

        var results = ValidateModel(dto);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ApprovePaymentRequestDto.SessionId)));
    }

    [Fact]
    public void ApprovePaymentRequestDto_ShouldBeValid_WhenAllFieldsPresent()
    {
        var dto = new ApprovePaymentRequestDto
        {
            SessionId = "sess_456",
            Amount = 1500
        };

        var results = ValidateModel(dto);

        Assert.Empty(results);
    }
}
