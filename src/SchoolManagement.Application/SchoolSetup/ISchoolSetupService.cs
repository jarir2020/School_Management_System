using SchoolManagement.Core.Entities;

namespace SchoolManagement.Application.SchoolSetup;

public interface ISchoolSetupService
{
    Task<SchoolProfile> GetSchoolProfileAsync(CancellationToken cancellationToken = default);
    Task SaveSchoolProfileAsync(SchoolProfileInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcademicYear>> GetAcademicYearsAsync(CancellationToken cancellationToken = default);
    Task CreateAcademicYearAsync(AcademicYearInput input, CancellationToken cancellationToken = default);
    Task UpdateAcademicYearAsync(int id, AcademicYearInput input, CancellationToken cancellationToken = default);
    Task SetActiveAcademicYearAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAcademicYearAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolClass>> GetClassesAsync(CancellationToken cancellationToken = default);
    Task CreateClassAsync(SchoolClassInput input, CancellationToken cancellationToken = default);
    Task UpdateClassAsync(int id, SchoolClassInput input, CancellationToken cancellationToken = default);
    Task DeleteClassAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Section>> GetSectionsAsync(int? classId = null, CancellationToken cancellationToken = default);
    Task CreateSectionAsync(SectionInput input, CancellationToken cancellationToken = default);
    Task UpdateSectionAsync(int id, SectionInput input, CancellationToken cancellationToken = default);
    Task DeleteSectionAsync(int id, CancellationToken cancellationToken = default);
}
