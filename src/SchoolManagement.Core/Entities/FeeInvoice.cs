using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class FeeInvoice
{
    public int Id { get; set; }

    public int StudentFeeAssignmentId { get; set; }

    public int StudentId { get; set; }

    [Required, StringLength(40)]
    public string InvoiceNumber { get; set; } = "";

    [Required, StringLength(100)]
    public string FeeName { get; set; } = "";

    public FeeCategory Category { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    [Required, StringLength(3)]
    public string Currency { get; set; } = "BDT";

    public DateOnly DueDate { get; set; }

    public FeeInvoiceStatus Status { get; set; } = FeeInvoiceStatus.Issued;

    public DateTimeOffset IssuedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public StudentFeeAssignment? StudentFeeAssignment { get; set; }

    public Student? Student { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
