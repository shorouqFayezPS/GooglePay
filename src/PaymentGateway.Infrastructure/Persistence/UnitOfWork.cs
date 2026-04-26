using PaymentGateway.Domain.Interfaces;
using PaymentGateway.Infrastructure.Persistence.Repositories;

namespace PaymentGateway.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext _ctx;

    public UnitOfWork(
        PaymentDbContext ctx,
        IPaymentTransactionRepository paymentTransactions,
        ICallbackLogRepository callbackLogs,
        IPaymentStatusHistoryRepository paymentStatusHistory,
        IGatewayConfigurationRepository gatewayConfigurations)
    {
        _ctx = ctx;
        PaymentTransactions = paymentTransactions;
        CallbackLogs = callbackLogs;
        PaymentStatusHistory = paymentStatusHistory;
        GatewayConfigurations = gatewayConfigurations;
    }

    public IPaymentTransactionRepository PaymentTransactions { get; }
    public ICallbackLogRepository CallbackLogs { get; }
    public IPaymentStatusHistoryRepository PaymentStatusHistory { get; }
    public IGatewayConfigurationRepository GatewayConfigurations { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _ctx.SaveChangesAsync(ct);
}
