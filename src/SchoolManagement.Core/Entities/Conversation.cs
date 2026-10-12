using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class Conversation
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    [Required, StringLength(450)]
    public string ParentUserId { get; set; } = "";

    [Required, StringLength(450)]
    public string TeacherUserId { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastMessageAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Student? Student { get; set; }

    public ICollection<SchoolMessage> Messages { get; set; } = new List<SchoolMessage>();
}
