using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Infrastructure.Persistence.Configurations;

public sealed class CallbackLogConfiguration : IEntityTypeConfiguration<CallbackLog>
{
    public void Configure(EntityTypeBuilder<CallbackLog> b)
    {
        b.ToTable("CallbackLogs", "Payment");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).UseIdentityColumn();

        b.Property(x => x.TransactionId)
            .HasColumnName("TransactionId")
            .IsRequired()
            .HasComment("Transaction this callback is for");

        b.Property(x => x.CallbackType)
            .HasColumnName("CallbackType")
            .HasMaxLength(50)
            .IsRequired()
            .HasComment("Type: Gateway or V8");

        b.Property(x => x.Url)
            .HasColumnName("Url")
            .HasMaxLength(500)
            .IsRequired();

        b.Property(x => x.HttpMethod)
            .HasColumnName("HttpMethod")
            .HasMaxLength(10);

        b.Property(x => x.ClientIpAddress)
            .HasColumnName("ClientIpAddress")
            .HasMaxLength(50);

        b.Property(x => x.RequestPayload)
            .HasColumnName("RequestPayload")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.ResponseStatusCode)
            .HasColumnName("ResponseStatusCode");

        b.Property(x => x.ResponseBody)
            .HasColumnName("ResponseBody")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.Success)
            .HasColumnName("Success")
            .IsRequired();

        b.Property(x => x.ErrorMessage)
            .HasColumnName("ErrorMessage")
            .HasMaxLength(1000);

        b.Property(x => x.IdempotencyHash)
            .HasColumnName("IdempotencyHash")
            .HasMaxLength(64)
            .HasComment("SHA256 hash for duplicate detection");

        b.Property(x => x.IsProcessed)
            .HasColumnName("IsProcessed")
            .IsRequired()
            .HasDefaultValue(false);

        b.Property(x => x.ProcessingResult)
            .HasColumnName("ProcessingResult")
            .HasMaxLength(50)
            .HasComment("Result: Success, Failed, Duplicate, Error");

        b.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired()
            .HasDefaultValueSql("getutcdate()");

        b.HasIndex(x => x.IdempotencyHash)
            .HasDatabaseName("IX_CallbackLogs_IdempotencyHash");
    }
}

public sealed class PaymentStatusHistoryConfiguration
    : IEntityTypeConfiguration<PaymentStatusHistory>
{
    public void Configure(EntityTypeBuilder<PaymentStatusHistory> b)
    {
        b.ToTable("PaymentStatusHistory", "Payment");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).UseIdentityColumn();

        b.Property(x => x.TransactionId)
            .HasColumnName("TransactionId")
            .IsRequired();

        b.Property(x => x.PreviousStatus)
            .HasColumnName("PreviousStatus")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.NewStatus)
            .HasColumnName("NewStatus")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.Reason)
            .HasColumnName("Reason")
            .HasMaxLength(500);

        b.Property(x => x.Metadata)
            .HasColumnName("Metadata")
            .HasColumnType("nvarchar(max)");

        b.Property(x => x.ChangedAt)
            .HasColumnName("ChangedAt")
            .IsRequired()
            .HasDefaultValueSql("getutcdate()");

        b.Property(x => x.ChangedBy)
            .HasColumnName("ChangedBy")
            .HasMaxLength(200);
    }
}

public sealed class GatewayConfigurationConfiguration
    : IEntityTypeConfiguration<GatewayConfiguration>
{
    public void Configure(EntityTypeBuilder<GatewayConfiguration> b)
    {
        b.ToTable("GatewayConfigurations", "Payment");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id).UseIdentityColumn();

        b.Property(x => x.CountryId)
            .HasColumnName("CountryId")
            .IsRequired();

        b.Property(x => x.GatewayType)
            .HasColumnName("GatewayType")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.Name)
            .HasColumnName("Name")
            .HasMaxLength(100)
            .IsRequired();

        b.Property(x => x.DisplayName)
            .HasColumnName("DisplayName")
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.ApiBaseUrl)
            .HasColumnName("ApiBaseUrl")
            .HasMaxLength(500)
            .IsRequired();

        b.Property(x => x.Environment)
            .HasColumnName("Environment")
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("Production");

        b.Property(x => x.WebsiteUrl)
            .HasColumnName("WebsiteUrl")
            .HasMaxLength(500);

        b.Property(x => x.CredentialsJson)
            .HasColumnName("CredentialsJson")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        b.Property(x => x.IsDeleted)
            .HasColumnName("IsDeleted")
            .IsRequired()
            .HasDefaultValue(false);

        b.Property(x => x.CreatedDate)
            .HasColumnName("CreatedDate")
            .IsRequired()
            .HasDefaultValueSql("getutcdate()");

        b.Property(x => x.CreatedBy)
            .HasColumnName("CreatedBy")
            .HasMaxLength(100)
            .IsRequired();

        b.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate");

        b.Property(x => x.ModifiedBy)
            .HasColumnName("ModifiedBy")
            .HasMaxLength(100);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
