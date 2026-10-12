using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class Section
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    [Required, StringLength(40)]
    public string Name { get; set; } = "";

    [Range(1, 500)]
    public int Capacity { get; set; }

    public SchoolClass? Class { get; set; }
}
