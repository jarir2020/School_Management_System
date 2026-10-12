namespace SchoolManagement.Infrastructure.Payments;

public sealed class SslCommerzOptions
{
    public const string SectionName = "Payments:SslCommerz";

    public string StoreId { get; set; } = "";

    public string StorePassword { get; set; } = "";

    public string PublicBaseUrl { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(StoreId) && !string.IsNullOrWhiteSpace(StorePassword);
}
