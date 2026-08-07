using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Storage;
using PrivateExpenses.Infrastructure.Persistence;
using PrivateExpenses.Infrastructure.Persistence.Seed;
using PrivateExpenses.Infrastructure.Storage;

namespace PrivateExpenses.IntegrationTests.TestSupport;

/// <summary>Spins up a real (file-backed) SQLite database and local upload folder per test class
/// instance, migrated with the same migrations the app ships, so integration tests exercise the
/// actual persistence stack rather than an in-memory stand-in.</summary>
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly string _tempDir;
    private readonly IDbContextFactory<PrivateExpensesDbContext> _contextFactory;

    public IUnitOfWorkFactory UnitOfWorkFactory { get; }
    public IReceiptStorage ReceiptStorage { get; }

    public SqliteTestDatabase()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PrivateExpensesTests", Guid.NewGuid().ToString("N"));
        var uploadsPath = Path.Combine(_tempDir, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var dbPath = Path.Combine(_tempDir, "test.db");
        var options = new DbContextOptionsBuilder<PrivateExpensesDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _contextFactory = new SimpleDbContextFactory(options);
        UnitOfWorkFactory = new EfUnitOfWorkFactory(_contextFactory);

        using (var context = _contextFactory.CreateDbContext())
        {
            context.Database.Migrate();
            DbSeeder.SeedCoreDataAsync(context).GetAwaiter().GetResult();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ReceiptStorage:RootPath"] = uploadsPath })
            .Build();
        ReceiptStorage = new LocalReceiptStorage(configuration);
    }

    public async Task<List<Domain.Entities.Person>> GetPeopleAsync()
    {
        await using var uow = await UnitOfWorkFactory.CreateAsync();
        return await uow.Persons.GetAllAsync(includeInactive: true);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the OS temp folder gets swept eventually regardless.
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Minimal IDbContextFactory that just hands out a new context from fixed options — avoids
/// pulling in the pooled-factory/object-pool machinery for tests, which only ever need short-lived
/// contexts one at a time.</summary>
internal sealed class SimpleDbContextFactory(DbContextOptions<PrivateExpensesDbContext> options)
    : IDbContextFactory<PrivateExpensesDbContext>
{
    public PrivateExpensesDbContext CreateDbContext() => new(options);
}
