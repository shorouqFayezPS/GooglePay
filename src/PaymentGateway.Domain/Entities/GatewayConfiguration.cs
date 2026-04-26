using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Domain.Entities;

/// <summary>
/// Maps to Payment.GatewayConfigurations table.
/// </summary>
public class GatewayConfiguration
{
    public int Id { get; private set; }
    public int CountryId { get; private set; }
    public GatewayType GatewayType { get; private set; }
    public string Name { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string ApiBaseUrl { get; private set; } = null!;
    public string Environment { get; private set; } = null!;
    public string? WebsiteUrl { get; private set; }
    public string CredentialsJson { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime? ModifiedDate { get; private set; }
    public string? ModifiedBy { get; private set; }

    private GatewayConfiguration() { }
}
