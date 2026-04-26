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

public sealed record InitiatePaymentCommand : IRequest<InitiatePaymentResponse>
{
    public string OrderNumber { get; init; } = null!;
    public int CountryId { get; init; }
    public GatewayType GatewayType { get; init; } = GatewayType.Checkout;
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
    public Guid? HeaderPaymentGuid { get; init; }
}

// ─────────────────────────────────────────────
//  Handler
// ─────────────────────────────────────────────

public sealed class InitiatePaymentCommandHandler
    : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResponse>
{
    private readonly IUnitOfWork _uow;
    private readonly ICheckoutPaymentService _checkoutService;
    private readonly ILogger<InitiatePaymentCommandHandler> _logger;

    public InitiatePaymentCommandHandler(
        IUnitOfWork uow,
        ICheckoutPaymentService checkoutService,
        ILogger<InitiatePaymentCommandHandler> logger)
    {
        _uow = uow;
        _checkoutService = checkoutService;
        _logger = logger;
    }

    public async Task<InitiatePaymentResponse> Handle(
        InitiatePaymentCommand cmd, CancellationToken ct)
    {
        _logger.LogInformation(
            "Initiating payment for OrderNumber={OrderNumber}, Amount={Amount} {Currency}",
            cmd.OrderNumber, cmd.Amount, cmd.Currency);

        // 1. Load gateway configuration
        var config = await _uow.GatewayConfigurations
            .GetActiveAsync(cmd.CountryId, cmd.GatewayType, ct)
            ?? throw new GatewayConfigNotFoundException(cmd.CountryId, cmd.GatewayType);

        var credentials = JsonSerializer.Deserialize<CheckoutCredentials>(
            config.CredentialsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize gateway credentials.");

        // 2. Build transaction
        var transactionId = Guid.NewGuid();
        var initiationRequest = JsonSerializer.Serialize(cmd);

        var transaction = PaymentTransaction.Create(
            transactionId: transactionId,
            orderNumber: cmd.OrderNumber,
            gatewayType: cmd.GatewayType,
            countryId: cmd.CountryId,
            amount: cmd.Amount,
            currency: cmd.Currency,
            customerName: cmd.CustomerName,
            customerEmail: cmd.CustomerEmail,
            customerPhone: cmd.CustomerPhone,
            requestSource: cmd.RequestSource,
            successRedirectUrl: cmd.SuccessRedirectUrl,
            failureRedirectUrl: cmd.FailureRedirectUrl,
            cancelUrl: cmd.CancelUrl,
            callbackUrl: cmd.CallbackUrl,
            appCallbackUrl: cmd.AppCallbackUrl,
            appApiKey: cmd.AppApiKey,
            metadata: cmd.Metadata,
            headerPaymentGuid: cmd.HeaderPaymentGuid,
            initiationRequestPayload: initiationRequest);

        await _uow.PaymentTransactions.AddAsync(transaction, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Transaction {TransactionId} created, calling Checkout API",
            transactionId);

        // 3. Call Checkout API
        var sessionRequest = new CreateCheckoutSessionRequest
        {
            OrderNumber = cmd.OrderNumber,
            Amount = cmd.Amount,
            Currency = cmd.Currency,
            CustomerName = cmd.CustomerName,
            CustomerEmail = cmd.CustomerEmail,
            CustomerPhone = cmd.CustomerPhone,
            SuccessRedirectUrl = cmd.SuccessRedirectUrl,
            FailureRedirectUrl = cmd.FailureRedirectUrl,
            CancelUrl = cmd.CancelUrl,
            SecretKey = credentials.SecretKey,
            ProcessingChannelId = credentials.ProcessingChannelId,
            TransactionId = transactionId,
            ApiBaseUrl = config.ApiBaseUrl
        };

        var sessionResult = await _checkoutService.CreatePaymentSessionAsync(sessionRequest, ct);

        // 4. Persist gateway result
        transaction.SetGatewaySession(
            gatewaySessionId: sessionResult.SessionId,
            paymentUrl: sessionResult.PaymentUrl,
            gatewayRequestPayload: sessionResult.RawRequest,
            gatewayResponsePayload: sessionResult.RawResponse,
            initiationResponsePayload: sessionResult.RawResponse);

        if (!sessionResult.Success)
        {
            transaction.UpdateStatus(
                PaymentStatus.Failed,
                errorMessage: sessionResult.ErrorMessage,
                errorCode: sessionResult.ErrorCode);

            await _uow.PaymentStatusHistory.AddAsync(
                PaymentStatusHistory.Create(
                    transactionId,
                    PaymentStatus.Pending,
                    PaymentStatus.Failed,
                    reason: sessionResult.ErrorMessage,
                    changedBy: "System"), ct);

            await _uow.SaveChangesAsync(ct);

            _logger.LogError(
                "Checkout session creation failed for Transaction {TransactionId}: {Error}",
                transactionId, sessionResult.ErrorMessage);

            throw new PaymentSessionCreationException(sessionResult.ErrorMessage ?? "Unknown error");
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Payment session created. TransactionId={TransactionId}, SessionId={SessionId}",
            transactionId, sessionResult.SessionId);

        return new InitiatePaymentResponse
        {
            TransactionId = transactionId,
            OrderNumber = cmd.OrderNumber,
            PaymentUrl = sessionResult.PaymentUrl!,
            Status = PaymentStatus.Pending.ToString(),
            CreatedAt = transaction.CreatedAt
        };
    }
}
