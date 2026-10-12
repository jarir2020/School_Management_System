namespace SchoolManagement.Application.Admissions;

public interface IPrivateDocumentStore
{
    Task<string> SaveAsync(ReadOnlyMemory<byte> content, string extension, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
