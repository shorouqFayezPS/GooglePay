using FluentValidation;
using PaymentGateway.Application.Payments.Commands;

namespace PaymentGateway.Application.Payments.Validators;

public sealed class GooglePayPaymentCommandValidator
    : AbstractValidator<GooglePayPaymentCommand>
{
    public GooglePayPaymentCommandValidator()
    {
        RuleFor(x => x.OrderNumber)
            .NotEmpty().MaximumLength(100);

        RuleFor(x => x.CountryId)
            .GreaterThan(0);

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty().Length(3)
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO code.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().EmailAddress().MaximumLength(200);

        RuleFor(x => x.GooglePayTokenData)
            .NotNull().WithMessage("GooglePayTokenData is required.");
    }
}
