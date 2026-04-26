using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Domain.Entities;

/// <summary>
/// Maps to Payment.PaymentTransactions table — DO NOT alter column mapping.
/// </summary>
public class PaymentTransaction
{
    public long Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public string OrderNumber { get; private set; } = null!;
    public GatewayType GatewayType { get; private set; }
    public int CountryId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public PaymentStatus Status { get; private set; }
    public string? GatewayReference { get; private set; }
    public string? GatewaySessionId { get; private set; }
    public string? PaymentUrl { get; private set; }
    public string CustomerName { get; private set; } = null!;
    public string CustomerEmail { get; private set; } = null!;
    public string? CustomerPhone { get; private set; }
    public string? AppCallbackUrl { get; private set; }
    public string? AppApiKey { get; private set; }
    public RequestSource RequestSource { get; private set; }
    public string? SuccessRedirectUrl { get; private set; }
    public string? FailureRedirectUrl { get; private set; }
    public string? CallbackUrl { get; private set; }
    public string? CancelUrl { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? Metadata { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? HeaderPaymentGuid { get; private set; }

    // Payload columns
    public string? ClientCallbackRequestPayload { get; private set; }
    public string? ClientCallbackResponsePayload { get; private set; }
    public string? GatewayCallbackPayload { get; private set; }
    public string? GatewayRequestPayload { get; private set; }
    public string? GatewayResponsePayload { get; private set; }
    public string? InitiationRequestPayload { get; private set; }
    public string? InitiationResponsePayload { get; private set; }

    // Navigation properties
    public ICollection<CallbackLog> CallbackLogs { get; private set; } = new List<CallbackLog>();
    public ICollection<PaymentStatusHistory> StatusHistory { get; private set; } = new List<PaymentStatusHistory>();

    // EF Core constructor
    private PaymentTransaction() { }

    public static PaymentTransaction Create(
        Guid transactionId,
        string orderNumber,
        GatewayType gatewayType,
        int countryId,
        decimal amount,
        string currency,
        string customerName,
        string customerEmail,
        string? customerPhone,
        RequestSource requestSource,
        string? successRedirectUrl,
        string? failureRedirectUrl,
        string? cancelUrl,
        string? callbackUrl,
        string? appCallbackUrl,
        string? appApiKey,
        string? metadata,
        Guid? headerPaymentGuid,
        string? initiationRequestPayload)
    {
        var now = DateTime.UtcNow;
        return new PaymentTransaction
        {
            TransactionId = transactionId,
            OrderNumber = orderNumber,
            GatewayType = gatewayType,
            CountryId = countryId,
            Amount = amount,
            Currency = currency,
            Status = PaymentStatus.Pending,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            RequestSource = requestSource,
            SuccessRedirectUrl = successRedirectUrl,
            FailureRedirectUrl = failureRedirectUrl,
            CancelUrl = cancelUrl,
            CallbackUrl = callbackUrl,
            AppCallbackUrl = appCallbackUrl,
            AppApiKey = appApiKey,
            Metadata = metadata,
            HeaderPaymentGuid = headerPaymentGuid,
            InitiationRequestPayload = initiationRequestPayload,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };
    }

    public void SetGatewaySession(
        string? gatewaySessionId,
        string? paymentUrl,
        string? gatewayRequestPayload,
        string? gatewayResponsePayload,
        string? initiationResponsePayload)
    {
        GatewaySessionId = gatewaySessionId;
        PaymentUrl = paymentUrl;
        GatewayRequestPayload = gatewayRequestPayload;
        GatewayResponsePayload = gatewayResponsePayload;
        InitiationResponsePayload = initiationResponsePayload;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(PaymentStatus newStatus, string? errorMessage = null, string? errorCode = null)
    {
        Status = newStatus;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        UpdatedAt = DateTime.UtcNow;

        if (newStatus == PaymentStatus.Paid || newStatus == PaymentStatus.Failed || newStatus == PaymentStatus.Cancelled)
            CompletedAt = DateTime.UtcNow;
    }

    public void SetGatewayReference(string gatewayReference)
    {
        GatewayReference = gatewayReference;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetGatewayCallbackPayload(string payload)
    {
        GatewayCallbackPayload = payload;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetClientCallbackPayloads(string? requestPayload, string? responsePayload)
    {
        ClientCallbackRequestPayload = requestPayload;
        ClientCallbackResponsePayload = responsePayload;
        UpdatedAt = DateTime.UtcNow;
    }
}
