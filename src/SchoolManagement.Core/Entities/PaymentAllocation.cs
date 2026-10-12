namespace SchoolManagement.Core.Entities;

public sealed class PaymentAllocation
{
    public int Id { get; set; }

    public int PaymentId { get; set; }

    public int FeeInvoiceId { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset AllocatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Payment? Payment { get; set; }

    public FeeInvoice? FeeInvoice { get; set; }
}
