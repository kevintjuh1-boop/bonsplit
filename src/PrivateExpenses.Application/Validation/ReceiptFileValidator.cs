namespace PrivateExpenses.Application.Validation;

public sealed record ReceiptFileValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ReceiptFileValidationResult Valid() => new(true, null);
    public static ReceiptFileValidationResult Invalid(string message) => new(false, message);
}

/// <summary>
/// Server-side receipt upload validation (section 8, 47). Never trust the browser's reported MIME
/// type or file extension alone — the magic-byte check catches a renamed or mislabeled file.
/// </summary>
public static class ReceiptFileValidator
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf",
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf",
    };

    public static ReceiptFileValidationResult Validate(string originalFileName, string mimeType, long fileSize, ReadOnlySpan<byte> headerBytes)
    {
        if (fileSize <= 0)
        {
            return ReceiptFileValidationResult.Invalid("Dit bestand is leeg.");
        }

        if (fileSize > MaxFileSizeBytes)
        {
            return ReceiptFileValidationResult.Invalid($"Dit bestand is te groot. Maximaal {MaxFileSizeBytes / 1024 / 1024} MB.");
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return ReceiptFileValidationResult.Invalid("Alleen JPG, PNG, WEBP en PDF bestanden zijn toegestaan.");
        }

        if (!AllowedMimeTypes.Contains(mimeType))
        {
            return ReceiptFileValidationResult.Invalid("Alleen JPG, PNG, WEBP en PDF bestanden zijn toegestaan.");
        }

        if (!MatchesKnownFileSignature(mimeType, headerBytes))
        {
            return ReceiptFileValidationResult.Invalid("Dit bestand lijkt beschadigd of is geen geldig bestand van het opgegeven type.");
        }

        return ReceiptFileValidationResult.Valid();
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
            "application/pdf" => header.Length >= 4 && header[0] == '%' && header[1] == 'P' && header[2] == 'D' && header[3] == 'F',
            _ => false,
        };
    }
}
