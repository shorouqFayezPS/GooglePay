using PaymentGateway.Application.Payments.DTOs;

namespace PaymentGateway.Application.Abstractions;

public interface ICheckoutPaymentService
{
    /// <summary>
    /// Creates a hosted payment session on Checkout.com and returns the payment URL.
    /// </summary>
    Task<CheckoutSessionResult> CreatePaymentSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Validates the HMAC-SHA256 webhook signature sent by Checkout.com.
    /// </summary>
    bool VerifyWebhookSignature(string payload, string signature, string webhookSecret);

    /// <summary>
    /// Deserialises the raw webhook JSON into a structured event object.
    /// </summary>
    CheckoutWebhookEvent ParseWebhookEvent(string payload);
}
