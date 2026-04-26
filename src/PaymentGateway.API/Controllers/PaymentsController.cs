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
    /// Initiates a new payment session through Checkout.com (supports Google Pay).
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
}

// ─────────────────────────────────────────────
//  Request Model
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
