using PrivateExpenses.Application.Abstractions.Parsing;
using PrivateExpenses.Application.Dtos.Receipts;

namespace PrivateExpenses.Infrastructure.Parsing;

/// <summary>
/// Safe default when no real receipt-recognition provider is configured. It never pretends to have
/// read a receipt (section 60) — every call fails cleanly and routes the user to manual entry, which
/// stays fully functional without AI (section 96).
/// </summary>
public class DevelopmentReceiptParser : IReceiptParser
{
    public string ProviderName => "development";

    public Task<ReceiptParseResult> ParseAsync(ReceiptParseRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReceiptParseResult.Failed(
            "Automatische bonherkenning is niet geconfigureerd in deze omgeving. Vul de bon handmatig in."));
}
