using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class SchoolClass
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = "";

    [Range(0, 20)]
    public int GradeLevel { get; set; }

    public ICollection<Section> Sections { get; set; } = new List<Section>();
}
