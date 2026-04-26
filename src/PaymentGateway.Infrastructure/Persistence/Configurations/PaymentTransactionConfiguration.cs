using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Configurations;

public sealed class PaymentTransactionConfiguration
    : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions", "Payment");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasColumnName("Id")
            .UseIdentityColumn();

        b.Property(x => x.TransactionId)
            .HasColumnName("TransactionId")
            .IsRequired();

        b.HasIndex(x => x.TransactionId)
            .IsUnique()
            .HasDatabaseName("AK_PaymentTransactions_TransactionId");

        b.Property(x => x.OrderNumber)
            .HasColumnName("OrderNumber")
            .HasMaxLength(100)
            .IsRequired();

        b.Property(x => x.GatewayType)
            .HasColumnName("GatewayType")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.CountryId)
            .HasColumnName("CountryId")
            .IsRequired();

        b.Property(x => x.Amount)
            .HasColumnName("Amount")
            .HasColumnType("decimal(18,3)")
            .IsRequired();

        b.Property(x => x.Currency)
            .HasColumnName("Currency")
            .HasMaxLength(3)
            .IsRequired();

        b.Property(x => x.Status)
            .HasColumnName("Status")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.GatewayReference)
            .HasColumnName("GatewayReference")
            .HasMaxLength(200);

        b.Property(x => x.GatewaySessionId)
            .HasColumnName("GatewaySessionId")
            .HasMaxLength(200);

        b.Property(x => x.PaymentUrl)
            .HasColumnName("PaymentUrl")
            .HasMaxLength(2000);

        b.Property(x => x.CustomerName)
            .HasColumnName("CustomerName")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.CustomerEmail)
            .HasColumnName("CustomerEmail")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.CustomerPhone)
            .HasColumnName("CustomerPhone")
            .HasMaxLength(50);

        b.Property(x => x.AppCallbackUrl)
            .HasColumnName("AppCallbackUrl")
            .HasMaxLength(500);

        b.Property(x => x.AppApiKey)
            .HasColumnName("AppApiKey")
            .HasMaxLength(4000);

        b.Property(x => x.RequestSource)
            .HasColumnName("RequestSource")
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(1);

        b.Property(x => x.SuccessRedirectUrl)
            .HasColumnName("SuccessRedirectUrl")
            .HasMaxLength(500);

        b.Property(x => x.FailureRedirectUrl)
            .HasColumnName("FailureRedirectUrl")
            .HasMaxLength(500);

        b.Property(x => x.CallbackUrl)
            .HasColumnName("CallbackUrl")
            .HasMaxLength(500);

        b.Property(x => x.CancelUrl)
            .HasColumnName("CancelUrl")
            .HasMaxLength(500);

        b.Property(x => x.ErrorMessage)
            .HasColumnName("ErrorMessage")
            .HasMaxLength(1000);

        b.Property(x => x.ErrorCode)
            .HasColumnName("ErrorCode")
            .HasMaxLength(50);

        b.Property(x => x.Metadata)
            .HasColumnName("Metadata")
            .HasMaxLength(4000);

        b.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired()
            .HasDefaultValueSql("getutcdate()");

        b.Property(x => x.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .IsRequired()
            .HasDefaultValueSql("getutcdate()");

        b.Property(x => x.CompletedAt)
            .HasColumnName("CompletedAt");

        b.Property(x => x.IsDeleted)
            .HasColumnName("IsDeleted")
            .IsRequired()
            .HasDefaultValue(false);

        b.Property(x => x.DeletedAt)
            .HasColumnName("DeletedAt");

        b.Property(x => x.HeaderPaymentGuid)
            .HasColumnName("HeaderPaymentGuid");

        // Large text payload columns
        b.Property(x => x.ClientCallbackRequestPayload)
            .HasColumnName("ClientCallbackRequestPayload")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.ClientCallbackResponsePayload)
            .HasColumnName("ClientCallbackResponsePayload")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.GatewayCallbackPayload)
            .HasColumnName("GatewayCallbackPayload")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.GatewayRequestPayload)
            .HasColumnName("GatewayRequestPayload")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.GatewayResponsePayload)
            .HasColumnName("GatewayResponsePayload")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.InitiationRequestPayload)
            .HasColumnName("InitiationRequestPayload")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.InitiationResponsePayload)
            .HasColumnName("InitiationResponsePayload")
            .HasColumnType("nvarchar(max)");

        // Relationships
        b.HasMany(x => x.CallbackLogs)
            .WithOne(x => x.Transaction)
            .HasForeignKey(x => x.TransactionId)
            .HasPrincipalKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.StatusHistory)
            .WithOne(x => x.Transaction)
            .HasForeignKey(x => x.TransactionId)
            .HasPrincipalKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
