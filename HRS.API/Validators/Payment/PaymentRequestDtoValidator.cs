using FluentValidation;
using HRS.API.Contracts.DTOs.Payment;

namespace HRS.API.Validators.Payment;

public class PaymentRequestDtoValidator : AbstractValidator<PaymentRequestDto>
{
    public PaymentRequestDtoValidator()
    {
        RuleFor(x => x.OrderId)
            .NotNull().WithMessage("OrderId is required.")
            .NotEmpty().WithMessage("OrderId is required.")
            .Must(id => !string.IsNullOrWhiteSpace(id)).WithMessage("OrderId cannot be whitespace.");
    }
}

public class VerifyPaymentRequestDtoValidator : AbstractValidator<VerifyPaymentRequestDto>
{
    public VerifyPaymentRequestDtoValidator()
    {
        RuleFor(x => x.SecretKey)
            .NotNull().WithMessage("SecretKey is required.")
            .NotEmpty().WithMessage("SecretKey is required.")
            .Must(k => !string.IsNullOrWhiteSpace(k)).WithMessage("SecretKey cannot be whitespace.");
    }
}
