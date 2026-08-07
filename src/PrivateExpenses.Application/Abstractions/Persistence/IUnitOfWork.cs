namespace PrivateExpenses.Application.Abstractions.Persistence;

/// <summary>
/// Owns one short-lived DbContext (created fresh per unit of work, per section 46 of the spec — Blazor
/// Interactive Server components must never share one long-lived context). Application services request
/// a unit of work from <see cref="IUnitOfWorkFactory"/>, use its repositories, call
/// <see cref="SaveChangesAsync"/> (or <see cref="ExecuteInTransactionAsync"/> for multi-step writes that
/// must be atomic), and dispose it.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IPersonRepository Persons { get; }
    ICategoryRepository Categories { get; }
    IExpenseRepository Expenses { get; }
    ISettlementRepository Settlements { get; }
    IReceiptDocumentRepository ReceiptDocuments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="action"/> and commits all repository changes as a single database
    /// transaction; rolls back entirely if any step throws. Use for multi-entity writes (e.g. saving an
    /// Expense together with its items, shares and payments) that must never be left half-saved.</summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken = default);
}
