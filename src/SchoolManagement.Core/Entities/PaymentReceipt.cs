using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class PaymentReceipt
{
    public int Id { get; set; }

    public int PaymentId { get; set; }

    [Required, StringLength(40)]
    public string ReceiptNumber { get; set; } = "";

    public DateTimeOffset IssuedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Payment? Payment { get; set; }
}
