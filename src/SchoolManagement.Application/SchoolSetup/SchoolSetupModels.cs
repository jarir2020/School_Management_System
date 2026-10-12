namespace SchoolManagement.Application.SchoolSetup;

public sealed record SchoolProfileInput(string Name, string Address, string Phone, string Email, string TimeZoneId);

public sealed record AcademicYearInput(string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record SchoolClassInput(string Name, int GradeLevel);

public sealed record SectionInput(int ClassId, string Name, int Capacity);
