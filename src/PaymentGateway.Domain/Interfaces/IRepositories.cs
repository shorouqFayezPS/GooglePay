using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Domain.Interfaces;

public interface IPaymentTransactionRepository
{
    Task<PaymentTransaction?> GetByTransactionIdAsync(Guid transactionId, CancellationToken ct = default);
    Task<PaymentTransaction?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task AddAsync(PaymentTransaction transaction, CancellationToken ct = default);
    Task UpdateAsync(PaymentTransaction transaction, CancellationToken ct = default);
}

public interface ICallbackLogRepository
{
    Task<bool> ExistsByIdempotencyHashAsync(string hash, CancellationToken ct = default);
    Task AddAsync(CallbackLog log, CancellationToken ct = default);
    Task UpdateAsync(CallbackLog log, CancellationToken ct = default);
}

public interface IPaymentStatusHistoryRepository
{
    Task AddAsync(PaymentStatusHistory history, CancellationToken ct = default);
}

public interface IGatewayConfigurationRepository
{
    Task<GatewayConfiguration?> GetActiveAsync(int countryId, GatewayType gatewayType, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    IPaymentTransactionRepository PaymentTransactions { get; }
    ICallbackLogRepository CallbackLogs { get; }
    IPaymentStatusHistoryRepository PaymentStatusHistory { get; }
    IGatewayConfigurationRepository GatewayConfigurations { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
