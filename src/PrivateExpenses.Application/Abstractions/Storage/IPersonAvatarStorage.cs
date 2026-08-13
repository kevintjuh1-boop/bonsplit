namespace PrivateExpenses.Application.Abstractions.Storage;

public sealed record StoredAvatarFile(string StoredFileName, long FileSize);

/// <summary>
/// Persists uploaded profile photos, separately from <see cref="IReceiptStorage"/> since these are a
/// different kind of file with their own storage location and lifecycle (one per person, replaced
/// in place rather than accumulated).
/// </summary>
public interface IPersonAvatarStorage
{
    /// <summary>Stores <paramref name="content"/> under a new random file name (never the original file
    /// name) and returns the generated name and size.</summary>
    Task<StoredAvatarFile> SaveAsync(
        Stream content, string originalFileName, CancellationToken cancellationToken = default);

    Task<Stream> OpenAsync(string storedFileName, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);
}
