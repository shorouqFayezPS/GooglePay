using MediatR;
using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Abstractions;
using PaymentGateway.Application.Common.Exceptions;
using PaymentGateway.Application.Payments.DTOs;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Interfaces;
using PaymentGateway.Domain.ValueObjects;
using System.Text.Json;

namespace PaymentGateway.Application.Payments.Commands;

// ─────────────────────────────────────────────
//  Command
// ─────────────────────────────────────────────

public sealed record GooglePayPaymentCommand : IRequest<GooglePayPaymentResponse>
{
    public string OrderNumber { get; init; } = null!;
    public int CountryId { get; init; }

    /// <summary>
    /// The raw encrypted PaymentData object returned by the Google Pay SDK on the device.
    /// Passed as-is to Checkout's /tokens endpoint — do not decrypt or modify.
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
    public Guid? HeaderPaymentGuid { get; init; }
}

// ─────────────────────────────────────────────
//  Handler
// ─────────────────────────────────────────────

public sealed class GooglePayPaymentCommandHandler
    : IRequestHandler<GooglePayPaymentCommand, GooglePayPaymentResponse>
{
    private readonly IUnitOfWork _uow;
    private readonly ICheckoutPaymentService _checkoutService;
    private readonly ILogger<GooglePayPaymentCommandHandler> _logger;

    public GooglePayPaymentCommandHandler(
        IUnitOfWork uow,
        ICheckoutPaymentService checkoutService,
        ILogger<GooglePayPaymentCommandHandler> logger)
    {
        _uow = uow;
        _checkoutService = checkoutService;
        _logger = logger;
    }

    public async Task<GooglePayPaymentResponse> Handle(
        GooglePayPaymentCommand cmd, CancellationToken ct)
    {
        _logger.LogInformation(
            "Google Pay payment request. OrderNumber={OrderNumber}, Amount={Amount} {Currency}",
            cmd.OrderNumber, cmd.Amount, cmd.Currency);

        // 1. Load gateway config — same table, same pattern as hosted payments
        var config = await _uow.GatewayConfigurations
            .GetActiveAsync(cmd.CountryId, GatewayType.Checkout, ct)
            ?? throw new GatewayConfigNotFoundException(cmd.CountryId, GatewayType.Checkout);

        var credentials = JsonSerializer.Deserialize<CheckoutCredentials>(
            config.CredentialsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize gateway credentials.");

        // 2. Create transaction record (Pending)
        var transactionId = Guid.NewGuid();

        var transaction = PaymentTransaction.Create(
            transactionId: transactionId,
            orderNumber: cmd.OrderNumber,
            gatewayType: GatewayType.Checkout,
            countryId: cmd.CountryId,
            amount: cmd.Amount,
            currency: cmd.Currency,
            customerName: cmd.CustomerName,
            customerEmail: cmd.CustomerEmail,
            customerPhone: cmd.CustomerPhone,
            requestSource: RequestSource.Mobile,
            successRedirectUrl: cmd.SuccessRedirectUrl,
            failureRedirectUrl: cmd.FailureRedirectUrl,
            cancelUrl: null,
            callbackUrl: cmd.CallbackUrl,
            appCallbackUrl: cmd.AppCallbackUrl,
            appApiKey: cmd.AppApiKey,
            metadata: cmd.Metadata,
            headerPaymentGuid: cmd.HeaderPaymentGuid,
            initiationRequestPayload: JsonSerializer.Serialize(new
            {
                cmd.OrderNumber,
                cmd.Amount,
                cmd.Currency,
                cmd.CustomerEmail,
                Source = "GooglePay"
            }));

        await _uow.PaymentTransactions.AddAsync(transaction, ct);
        await _uow.SaveChangesAsync(ct);

        // ── STEP 1: Tokenize ──────────────────────────────────────────────────
        // POST /tokens with PublicKey as Bearer
        // Exchanges the Google-encrypted blob for a Checkout tok_xxx
        // Also tells us token_format: pan_only | cryptogram_3ds

        _logger.LogInformation(
            "Tokenizing Google Pay token for TransactionId={TransactionId}", transactionId);

        var tokenizeResult = await _checkoutService.TokenizeGooglePayAsync(
            new GooglePayTokenizeRequest
            {
                TokenData = cmd.GooglePayTokenData,
                PublicKey = credentials.PublicKey,
                ApiBaseUrl = config.ApiBaseUrl
            }, ct);

        if (!tokenizeResult.Success)
        {
            transaction.UpdateStatus(
                PaymentStatus.Failed,
                errorMessage: tokenizeResult.ErrorMessage,
                errorCode: tokenizeResult.ErrorCode);

            await _uow.PaymentStatusHistory.AddAsync(
                PaymentStatusHistory.Create(
                    transactionId,
                    PaymentStatus.Pending,
                    PaymentStatus.Failed,
                    reason: $"Google Pay tokenization failed: {tokenizeResult.ErrorMessage}",
                    changedBy: "System"), ct);

            await _uow.SaveChangesAsync(ct);

            _logger.LogError(
                "Google Pay tokenization failed for TransactionId={TransactionId}: {Error}",
                transactionId, tokenizeResult.ErrorMessage);

            throw new PaymentSessionCreationException(
                tokenizeResult.ErrorMessage ?? "Google Pay tokenization failed.");
        }

        _logger.LogInformation(
            "Tokenized successfully. TransactionId={TransactionId}, TokenFormat={TokenFormat}",
            transactionId, tokenizeResult.TokenFormat);

        // ── STEP 2: Charge ────────────────────────────────────────────────────
        // POST /payments with SecretKey as Bearer
        // If token_format=pan_only → add 3ds.enabled=true
        // If token_format=cryptogram_3ds → Google already authenticated, no 3DS

        var chargeResult = await _checkoutService.ChargeGooglePayTokenAsync(
            new GooglePayChargeRequest
            {
                CheckoutToken = tokenizeResult.Token!,
                ApplyThreeDs = tokenizeResult.RequiresThreeDs,
                Amount = cmd.Amount,
                Currency = cmd.Currency,
                OrderNumber = cmd.OrderNumber,
                TransactionId = transactionId,
                ProcessingChannelId = credentials.ProcessingChannelId,
                SecretKey = credentials.SecretKey,
                ApiBaseUrl = config.ApiBaseUrl,
                SuccessRedirectUrl = cmd.SuccessRedirectUrl,
                FailureRedirectUrl = cmd.FailureRedirectUrl
            }, ct);

        // Persist the charge result payloads
        transaction.SetGatewaySession(
            gatewaySessionId: chargeResult.PaymentId,
            paymentUrl: chargeResult.RedirectUrl,
            gatewayRequestPayload: chargeResult.RawRequest,
            gatewayResponsePayload: chargeResult.RawResponse,
            initiationResponsePayload: chargeResult.RawResponse);

        if (!chargeResult.Success)
        {
            transaction.UpdateStatus(
                PaymentStatus.Failed,
                errorMessage: chargeResult.ErrorMessage,
                errorCode: chargeResult.ErrorCode);

            await _uow.PaymentStatusHistory.AddAsync(
                PaymentStatusHistory.Create(
                    transactionId,
                    PaymentStatus.Pending,
                    PaymentStatus.Failed,
                    reason: chargeResult.ErrorMessage,
                    changedBy: "System"), ct);

            await _uow.SaveChangesAsync(ct);

            _logger.LogError(
                "Google Pay charge failed for TransactionId={TransactionId}: {Error}",
                transactionId, chargeResult.ErrorMessage);

            throw new PaymentSessionCreationException(
                chargeResult.ErrorMessage ?? "Google Pay charge failed.");
        }

        // Map charge status → our PaymentStatus
        // "Pending"    = 3DS challenge pending (pan_only path)
        // "Authorized" = approved, awaiting capture
        // "Captured"   = fully paid
        var paymentStatus = chargeResult.Status?.ToLowerInvariant() switch
        {
            "pending"    => PaymentStatus.Pending,     // 3DS redirect needed
            "authorized" => PaymentStatus.Authorized,
            "captured"   => PaymentStatus.Paid,
            _            => PaymentStatus.Pending
        };

        if (paymentStatus != PaymentStatus.Pending)
        {
            transaction.UpdateStatus(paymentStatus);

            await _uow.PaymentStatusHistory.AddAsync(
                PaymentStatusHistory.Create(
                    transactionId,
                    PaymentStatus.Pending,
                    paymentStatus,
                    reason: $"Google Pay {tokenizeResult.TokenFormat}",
                    changedBy: "System"), ct);
        }

        if (chargeResult.PaymentId != null)
            transaction.SetGatewayReference(chargeResult.PaymentId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Google Pay payment complete. TransactionId={TransactionId}, Status={Status}, Requires3DS={Req3DS}",
            transactionId, chargeResult.Status, chargeResult.Requires3DsRedirect);

        return new GooglePayPaymentResponse
        {
            TransactionId = transactionId,
            OrderNumber = cmd.OrderNumber,
            Status = chargeResult.Status ?? PaymentStatus.Pending.ToString(),
            ThreeDsRedirectUrl = chargeResult.Requires3DsRedirect ? chargeResult.RedirectUrl : null,
            CreatedAt = transaction.CreatedAt
        };
    }
}
