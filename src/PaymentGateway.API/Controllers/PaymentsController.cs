using MediatR;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Application.Payments.Commands;
using PaymentGateway.Application.Payments.DTOs;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.API.Controllers;

[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IMediator mediator, ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Initiates a new payment session through Checkout.com hosted payments page.
    /// </summary>
    /// <response code="200">Payment session created — PaymentUrl returned.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="502">Checkout.com gateway error.</response>
    [HttpPost("initiate")]
    [ProducesResponseType(typeof(InitiatePaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken ct)
    {
        var command = new InitiatePaymentCommand
        {
            OrderNumber = request.OrderNumber,
            CountryId = request.CountryId,
            GatewayType = GatewayType.Checkout,
            Amount = request.Amount,
            Currency = request.Currency,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            RequestSource = request.RequestSource,
            SuccessRedirectUrl = request.SuccessRedirectUrl,
            FailureRedirectUrl = request.FailureRedirectUrl,
            CancelUrl = request.CancelUrl,
            CallbackUrl = request.CallbackUrl,
            AppCallbackUrl = request.AppCallbackUrl,
            AppApiKey = request.AppApiKey,
            Metadata = request.Metadata,
            HeaderPaymentGuid = Request.Headers.TryGetValue("X-Payment-Guid", out var guid)
                ? Guid.TryParse(guid, out var parsedGuid) ? parsedGuid : null
                : null
        };

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Processes a Google Pay payment from a mobile app.
    /// The app collects the encrypted Google Pay token via the Google Pay SDK
    /// and sends it here. The backend tokenizes it with Checkout then charges.
    /// </summary>
    /// <remarks>
    /// Flow:
    /// 1. App calls Google Pay SDK → gets encrypted PaymentData
    /// 2. App POSTs { orderNumber, googlePayTokenData: {...} } to this endpoint
    /// 3. Backend: POST /tokens (PublicKey) → tok_xxx + token_format
    /// 4. Backend: POST /payments (SecretKey), with 3ds.enabled if token_format=pan_only
    /// 5. If ThreeDsRedirectUrl is returned → app opens it in WebView for 3DS challenge
    /// 6. Final status confirmed via webhook
    /// </remarks>
    /// <response code="200">Payment accepted. Check ThreeDsRedirectUrl — if set, open in WebView.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="502">Checkout.com gateway error.</response>
    [HttpPost("googlepay")]
    [ProducesResponseType(typeof(GooglePayPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GooglePayPayment(
        [FromBody] GooglePayPaymentRequest request,
        CancellationToken ct)
    {
        var command = new GooglePayPaymentCommand
        {
            OrderNumber = request.OrderNumber,
            CountryId = request.CountryId,
            GooglePayTokenData = request.GooglePayTokenData,
            Amount = request.Amount,
            Currency = request.Currency,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            SuccessRedirectUrl = request.SuccessRedirectUrl,
            FailureRedirectUrl = request.FailureRedirectUrl,
            CallbackUrl = request.CallbackUrl,
            AppCallbackUrl = request.AppCallbackUrl,
            AppApiKey = request.AppApiKey,
            Metadata = request.Metadata,
            HeaderPaymentGuid = Request.Headers.TryGetValue("X-Payment-Guid", out var guid)
                ? Guid.TryParse(guid, out var parsedGuid) ? parsedGuid : null
                : null
        };

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}

// ─────────────────────────────────────────────
//  Request Models
// ─────────────────────────────────────────────

public sealed record InitiatePaymentRequest
{
    public string OrderNumber { get; init; } = null!;
    public int CountryId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string CustomerName { get; init; } = null!;
    public string CustomerEmail { get; init; } = null!;
    public string? CustomerPhone { get; init; }
    public RequestSource RequestSource { get; init; } = RequestSource.Web;
    public string? SuccessRedirectUrl { get; init; }
    public string? FailureRedirectUrl { get; init; }
    public string? CancelUrl { get; init; }
    public string? CallbackUrl { get; init; }
    public string? AppCallbackUrl { get; init; }
    public string? AppApiKey { get; init; }
    public string? Metadata { get; init; }
}

public sealed record GooglePayPaymentRequest
{
    public string OrderNumber { get; init; } = null!;
    public int CountryId { get; init; }

    /// <summary>
    /// The raw encrypted PaymentData object from the Google Pay SDK.
    /// Pass exactly as received from the SDK — do not parse or modify.
    /// Shape: { "signature": "...", "intermediateSigningKey": {...}, "protocolVersion": "ECv2", "signedMessage": "..." }
    /// </summary>
    public object GooglePayTokenData { get; init; } = null!;

    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string CustomerName { get; init; } = null!;
    public string CustomerEmail { get; init; } = null!;
    public string? CustomerPhone { get; init; }
    public string? SuccessRedirectUrl { get; init; }
    public string? FailureRedirectUrl { get; init; }
    public string? CallbackUrl { get; init; }
    public string? AppCallbackUrl { get; init; }
    public string? AppApiKey { get; init; }
    public string? Metadata { get; init; }
}

