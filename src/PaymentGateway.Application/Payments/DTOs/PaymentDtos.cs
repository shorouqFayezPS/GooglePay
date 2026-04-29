using System.Text.Json.Serialization;

namespace PaymentGateway.Application.Payments.DTOs;

// ─────────────────────────────────────────────
//  Checkout.com Session
// ─────────────────────────────────────────────

public sealed record CreateCheckoutSessionRequest
{
    public string OrderNumber { get; init; } = null!;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string CustomerName { get; init; } = null!;
    public string CustomerEmail { get; init; } = null!;
    public string? CustomerPhone { get; init; }
    public string? SuccessRedirectUrl { get; init; }
    public string? FailureRedirectUrl { get; init; }
    public string? CancelUrl { get; init; }
    public string SecretKey { get; init; } = null!;
    public string? ProcessingChannelId { get; init; }
    public string GatewayMerchantId { get; init; } = null!;
    public Guid TransactionId { get; init; }
    public string ApiBaseUrl { get; init; } = null!;
}

public sealed record CheckoutSessionResult
{
    public bool Success { get; init; }
    public string? SessionId { get; init; }
    public string? PaymentUrl { get; init; }
    public string? GatewayReference { get; init; }
    public string? RawRequest { get; init; }
    public string? RawResponse { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
}

// ─────────────────────────────────────────────
//  Checkout Webhook Event
// ─────────────────────────────────────────────

public sealed record CheckoutWebhookEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;

    [JsonPropertyName("created_on")]
    public DateTime CreatedOn { get; init; }

    [JsonPropertyName("data")]
    public CheckoutWebhookData? Data { get; init; }
}

public sealed record CheckoutWebhookData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("action_id")]
    public string? ActionId { get; init; }

    [JsonPropertyName("reference")]
    public string? Reference { get; init; }

    [JsonPropertyName("amount")]
    public long Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("response_code")]
    public string? ResponseCode { get; init; }

    [JsonPropertyName("response_summary")]
    public string? ResponseSummary { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }
}

// ─────────────────────────────────────────────
//  API Response DTOs
// ─────────────────────────────────────────────

public sealed record InitiatePaymentResponse
{
    public Guid TransactionId { get; init; }
    public string OrderNumber { get; init; } = null!;
    public string PaymentUrl { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
}

public sealed record WebhookProcessingResponse
{
    public bool Processed { get; init; }
    public string Result { get; init; } = null!;
    public string? Message { get; init; }
}
