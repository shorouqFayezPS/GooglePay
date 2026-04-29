namespace PaymentGateway.Domain.ValueObjects;

/// <summary>
/// Deserialized from GatewayConfigurations.CredentialsJson
/// </summary>
public sealed record CheckoutCredentials
{
    /// <summary>Used as Bearer token on every Checkout.com API call (charges, sessions).</summary>
    public string SecretKey { get; init; } = null!;

    /// <summary>
    /// Used as Bearer token specifically for the /tokens endpoint.
    /// Required for Google Pay tokenization — the mobile SDK sends the
    /// encrypted Google Pay blob to your backend, which exchanges it here.
    /// </summary>
    public string PublicKey { get; init; } = null!;

    public string WebhookSecret { get; init; } = null!;
    public string? ProcessingChannelId { get; init; }

    /// <summary>Gateway display name, e.g. "Checkout.com"</summary>
    public string GatewayName { get; init; } = null!;

    /// <summary>Merchant ID assigned by Checkout.com — sent to Google Pay SDK on the client.</summary>
    public string GatewayMerchantId { get; init; } = null!;

    /// <summary>
    /// ISO 3166-1 alpha-2 billing country code (e.g. "BH", "SA", "AE").
    /// Must be a country supported by Google Pay per Checkout.com docs.
    /// Stored per gateway config — never hardcode.
    /// </summary>
    public string BillingCountry { get; init; } = null!;
}
