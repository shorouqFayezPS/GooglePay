using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Application.Common.Exceptions;

public sealed class GatewayConfigNotFoundException : Exception
{
    public GatewayConfigNotFoundException(int countryId, GatewayType gatewayType)
        : base($"No active gateway configuration found for CountryId={countryId}, GatewayType={gatewayType}.") { }
}

public sealed class PaymentSessionCreationException : Exception
{
    public PaymentSessionCreationException(string message)
        : base($"Payment session creation failed: {message}") { }
}

public sealed class TransactionNotFoundException : Exception
{
    public TransactionNotFoundException(Guid transactionId)
        : base($"Payment transaction with Id={transactionId} was not found.") { }
}

public sealed class WebhookSignatureException : Exception
{
    public WebhookSignatureException(string message)
        : base(message) { }
}

public sealed class WebhookParsingException : Exception
{
    public WebhookParsingException(string message, Exception? inner = null)
        : base(message, inner) { }
}

public sealed class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures have occurred.")
    {
        Errors = errors;
    }
}
