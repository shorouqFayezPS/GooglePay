using MediatR;
using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Abstractions;
using PaymentGateway.Application.Common.Exceptions;
using PaymentGateway.Application.Payments.DTOs;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Interfaces;
using PaymentGateway.Domain.ValueObjects;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Application.Payments.Commands;

// ─────────────────────────────────────────────
//  Command
// ─────────────────────────────────────────────

public sealed record ProcessWebhookCommand : IRequest<WebhookProcessingResponse>
{
    public string RawPayload { get; init; } = null!;
    public string Signature { get; init; } = null!;
    public string? ClientIpAddress { get; init; }
    public string WebhookUrl { get; init; } = null!;

    /// <summary>CountryId is resolved from gateway config lookup during processing.</summary>
    public int CountryId { get; init; }
}

// ─────────────────────────────────────────────
//  Handler
// ─────────────────────────────────────────────

public sealed class ProcessWebhookCommandHandler
    : IRequestHandler<ProcessWebhookCommand, WebhookProcessingResponse>
{
    private readonly IUnitOfWork _uow;
    private readonly ICheckoutPaymentService _checkoutService;
    private readonly ILogger<ProcessWebhookCommandHandler> _logger;

    // Checkout.com webhook event types
    private const string EventPaymentCaptured = "payment_captured";
    private const string EventPaymentApproved = "payment_approved";
    private const string EventPaymentDeclined = "payment_declined";
    private const string EventPaymentExpired  = "payment_expired";
    private const string EventPaymentCancelled = "payment_cancelled";
    private const string EventPaymentRefunded = "payment_refunded";

    public ProcessWebhookCommandHandler(
        IUnitOfWork uow,
        ICheckoutPaymentService checkoutService,
        ILogger<ProcessWebhookCommandHandler> logger)
    {
        _uow = uow;
        _checkoutService = checkoutService;
        _logger = logger;
    }

    public async Task<WebhookProcessingResponse> Handle(
        ProcessWebhookCommand cmd, CancellationToken ct)
    {
        _logger.LogInformation(
            "Received Checkout webhook. IP={IP}", cmd.ClientIpAddress);

        // 1. Compute idempotency hash (SHA-256 of raw payload)
        var idempotencyHash = ComputeSha256(cmd.RawPayload);

        // 2. Duplicate detection
        var isDuplicate = await _uow.CallbackLogs
            .ExistsByIdempotencyHashAsync(idempotencyHash, ct);

        if (isDuplicate)
        {
            _logger.LogWarning(
                "Duplicate webhook detected. Hash={Hash}", idempotencyHash);

            return new WebhookProcessingResponse
            {
                Processed = false,
                Result = "Duplicate",
                Message = "Event already processed."
            };
        }

        // 3. Parse event to extract TransactionId (stored in reference/metadata)
        CheckoutWebhookEvent webhookEvent;
        try
        {
            webhookEvent = _checkoutService.ParseWebhookEvent(cmd.RawPayload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse webhook payload");
            throw new WebhookParsingException("Invalid webhook payload format.", ex);
        }

        // Checkout stores our TransactionId in metadata or reference
        var transactionIdStr = webhookEvent.Data?.Metadata?
            .GetValueOrDefault("transaction_id")
            ?? webhookEvent.Data?.Reference;

        if (!Guid.TryParse(transactionIdStr, out var transactionId))
        {
            _logger.LogError(
                "Cannot resolve TransactionId from webhook. EventId={EventId}", webhookEvent.Id);
            throw new WebhookParsingException("Cannot resolve transaction_id from webhook data.");
        }

        // 4. Load the transaction
        var transaction = await _uow.PaymentTransactions
            .GetByTransactionIdAsync(transactionId, ct)
            ?? throw new TransactionNotFoundException(transactionId);

        // 5. Validate webhook signature using gateway config credentials
        var gatewayConfig = await _uow.GatewayConfigurations
            .GetActiveAsync(transaction.CountryId, transaction.GatewayType, ct)
            ?? throw new GatewayConfigNotFoundException(transaction.CountryId, transaction.GatewayType);

        var credentials = JsonSerializer.Deserialize<CheckoutCredentials>(
            gatewayConfig.CredentialsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Cannot deserialize gateway credentials.");

        var signatureValid = _checkoutService.VerifyWebhookSignature(
            cmd.RawPayload, cmd.Signature, credentials.WebhookSecret);

        if (!signatureValid)
        {
            _logger.LogWarning(
                "Invalid webhook signature for TransactionId={TransactionId}", transactionId);
            throw new WebhookSignatureException("Webhook signature validation failed.");
        }

        // 6. Create CallbackLog record
        var callbackLog = CallbackLog.Create(
            transactionId: transactionId,
            callbackType: "Gateway",
            url: cmd.WebhookUrl,
            httpMethod: "POST",
            clientIpAddress: cmd.ClientIpAddress,
            requestPayload: cmd.RawPayload,
            idempotencyHash: idempotencyHash);

        await _uow.CallbackLogs.AddAsync(callbackLog, ct);
        await _uow.SaveChangesAsync(ct);

        // 7. Determine new payment status
        var (newStatus, reason) = MapEventToStatus(webhookEvent.Type, webhookEvent.Data);

        if (newStatus == null)
        {
            _logger.LogInformation(
                "Ignoring unhandled webhook type={Type}", webhookEvent.Type);

            callbackLog.MarkProcessed(
                responseStatusCode: 200,
                responseBody: null,
                success: true,
                processingResult: "Success",
                errorMessage: $"Event type '{webhookEvent.Type}' acknowledged but not processed.");

            await _uow.CallbackLogs.UpdateAsync(callbackLog, ct);
            await _uow.SaveChangesAsync(ct);

            return new WebhookProcessingResponse
            {
                Processed = true,
                Result = "Success",
                Message = $"Event type '{webhookEvent.Type}' acknowledged."
            };
        }

        // 8. Update transaction status (concurrency safe — EF rowversion or optimistic)
        var previousStatus = transaction.Status;
        transaction.SetGatewayCallbackPayload(cmd.RawPayload);

        if (webhookEvent.Data?.Id != null)
            transaction.SetGatewayReference(webhookEvent.Data.Id);

        transaction.UpdateStatus(
            newStatus.Value,
            errorMessage: webhookEvent.Data?.ResponseSummary,
            errorCode: webhookEvent.Data?.ResponseCode);

        await _uow.PaymentTransactions.UpdateAsync(transaction, ct);

        // 9. Insert status history
        await _uow.PaymentStatusHistory.AddAsync(
            PaymentStatusHistory.Create(
                transactionId: transactionId,
                previousStatus: previousStatus,
                newStatus: newStatus.Value,
                reason: reason,
                metadata: cmd.RawPayload.Length > 500
                    ? cmd.RawPayload[..500]
                    : cmd.RawPayload,
                changedBy: "Checkout.Webhook"), ct);

        // 10. Mark log processed
        callbackLog.MarkProcessed(
            responseStatusCode: 200,
            responseBody: null,
            success: true,
            processingResult: "Success");

        await _uow.CallbackLogs.UpdateAsync(callbackLog, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Webhook processed. TransactionId={TransactionId}, Status={Status}",
            transactionId, newStatus);

        return new WebhookProcessingResponse
        {
            Processed = true,
            Result = "Success",
            Message = $"Transaction updated to {newStatus}."
        };
    }

    // ─── Helpers ───────────────────────────────

    private static (PaymentStatus? status, string? reason) MapEventToStatus(
        string eventType, CheckoutWebhookData? data)
    {
        return eventType switch
        {
            EventPaymentCaptured  => (PaymentStatus.Paid, "Payment captured"),
            EventPaymentApproved  => (PaymentStatus.Authorized, "Payment authorised"),
            EventPaymentDeclined  => (PaymentStatus.Failed, data?.ResponseSummary ?? "Payment declined"),
            EventPaymentExpired   => (PaymentStatus.Failed, "Payment expired"),
            EventPaymentCancelled => (PaymentStatus.Cancelled, "Payment cancelled by payer"),
            EventPaymentRefunded  => (PaymentStatus.Refunded, "Payment refunded"),
            _                     => (null, null)
        };
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
