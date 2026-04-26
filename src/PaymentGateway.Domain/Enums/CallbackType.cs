namespace PaymentGateway.Domain.Enums;

public enum CallbackType
{
    Gateway,
    Client
}

public enum ProcessingResult
{
    Success,
    Failed,
    Duplicate,
    Error
}
