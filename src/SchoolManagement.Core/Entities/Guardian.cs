using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class Guardian
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string FullName { get; set; } = "";

    [Required, StringLength(60)]
    public string Relationship { get; set; } = "";

    [Required, StringLength(40)]
    public string Phone { get; set; } = "";

    [StringLength(254)]
    public string Email { get; set; } = "";

    [StringLength(500)]
    public string Address { get; set; } = "";

    public ICollection<StudentGuardian> Students { get; set; } = new List<StudentGuardian>();
}
