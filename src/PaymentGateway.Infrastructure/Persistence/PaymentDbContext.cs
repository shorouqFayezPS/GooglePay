using Microsoft.EntityFrameworkCore;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Infrastructure.Persistence.Configurations;

namespace PaymentGateway.Infrastructure.Persistence;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options) { }

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<CallbackLog> CallbackLogs => Set<CallbackLog>();
    public DbSet<PaymentStatusHistory> PaymentStatusHistory => Set<PaymentStatusHistory>();
    public DbSet<GatewayConfiguration> GatewayConfigurations => Set<GatewayConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // All tables live in the Payment schema
        modelBuilder.HasDefaultSchema("Payment");

        modelBuilder.ApplyConfiguration(new PaymentTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new CallbackLogConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new GatewayConfigurationConfiguration());
    }
}
