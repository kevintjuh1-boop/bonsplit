using Microsoft.Extensions.Configuration;
using PrivateExpenses.Application.Abstractions.Storage;

namespace PrivateExpenses.Infrastructure.Storage;

/// <summary>
/// Stores profile photos on the local filesystem, outside wwwroot, under a fully random file name —
/// same approach as <see cref="LocalReceiptStorage"/>, kept as a separate class/root path since these
/// are a different kind of file with a different lifecycle (replaced in place, not accumulated).
/// </summary>
public class LocalPersonAvatarStorage(IConfiguration configuration) : IPersonAvatarStorage
{
    private string RootPath => configuration["PersonAvatarStorage:RootPath"]
        ?? throw new InvalidOperationException("PersonAvatarStorage:RootPath is niet geconfigureerd.");

    public async Task<StoredAvatarFile> SaveAsync(
        Stream content, string originalFileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(RootPath, storedFileName);

        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var fileSize = new FileInfo(fullPath).Length;
        return new StoredAvatarFile(storedFileName, fileSize);
    }

    public Task<Stream> OpenAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveExistingPath(storedFileName);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(RootPath, storedFileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolveExistingPath(string storedFileName)
    {
        // storedFileName is always a GUID we generated ourselves, but resolve+verify it stays inside
        // RootPath regardless — defence in depth against path traversal.
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, storedFileName));
        var rootFullPath = Path.GetFullPath(RootPath);
        if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new FileNotFoundException("Profielfoto niet gevonden.", storedFileName);
        }

        return fullPath;
    }
}
