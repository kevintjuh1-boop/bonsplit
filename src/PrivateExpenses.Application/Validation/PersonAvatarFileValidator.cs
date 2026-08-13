namespace PrivateExpenses.Application.Validation;

public sealed record PersonAvatarFileValidationResult(bool IsValid, string? ErrorMessage)
{
    public static PersonAvatarFileValidationResult Valid() => new(true, null);
    public static PersonAvatarFileValidationResult Invalid(string message) => new(false, message);
}

/// <summary>
/// Server-side profile-photo upload validation — images only, smaller size cap than receipts since
/// this is a single portrait photo, not a scanned document. Never trusts the browser's reported MIME
/// type or file extension alone, same as <see cref="ReceiptFileValidator"/>.
/// </summary>
public static class PersonAvatarFileValidator
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp",
    };

    public static PersonAvatarFileValidationResult Validate(string originalFileName, string mimeType, long fileSize, ReadOnlySpan<byte> headerBytes)
    {
        if (fileSize <= 0)
        {
            return PersonAvatarFileValidationResult.Invalid("Dit bestand is leeg.");
        }

        if (fileSize > MaxFileSizeBytes)
        {
            return PersonAvatarFileValidationResult.Invalid($"Deze foto is te groot. Maximaal {MaxFileSizeBytes / 1024 / 1024} MB.");
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return PersonAvatarFileValidationResult.Invalid("Alleen JPG, PNG en WEBP foto's zijn toegestaan.");
        }

        if (!AllowedMimeTypes.Contains(mimeType))
        {
            return PersonAvatarFileValidationResult.Invalid("Alleen JPG, PNG en WEBP foto's zijn toegestaan.");
        }

        if (!MatchesKnownFileSignature(mimeType, headerBytes))
        {
            return PersonAvatarFileValidationResult.Invalid("Dit bestand lijkt beschadigd of is geen geldige foto van het opgegeven type.");
        }

        return PersonAvatarFileValidationResult.Valid();
    }

    private static bool MatchesKnownFileSignature(string mimeType, ReadOnlySpan<byte> header)
    {
        return mimeType switch
        {
            "image/jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            "image/webp" => header.Length >= 12
                && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P',
            _ => false,
        };
    }
}
