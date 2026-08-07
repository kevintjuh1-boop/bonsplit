using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using PrivateExpenses.Application.Abstractions.Storage;

namespace PrivateExpenses.Infrastructure.Storage;

/// <summary>
/// Stores receipt files on the local filesystem, outside wwwroot, under a fully random file name
/// (the original name is kept only as metadata on ReceiptDocument, per section 8). This is the first
/// implementation of <see cref="IReceiptStorage"/>; a cloud provider (Azure Blob, S3, R2, ...) can
/// replace it later without any caller changes.
/// </summary>
public class LocalReceiptStorage(IConfiguration configuration) : IReceiptStorage
{
    private string RootPath => configuration["ReceiptStorage:RootPath"]
        ?? throw new InvalidOperationException("ReceiptStorage:RootPath is niet geconfigureerd.");

    public async Task<StoredReceiptFile> SaveAsync(
        Stream content, string originalFileName, string mimeType, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(RootPath, storedFileName);

        using var sha256 = SHA256.Create();
        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var hashingStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(hashingStream, cancellationToken);
        }

        var fileSize = new FileInfo(fullPath).Length;
        var hash = Convert.ToHexStringLower(sha256.Hash!);

        return new StoredReceiptFile(storedFileName, fileSize, hash);
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
            throw new FileNotFoundException("Bondocument niet gevonden.", storedFileName);
        }

        return fullPath;
    }
}
