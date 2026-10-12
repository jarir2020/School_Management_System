using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

/// <summary>
/// Keeps school-specific contact details separate from ASP.NET Core Identity data.
/// </summary>
public sealed class UserProfile
{
    [Required, StringLength(450)]
    public string UserId { get; set; } = "";

    [Required, StringLength(160)]
    public string FullName { get; set; } = "";

    [Phone, StringLength(40)]
    public string Phone { get; set; } = "";
}
