using Microsoft.EntityFrameworkCore;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Interfaces;

namespace PaymentGateway.Infrastructure.Persistence.Repositories;

public sealed class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly PaymentDbContext _ctx;
    public PaymentTransactionRepository(PaymentDbContext ctx) => _ctx = ctx;

    public Task<PaymentTransaction?> GetByTransactionIdAsync(Guid id, CancellationToken ct) =>
        _ctx.PaymentTransactions
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.TransactionId == id, ct);

    public Task<PaymentTransaction?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct) =>
        _ctx.PaymentTransactions
            .FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, ct);

    public async Task AddAsync(PaymentTransaction transaction, CancellationToken ct) =>
        await _ctx.PaymentTransactions.AddAsync(transaction, ct);

    public Task UpdateAsync(PaymentTransaction transaction, CancellationToken ct)
    {
        _ctx.PaymentTransactions.Update(transaction);
        return Task.CompletedTask;
    }
}

public sealed class CallbackLogRepository : ICallbackLogRepository
{
    private readonly PaymentDbContext _ctx;
    public CallbackLogRepository(PaymentDbContext ctx) => _ctx = ctx;

    public Task<bool> ExistsByIdempotencyHashAsync(string hash, CancellationToken ct) =>
        _ctx.CallbackLogs.AnyAsync(x => x.IdempotencyHash == hash, ct);

    public async Task AddAsync(CallbackLog log, CancellationToken ct) =>
        await _ctx.CallbackLogs.AddAsync(log, ct);

    public Task UpdateAsync(CallbackLog log, CancellationToken ct)
    {
        _ctx.CallbackLogs.Update(log);
        return Task.CompletedTask;
    }
}

public sealed class PaymentStatusHistoryRepository : IPaymentStatusHistoryRepository
{
    private readonly PaymentDbContext _ctx;
    public PaymentStatusHistoryRepository(PaymentDbContext ctx) => _ctx = ctx;

    public async Task AddAsync(PaymentStatusHistory history, CancellationToken ct) =>
        await _ctx.PaymentStatusHistory.AddAsync(history, ct);
}

public sealed class GatewayConfigurationRepository : IGatewayConfigurationRepository
{
    private readonly PaymentDbContext _ctx;
    public GatewayConfigurationRepository(PaymentDbContext ctx) => _ctx = ctx;

    public Task<GatewayConfiguration?> GetActiveAsync(
        int countryId, GatewayType gatewayType, CancellationToken ct) =>
        _ctx.GatewayConfigurations
            .Where(x => x.CountryId == countryId && x.GatewayType == gatewayType)
            .FirstOrDefaultAsync(ct);   // IsDeleted filter applied by global query filter
}
