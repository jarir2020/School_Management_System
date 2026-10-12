using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class Announcement
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Title { get; set; } = "";

    [Required, StringLength(8000)]
    public string Content { get; set; } = "";

    [Required, StringLength(20)]
    public string Audience { get; set; } = "Everyone";

    public DateTimeOffset PublishedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [Required, StringLength(450)]
    public string PublishedByUserId { get; set; } = "";
}
