namespace PaymentGateway.Domain.ValueObjects;

/// <summary>
/// Deserialized from GatewayConfigurations.CredentialsJson
/// </summary>
public sealed record CheckoutCredentials
{
    public string SecretKey { get; init; } = null!;
    public string PublicKey { get; init; } = null!;
    public string WebhookSecret { get; init; } = null!;
    public string? ProcessingChannelId { get; init; }
}
