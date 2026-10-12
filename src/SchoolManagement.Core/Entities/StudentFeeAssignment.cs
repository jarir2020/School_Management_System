using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class StudentFeeAssignment
{
    public int Id { get; set; }

    public int FeeStructureId { get; set; }

    public int EnrollmentId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly DueDate { get; set; }

    [Required, StringLength(450)]
    public string AssignedByUserId { get; set; } = "";

    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public FeeStructure? FeeStructure { get; set; }

    public Enrollment? Enrollment { get; set; }

    public FeeInvoice? Invoice { get; set; }

    public ICollection<FeeDiscountRequest> DiscountRequests { get; set; } = new List<FeeDiscountRequest>();
}
