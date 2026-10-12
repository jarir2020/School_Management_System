namespace SchoolManagement.Core.Enums;

public enum PaymentStatus
{
    PendingGateway,
    Succeeded,
    ReviewRequired,
    Failed,
    Cancelled
}
