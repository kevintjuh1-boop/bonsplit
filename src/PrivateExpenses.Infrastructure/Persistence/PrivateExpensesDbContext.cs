using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence;

public class PrivateExpensesDbContext(DbContextOptions<PrivateExpensesDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<ExpenseItemShare> ExpenseItemShares => Set<ExpenseItemShare>();
    public DbSet<ExpensePayment> ExpensePayments => Set<ExpensePayment>();
    public DbSet<ReceiptDocument> ReceiptDocuments => Set<ReceiptDocument>();
    public DbSet<ReceiptDocumentPage> ReceiptDocumentPages => Set<ReceiptDocumentPage>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrivateExpensesDbContext).Assembly);
    }
}
