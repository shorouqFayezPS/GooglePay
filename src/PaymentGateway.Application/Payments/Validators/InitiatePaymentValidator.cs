using FluentValidation;
using PaymentGateway.Application.Payments.Commands;

namespace PaymentGateway.Application.Payments.Validators;

public sealed class InitiatePaymentCommandValidator
    : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.OrderNumber)
            .NotEmpty().MaximumLength(100);

        RuleFor(x => x.CountryId)
            .GreaterThan(0);

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty().Length(3)
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO code (e.g. BHD).");

        RuleFor(x => x.CustomerName)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.CustomerEmail)
            .NotEmpty().EmailAddress().MaximumLength(200);

        RuleFor(x => x.CustomerPhone)
            .MaximumLength(50).When(x => x.CustomerPhone != null);

        RuleFor(x => x.SuccessRedirectUrl)
            .Must(BeAValidUrl!).When(x => !string.IsNullOrEmpty(x.SuccessRedirectUrl))
            .WithMessage("SuccessRedirectUrl must be a valid URL.");

        RuleFor(x => x.FailureRedirectUrl)
            .Must(BeAValidUrl!).When(x => !string.IsNullOrEmpty(x.FailureRedirectUrl))
            .WithMessage("FailureRedirectUrl must be a valid URL.");
    }

    private static bool BeAValidUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out _);
}
