namespace PrivateExpenses.Application.Abstractions.Storage;

public sealed record StoredReceiptFile(string StoredFileName, long FileSize, string Sha256Hash);

/// <summary>
/// Persists uploaded receipt files. The first implementation writes to the local filesystem outside
/// wwwroot; a cloud implementation (Azure Blob, S3, Cloudflare R2, ...) can be swapped in later without
/// touching callers, since nothing outside Infrastructure depends on how/where files are stored.
/// </summary>
public interface IReceiptStorage
{
    /// <summary>Stores <paramref name="content"/> under a new random file name (never the original file
    /// name) and returns the generated name, size and content hash.</summary>
    Task<StoredReceiptFile> SaveAsync(
        Stream content, string originalFileName, string mimeType, CancellationToken cancellationToken = default);

    Task<Stream> OpenAsync(string storedFileName, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);
}
