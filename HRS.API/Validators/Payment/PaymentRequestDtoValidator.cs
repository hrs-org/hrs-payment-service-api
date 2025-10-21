using FluentValidation;
using HRS.API.Contracts.DTOs.Payment;

namespace HRS.API.Validators.Payment;

public class PaymentRequestDtoValidator : AbstractValidator<PaymentRequestDto>
{
    public PaymentRequestDtoValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("OrderId is required.");
    }
}

public class VerifyPaymentRequestDtoValidator : AbstractValidator<VerifyPaymentRequestDto>
{
    public VerifyPaymentRequestDtoValidator()
    {
        RuleFor(x => x.SecretKey)
            .NotEmpty().WithMessage("SecretKey is required.");
    }
}
