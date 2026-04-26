namespace PaymentGateway.Domain.Entities;

/// <summary>
/// Maps to Payment.CallbackLogs table.
/// </summary>
public class CallbackLog
{
    public long Id { get; private set; }
    public Guid TransactionId { get; private set; }

    /// <summary>Type: Gateway or Client (V8)</summary>
    public string CallbackType { get; private set; } = null!;
    public string Url { get; private set; } = null!;
    public string? HttpMethod { get; private set; }
    public string? ClientIpAddress { get; private set; }
    public string? RequestPayload { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>SHA256 hash for duplicate detection</summary>
    public string? IdempotencyHash { get; private set; }
    public bool IsProcessed { get; private set; }

    /// <summary>Success / Failed / Duplicate / Error</summary>
    public string? ProcessingResult { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public PaymentTransaction Transaction { get; private set; } = null!;

    private CallbackLog() { }

    public static CallbackLog Create(
        Guid transactionId,
        string callbackType,
        string url,
        string? httpMethod,
        string? clientIpAddress,
        string? requestPayload,
        string? idempotencyHash)
    {
        return new CallbackLog
        {
            TransactionId = transactionId,
            CallbackType = callbackType,
            Url = url,
            HttpMethod = httpMethod,
            ClientIpAddress = clientIpAddress,
            RequestPayload = requestPayload,
            IdempotencyHash = idempotencyHash,
            Success = false,
            IsProcessed = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkProcessed(
        int? responseStatusCode,
        string? responseBody,
        bool success,
        string processingResult,
        string? errorMessage = null)
    {
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        Success = success;
        IsProcessed = true;
        ProcessingResult = processingResult;
        ErrorMessage = errorMessage;
    }
}
