using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Entities;

namespace SchoolManagement.Application.SchoolSetup;

/// <summary>
/// Applies school setup rules before data is saved.
/// </summary>
public sealed class SchoolSetupService(IApplicationDbContextFactory contextFactory) : ISchoolSetupService
{
    public async Task<SchoolProfile> GetSchoolProfileAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.SchoolProfiles.SingleOrDefaultAsync(
            item => item.Id == SchoolProfile.PrimaryProfileId,
            cancellationToken);

        return profile ?? new SchoolProfile();
    }

    public async Task SaveSchoolProfileAsync(SchoolProfileInput input, CancellationToken cancellationToken = default)
    {
        var name = RequiredText(input.Name, "School name", 160);
        var address = OptionalText(input.Address, 500);
        var phone = OptionalText(input.Phone, 40);
        var email = OptionalText(input.Email, 254);
        var timeZoneId = RequiredText(input.TimeZoneId, "Time zone", 100);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.SchoolProfiles.SingleOrDefaultAsync(
            item => item.Id == SchoolProfile.PrimaryProfileId,
            cancellationToken);

        if (profile is null)
        {
            profile = new SchoolProfile();
            db.SchoolProfiles.Add(profile);
        }

        profile.Name = name;
        profile.Address = address;
        profile.Phone = phone;
        profile.Email = email;
        profile.TimeZoneId = timeZoneId;
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AcademicYear>> GetAcademicYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.AcademicYears.AsNoTracking()
            .OrderByDescending(year => year.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAcademicYearAsync(AcademicYearInput input, CancellationToken cancellationToken = default)
    {
        var details = ValidateAcademicYear(input);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureUniqueAcademicYearNameAsync(db, details.Name, null, cancellationToken);

        db.AcademicYears.Add(new AcademicYear
        {
            Name = details.Name,
            StartDate = details.StartDate,
            EndDate = details.EndDate
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAcademicYearAsync(int id, AcademicYearInput input, CancellationToken cancellationToken = default)
    {
        var details = ValidateAcademicYear(input);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var year = await db.AcademicYears.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected academic year no longer exists.");

        await EnsureUniqueAcademicYearNameAsync(db, details.Name, id, cancellationToken);
        year.Name = details.Name;
        year.StartDate = details.StartDate;
        year.EndDate = details.EndDate;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAcademicYearAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var years = await db.AcademicYears.ToListAsync(cancellationToken);
        if (years.All(year => year.Id != id))
        {
            throw new KeyNotFoundException("The selected academic year no longer exists.");
        }

        foreach (var year in years)
        {
            year.IsActive = year.Id == id;
        }

        // EF Core wraps these updates in one database transaction when saving.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAcademicYearAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var year = await db.AcademicYears.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected academic year no longer exists.");

        if (year.IsActive)
        {
            throw new InvalidOperationException("Select another active academic year before deleting this one.");
        }

        db.AcademicYears.Remove(year);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SchoolClass>> GetClassesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Classes.AsNoTracking()
            .Include(schoolClass => schoolClass.Sections)
            .OrderBy(schoolClass => schoolClass.GradeLevel)
            .ThenBy(schoolClass => schoolClass.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateClassAsync(SchoolClassInput input, CancellationToken cancellationToken = default)
    {
        var details = ValidateClass(input);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureUniqueClassAsync(db, details, null, cancellationToken);

        db.Classes.Add(new SchoolClass { Name = details.Name, GradeLevel = details.GradeLevel });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateClassAsync(int id, SchoolClassInput input, CancellationToken cancellationToken = default)
    {
        var details = ValidateClass(input);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected class no longer exists.");

        await EnsureUniqueClassAsync(db, details, id, cancellationToken);
        schoolClass.Name = details.Name;
        schoolClass.GradeLevel = details.GradeLevel;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteClassAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected class no longer exists.");

        if (await db.Sections.AnyAsync(section => section.ClassId == id, cancellationToken))
        {
            throw new InvalidOperationException("Delete the class sections before deleting the class.");
        }

        db.Classes.Remove(schoolClass);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Section>> GetSectionsAsync(int? classId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Sections.AsNoTracking().Include(section => section.Class).AsQueryable();
        if (classId.HasValue)
        {
            query = query.Where(section => section.ClassId == classId.Value);
        }

        return await query.OrderBy(section => section.Class!.GradeLevel)
            .ThenBy(section => section.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateSectionAsync(SectionInput input, CancellationToken cancellationToken = default)
    {
        var details = ValidateSection(input);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Classes.AnyAsync(item => item.Id == details.ClassId, cancellationToken))
        {
            throw new KeyNotFoundException("Select an existing class for this section.");
        }

        await EnsureUniqueSectionAsync(db, details, null, cancellationToken);
        db.Sections.Add(new Section
        {
            ClassId = details.ClassId,
            Name = details.Name,
            Capacity = details.Capacity
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSectionAsync(int id, SectionInput input, CancellationToken cancellationToken = default)
    {
        var details = ValidateSection(input);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var section = await db.Sections.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected section no longer exists.");

        if (!await db.Classes.AnyAsync(item => item.Id == details.ClassId, cancellationToken))
        {
            throw new KeyNotFoundException("Select an existing class for this section.");
        }

        await EnsureUniqueSectionAsync(db, details, id, cancellationToken);
        section.ClassId = details.ClassId;
        section.Name = details.Name;
        section.Capacity = details.Capacity;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSectionAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var section = await db.Sections.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected section no longer exists.");

        db.Sections.Remove(section);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static (string Name, DateOnly StartDate, DateOnly EndDate) ValidateAcademicYear(AcademicYearInput input)
    {
        var name = RequiredText(input.Name, "Academic year name", 32);
        if (input.StartDate >= input.EndDate)
        {
            throw new ArgumentException("The end date must be after the start date.");
        }

        return (name, input.StartDate, input.EndDate);
    }

    private static async Task EnsureUniqueAcademicYearNameAsync(
        IApplicationDbContext db,
        string name,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        var duplicate = await db.AcademicYears.AnyAsync(
            year => year.Name.ToUpper() == name.ToUpper() && (!exceptId.HasValue || year.Id != exceptId.Value),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("An academic year with this name already exists.");
        }
    }

    private static (string Name, int GradeLevel) ValidateClass(SchoolClassInput input)
    {
        var name = RequiredText(input.Name, "Class name", 80);
        if (input.GradeLevel is < 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(input.GradeLevel), "Grade level must be between 0 and 20.");
        }

        return (name, input.GradeLevel);
    }

    private static async Task EnsureUniqueClassAsync(
        IApplicationDbContext db,
        (string Name, int GradeLevel) details,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        var duplicate = await db.Classes.AnyAsync(
            item => (item.Name.ToUpper() == details.Name.ToUpper() || item.GradeLevel == details.GradeLevel)
                && (!exceptId.HasValue || item.Id != exceptId.Value),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("A class with this name or grade level already exists.");
        }
    }

    private static (int ClassId, string Name, int Capacity) ValidateSection(SectionInput input)
    {
        var name = RequiredText(input.Name, "Section name", 40);
        if (input.ClassId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(input.ClassId), "Choose a class.");
        }

        if (input.Capacity is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(input.Capacity), "Section capacity must be between 1 and 500.");
        }

        return (input.ClassId, name, input.Capacity);
    }

    private static async Task EnsureUniqueSectionAsync(
        IApplicationDbContext db,
        (int ClassId, string Name, int Capacity) details,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        var duplicate = await db.Sections.AnyAsync(
            item => item.ClassId == details.ClassId
                && item.Name.ToUpper() == details.Name.ToUpper()
                && (!exceptId.HasValue || item.Id != exceptId.Value),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("This section name already exists in the selected class.");
        }
    }

    private static string RequiredText(string? value, string displayName, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{displayName} is required.");
        }

        if (text.Length > maxLength)
        {
            throw new ArgumentException($"{displayName} must be {maxLength} characters or fewer.");
        }

        return text;
    }

    private static string OptionalText(string? value, int maxLength)
    {
        var text = value?.Trim() ?? "";
        if (text.Length > maxLength)
        {
            throw new ArgumentException($"Text must be {maxLength} characters or fewer.");
        }

        return text;
    }
}
