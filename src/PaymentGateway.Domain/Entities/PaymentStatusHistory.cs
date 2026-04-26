using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Domain.Entities;

/// <summary>
/// Maps to Payment.PaymentStatusHistory table.
/// </summary>
public class PaymentStatusHistory
{
    public long Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public PaymentStatus PreviousStatus { get; private set; }
    public PaymentStatus NewStatus { get; private set; }
    public string? Reason { get; private set; }
    public string? Metadata { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string? ChangedBy { get; private set; }

    // Navigation
    public PaymentTransaction Transaction { get; private set; } = null!;

    private PaymentStatusHistory() { }

    public static PaymentStatusHistory Create(
        Guid transactionId,
        PaymentStatus previousStatus,
        PaymentStatus newStatus,
        string? reason = null,
        string? metadata = null,
        string? changedBy = null)
    {
        return new PaymentStatusHistory
        {
            TransactionId = transactionId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Reason = reason,
            Metadata = metadata,
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow
        };
    }
}
