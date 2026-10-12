using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class Subject
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Required, StringLength(24)]
    public string Code { get; set; } = "";

    [Range(1, 10000)]
    public int FullMarks { get; set; } = 100;
}
