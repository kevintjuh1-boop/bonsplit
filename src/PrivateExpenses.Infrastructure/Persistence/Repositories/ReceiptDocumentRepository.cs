using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Domain.Entities;
using PrivateExpenses.Domain.Enums;

namespace PrivateExpenses.Infrastructure.Persistence.Repositories;

public class ReceiptDocumentRepository(PrivateExpensesDbContext context) : IReceiptDocumentRepository
{
    public Task<ReceiptDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ReceiptDocuments
            .Include(d => d.Expense)
            .Include(d => d.ExtraPages)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<ReceiptDocumentPage?> GetPageByIdAsync(Guid pageId, CancellationToken cancellationToken = default) =>
        context.ReceiptDocumentPages.FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);

    public Task<List<ReceiptDocument>> GetByHashAsync(string sha256Hash, CancellationToken cancellationToken = default) =>
        context.ReceiptDocuments.AsNoTracking()
            .Include(d => d.Expense)
            .Where(d => d.FileHash == sha256Hash)
            .ToListAsync(cancellationToken);

    public Task<List<ReceiptDocument>> FindPossibleDuplicatesAsync(
        string? merchantName, DateOnly? expenseDate, long? totalCents, CancellationToken cancellationToken = default)
    {
        if (merchantName is null || expenseDate is null || totalCents is null)
        {
            return Task.FromResult(new List<ReceiptDocument>());
        }

        return context.ReceiptDocuments.AsNoTracking()
            .Include(d => d.Expense)
            .Where(d => d.Expense != null
                && !d.Expense.IsDeleted
                && d.Expense.MerchantName == merchantName
                && d.Expense.ExpenseDate == expenseDate
                && d.Expense.TotalCents == totalCents)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ReceiptDocument>> GetPendingReviewAsync(CancellationToken cancellationToken = default) =>
        context.ReceiptDocuments.AsNoTracking()
            .Where(d => d.ExpenseId == null && (d.ParsingStatus == ParsingStatus.NeedsReview || d.ParsingStatus == ParsingStatus.Failed))
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ReceiptDocument document, CancellationToken cancellationToken = default) =>
        await context.ReceiptDocuments.AddAsync(document, cancellationToken);

    public void Update(ReceiptDocument document) => context.ReceiptDocuments.Update(document);

    public void Delete(ReceiptDocument document) => context.ReceiptDocuments.Remove(document);
}
