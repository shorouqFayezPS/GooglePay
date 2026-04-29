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
    /// Step 1 of Google Pay API-only flow.
    /// Exchanges the encrypted Google Pay PaymentData blob (from the mobile SDK)
    /// for a Checkout.com token. Uses the PUBLIC key as Bearer.
    /// Returns token + token_format (pan_only | cryptogram_3ds).
    /// </summary>
    Task<GooglePayTokenizeResult> TokenizeGooglePayAsync(
        GooglePayTokenizeRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Step 2 of Google Pay API-only flow.
    /// Charges using the Checkout token from Step 1. Uses the SECRET key as Bearer.
    /// Automatically applies 3DS when token_format was pan_only.
    /// Returns status + optional 3DS redirect URL for the mobile app.
    /// </summary>
    Task<GooglePayChargeResult> ChargeGooglePayTokenAsync(
        GooglePayChargeRequest request,
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
