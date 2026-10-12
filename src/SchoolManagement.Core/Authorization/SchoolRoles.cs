namespace SchoolManagement.Core.Authorization;

public static class SchoolRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string AdmissionOfficer = "Admission Officer";
    public const string Teacher = "Teacher";
    public const string Accountant = "Accountant";
    public const string ParentStudent = "Parent/Student";
    public const string Principal = "Principal";

    public static IReadOnlyList<string> All { get; } =
    [
        SuperAdmin,
        AdmissionOfficer,
        Teacher,
        Accountant,
        ParentStudent,
        Principal
    ];
}
