using PrivateExpenses.Application.Dtos.Receipts;

namespace PrivateExpenses.Application.Abstractions.Parsing;

/// <summary>An additional page of the same physical receipt as the request's primary
/// <see cref="ReceiptParseRequest.FileContent"/> — e.g. a second page a store prints its BTW
/// breakdown on. Sent to the provider alongside the primary page as one logical document.</summary>
public sealed record ReceiptParsePage(Stream Content, string MimeType);

public sealed record ReceiptParseRequest(
    Stream FileContent, string MimeType, string OriginalFileName,
    IReadOnlyList<ReceiptParsePage>? ExtraPages = null);

/// <summary>
/// Reads a receipt file and extracts structured data. Fully provider-independent — Blazor components
/// never call a parser directly (section 11); they go through <see cref="Services.IReceiptImportService"/>.
/// Implementations live in Infrastructure and are swapped via configuration
/// (<c>ReceiptParsing:Provider</c>), so switching between a mock, local OCR, or a cloud AI vision
/// provider never touches Application or Web code.
/// </summary>
public interface IReceiptParser
{
    /// <summary>Short machine-readable name stored on ReceiptDocument.ParsingProvider for audit
    /// purposes, e.g. "development", "fixture", "openai-vision".</summary>
    string ProviderName { get; }

    Task<ReceiptParseResult> ParseAsync(ReceiptParseRequest request, CancellationToken cancellationToken = default);
}
