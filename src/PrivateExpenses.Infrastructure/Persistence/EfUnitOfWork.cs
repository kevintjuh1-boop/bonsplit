using Microsoft.EntityFrameworkCore.Storage;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Infrastructure.Persistence.Repositories;

namespace PrivateExpenses.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly PrivateExpensesDbContext _context;

    public EfUnitOfWork(PrivateExpensesDbContext context)
    {
        _context = context;
        Persons = new PersonRepository(context);
        Categories = new CategoryRepository(context);
        Expenses = new ExpenseRepository(context);
        Settlements = new SettlementRepository(context);
        ReceiptDocuments = new ReceiptDocumentRepository(context);
    }

    public IPersonRepository Persons { get; }
    public ICategoryRepository Categories { get; }
    public IExpenseRepository Expenses { get; }
    public ISettlementRepository Settlements { get; }
    public IReceiptDocumentRepository ReceiptDocuments { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        // SQLite has no transient-fault retry semantics worth an IExecutionStrategy here; a single
        // straightforward transaction is sufficient and keeps rollback behavior easy to reason about.
        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action();
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
