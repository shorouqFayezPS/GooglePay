using MediatR;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Application.Payments.Commands;
using PaymentGateway.Application.Payments.DTOs;

namespace PaymentGateway.API.Controllers;

[ApiController]
[Route("api/payments/webhook")]
public sealed class WebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(IMediator mediator, ILogger<WebhookController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Receives and processes payment event webhooks from Checkout.com.
    /// Validate cko-signature header before processing.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(WebhookProcessingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckoutWebhook(CancellationToken ct)
    {
        // Read raw body — MUST be raw for signature verification
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning("Received empty webhook body from {IP}",
                HttpContext.Connection.RemoteIpAddress);
            return BadRequest("Empty payload.");
        }

        // Checkout sends the signature in the cko-signature header
        var signature = Request.Headers["cko-signature"].FirstOrDefault() ?? string.Empty;

        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Missing cko-signature header");
            return Unauthorized("Missing webhook signature.");
        }

        var command = new ProcessWebhookCommand
        {
            RawPayload = rawBody,
            Signature = signature,
            ClientIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            WebhookUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}",
            // CountryId resolved internally from transaction
            CountryId = 0
        };

        var result = await _mediator.Send(command, ct);

        // Always return 200 to prevent Checkout from retrying valid but unactionable events
        return Ok(result);
    }
}
