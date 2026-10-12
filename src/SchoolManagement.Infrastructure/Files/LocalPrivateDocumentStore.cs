using System.Text.RegularExpressions;
using SchoolManagement.Application.Admissions;

namespace SchoolManagement.Infrastructure.Files;

/// <summary>
/// Stores uploaded documents outside wwwroot under unpredictable names.
/// </summary>
public sealed partial class LocalPrivateDocumentStore(string rootDirectory) : IPrivateDocumentStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg"
    };

    public async Task<string> SaveAsync(
        ReadOnlyMemory<byte> content,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("This document type is not supported.", nameof(extension));
        }

        Directory.CreateDirectory(rootDirectory);
        var storageKey = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = GetPath(storageKey);
        try
        {
            await using var output = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await output.WriteAsync(content, cancellationToken);
        }
        catch
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            throw;
        }

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            GetPath(storageKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string storageKey)
    {
        if (!StorageKeyPattern().IsMatch(storageKey))
        {
            throw new ArgumentException("The private document key is invalid.", nameof(storageKey));
        }

        return Path.Combine(rootDirectory, storageKey);
    }

    [GeneratedRegex("^[0-9a-f]{32}\\.(pdf|png|jpg)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StorageKeyPattern();
}
