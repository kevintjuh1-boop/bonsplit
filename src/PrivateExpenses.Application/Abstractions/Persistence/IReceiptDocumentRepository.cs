using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Persistence;

public interface IReceiptDocumentRepository
{
    Task<ReceiptDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds prior uploads with the exact same file content, for duplicate-upload detection.</summary>
    Task<List<ReceiptDocument>> GetByHashAsync(string sha256Hash, CancellationToken cancellationToken = default);

    /// <summary>Finds prior expenses that look like the same receipt (merchant + date + total), for
    /// duplicate detection when the file itself differs (e.g. re-scanned).</summary>
    Task<List<ReceiptDocument>> FindPossibleDuplicatesAsync(
        string? merchantName, DateOnly? expenseDate, long? totalCents, CancellationToken cancellationToken = default);

    Task AddAsync(ReceiptDocument document, CancellationToken cancellationToken = default);

    void Update(ReceiptDocument document);
}
