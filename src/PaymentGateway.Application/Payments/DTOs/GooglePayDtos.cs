using System.Text.Json.Serialization;

namespace PaymentGateway.Application.Payments.DTOs;

// ─────────────────────────────────────────────
//  Step 1: Tokenize request  POST /tokens
//  Bearer = PublicKey
// ─────────────────────────────────────────────

public sealed record GooglePayTokenizeRequest
{
    /// <summary>The encrypted PaymentData blob from the Google Pay SDK on the device.</summary>
    public object TokenData { get; init; } = null!;
    public string PublicKey { get; init; } = null!;
    public string ApiBaseUrl { get; init; } = null!;
}

// ─────────────────────────────────────────────
//  Step 1: Tokenize response
//  token_format tells us whether 3DS is needed
// ─────────────────────────────────────────────

public sealed record GooglePayTokenizeResult
{
    public bool Success { get; init; }

    /// <summary>Checkout token e.g. tok_xxx — used in the /payments charge call.</summary>
    public string? Token { get; init; }

    /// <summary>
    /// "pan_only"       → desktop/non-Android, card stored in Google account.
    ///                    Requires 3ds.enabled=true on charge.
    /// "cryptogram_3ds" → Android device-bound. Google already authenticated.
    ///                    No additional 3DS needed.
    /// </summary>
    public string? TokenFormat { get; init; }

    public string? RawRequest { get; init; }
    public string? RawResponse { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public bool RequiresThreeDs =>
        string.Equals(TokenFormat, "pan_only", StringComparison.OrdinalIgnoreCase);
}

// ─────────────────────────────────────────────
//  Step 2: Charge request  POST /payments
//  Bearer = SecretKey
// ─────────────────────────────────────────────

public sealed record GooglePayChargeRequest
{
    public string CheckoutToken { get; init; } = null!;      // tok_xxx from tokenize step
    public bool ApplyThreeDs { get; init; }                   // true when token_format=pan_only
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string OrderNumber { get; init; } = null!;
    public Guid TransactionId { get; init; }
    public string? ProcessingChannelId { get; init; }
    public string SecretKey { get; init; } = null!;
    public string ApiBaseUrl { get; init; } = null!;
    public string? SuccessRedirectUrl { get; init; }
    public string? FailureRedirectUrl { get; init; }
}

// ─────────────────────────────────────────────
//  Step 2: Charge response
// ─────────────────────────────────────────────

public sealed record GooglePayChargeResult
{
    public bool Success { get; init; }

    /// <summary>Checkout payment ID e.g. pay_xxx</summary>
    public string? PaymentId { get; init; }

    /// <summary>
    /// "Authorized" or "Captured" → payment done, no redirect needed.
    /// "Pending"                   → 3DS redirect required, send RedirectUrl to app.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>Only populated when Status=Pending (3DS challenge URL).</summary>
    public string? RedirectUrl { get; init; }

    public string? RawRequest { get; init; }
    public string? RawResponse { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public bool Requires3DsRedirect =>
        string.Equals(Status, "Pending", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(RedirectUrl);
}

// ─────────────────────────────────────────────
//  API response back to the mobile app
// ─────────────────────────────────────────────

public sealed record GooglePayPaymentResponse
{
    public Guid TransactionId { get; init; }
    public string OrderNumber { get; init; } = null!;
    public string Status { get; init; } = null!;

    /// <summary>
    /// Null for cryptogram_3ds (payment done).
    /// Populated for pan_only when 3DS challenge is required —
    /// the app must open this URL in a WebView/browser.
    /// </summary>
    public string? ThreeDsRedirectUrl { get; init; }

    public DateTime CreatedAt { get; init; }
}
