using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class SchoolMessage
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    [Required, StringLength(450)]
    public string SenderUserId { get; set; } = "";

    [Required, StringLength(4000)]
    public string Content { get; set; } = "";

    public DateTimeOffset SentAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Conversation? Conversation { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}
