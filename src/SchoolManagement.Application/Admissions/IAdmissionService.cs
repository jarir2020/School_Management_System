namespace SchoolManagement.Application.Admissions;

public interface IAdmissionService
{
    Task<IReadOnlyList<AdmissionAcademicYear>> GetAcademicYearsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdmissionSection>> GetSectionsAsync(int academicYearId, CancellationToken cancellationToken = default);

    Task<AdmissionReceipt> SubmitAsync(
        AdmissionSubmissionInput input,
        IReadOnlyCollection<AdmissionDocumentUpload> documents,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AdmissionSummary>> SearchApplicationsAsync(
        AdmissionSearchFilter filter,
        CancellationToken cancellationToken = default);

    Task<AdmissionDetails?> GetApplicationAsync(int id, CancellationToken cancellationToken = default);

    Task<AdmissionDecisionResult> DecideAsync(
        int applicationId,
        AdmissionReviewInput input,
        string reviewerUserId,
        CancellationToken cancellationToken = default);

    Task<PrivateDocumentDownload?> GetDocumentAsync(string actorUserId, int documentId, CancellationToken cancellationToken = default);
}
