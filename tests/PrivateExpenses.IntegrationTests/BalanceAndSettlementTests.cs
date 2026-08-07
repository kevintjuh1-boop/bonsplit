using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Application.Services;
using PrivateExpenses.Domain.Entities;
using PrivateExpenses.IntegrationTests.TestSupport;

namespace PrivateExpenses.IntegrationTests;

public class BalanceAndSettlementTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;
    private ExpenseService _expenseService = null!;
    private BalanceService _balanceService = null!;
    private SettlementService _settlementService = null!;
    private Person _kevin = null!;
    private Person _wesley = null!;
    private Person _jos = null!;

    public async Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        _expenseService = new ExpenseService(_db.UnitOfWorkFactory);
        _balanceService = new BalanceService(_db.UnitOfWorkFactory);
        _settlementService = new SettlementService(_db.UnitOfWorkFactory);
        var people = await _db.GetPeopleAsync();
        _kevin = people.Single(p => p.Name == "Kevin");
        _wesley = people.Single(p => p.Name == "Wesley");
        _jos = people.Single(p => p.Name == "Jos");
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task PartialSettlement_ReducesOnlyThePayingPersonsDebt_OriginalExpenseUnchanged()
    {
        // Acceptance scenarios (spec 105-106): a €90 receipt paid entirely by Kevin, split evenly three
        // ways (€30 each), leaves Kevin +€60 and Wesley/Jos -€30 each — i.e. Wesley and Jos each owe
        // Kevin €30. A subsequent €10 partial settlement from Wesley to Kevin must reduce only Wesley's
        // debt to €20, leave Jos's €30 untouched, and never mutate the original expense.
        var expenseId = await _expenseService.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Restaurant De Kroon",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 9000,
            Items = [new ExpenseItemInput { Description = "Diner", TotalCents = 9000, ParticipantPersonIdsInOrder = [_kevin.Id, _wesley.Id, _jos.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 9000 }],
        });

        var balancesBeforeSettlement = await _balanceService.GetPersonBalancesAsync();
        Assert.Equal(6000, balancesBeforeSettlement.Single(b => b.PersonId == _kevin.Id).NetBalanceCents);
        Assert.Equal(-3000, balancesBeforeSettlement.Single(b => b.PersonId == _wesley.Id).NetBalanceCents);
        Assert.Equal(-3000, balancesBeforeSettlement.Single(b => b.PersonId == _jos.Id).NetBalanceCents);

        await _settlementService.CreateAsync(_wesley.Id, _kevin.Id, 1000, DateOnly.FromDateTime(DateTime.Today), note: null);

        var debts = await _balanceService.GetSuggestedDebtsAsync();
        var wesleyDebt = debts.Single(d => d.FromPersonId == _wesley.Id);
        var josDebt = debts.Single(d => d.FromPersonId == _jos.Id);

        Assert.Equal(2000, wesleyDebt.AmountCents);
        Assert.Equal(3000, josDebt.AmountCents);
        Assert.Equal(_kevin.Id, wesleyDebt.ToPersonId);
        Assert.Equal(_kevin.Id, josDebt.ToPersonId);

        var originalExpense = await _expenseService.GetDetailAsync(expenseId);
        Assert.NotNull(originalExpense);
        Assert.Equal(9000, originalExpense.TotalCents);
        Assert.False(originalExpense.IsDeleted);
    }

    [Fact]
    public async Task GetSuggestedDebtsAsync_ZeroNetBalances_ReturnsNoSuggestedPayments()
    {
        var debts = await _balanceService.GetSuggestedDebtsAsync();

        Assert.Empty(debts);
    }
}
