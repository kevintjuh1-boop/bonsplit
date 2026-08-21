using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Application.Exceptions;
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

    [Fact]
    public async Task CreateAsync_RegistersASettlement_NotifiesBothTheReceiverAndThePayer()
    {
        await _settlementService.CreateAsync(_wesley.Id, _kevin.Id, 1738, DateOnly.FromDateTime(DateTime.Today), note: null);

        await using var uow = await _db.UnitOfWorkFactory.CreateAsync();
        var kevinNotification = Assert.Single(await uow.Notifications.GetForPersonAsync(_kevin.Id, 10));
        Assert.Contains("Wesley", kevinNotification.Message);
        Assert.Contains("17,38", kevinNotification.Message);

        var wesleyNotification = Assert.Single(await uow.Notifications.GetForPersonAsync(_wesley.Id, 10));
        Assert.Contains("Kevin", wesleyNotification.Message);
        Assert.Contains("17,38", wesleyNotification.Message);

        Assert.Empty(await uow.Notifications.GetForPersonAsync(_jos.Id, 10));
    }

    [Fact]
    public async Task CreateAsync_DepositOnlyRefundSharedAmongThree_TheReceiverOwesTheOthersTheirShare()
    {
        // Kevin returns €4,50 of empties that belonged to all three of them equally (€1,50 each). He's
        // holding money that isn't only his, so — the mirror image of a normal expense — he now owes
        // Wesley and Jos €1,50 each instead of being owed anything.
        await _expenseService.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Jumbo (statiegeld)",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = -450,
            Items = [new ExpenseItemInput { Description = "Emballage", TotalCents = -450, IsDeposit = true, ParticipantPersonIdsInOrder = [_kevin.Id, _wesley.Id, _jos.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = -450 }],
        });

        var balances = await _balanceService.GetPersonBalancesAsync();
        Assert.Equal(-300, balances.Single(b => b.PersonId == _kevin.Id).NetBalanceCents);
        Assert.Equal(150, balances.Single(b => b.PersonId == _wesley.Id).NetBalanceCents);
        Assert.Equal(150, balances.Single(b => b.PersonId == _jos.Id).NetBalanceCents);

        var debts = await _balanceService.GetSuggestedDebtsAsync();
        Assert.Equal(2, debts.Count);
        Assert.All(debts, d => Assert.Equal(_kevin.Id, d.FromPersonId));
        Assert.Equal(150, debts.Single(d => d.ToPersonId == _wesley.Id).AmountCents);
        Assert.Equal(150, debts.Single(d => d.ToPersonId == _jos.Id).AmountCents);
    }

    [Fact]
    public async Task CreateAsync_DepositOnlyRefundForOnePersonAlone_DoesNotAffectAnyonesBalance()
    {
        // Kevin returns only his own empties — the refund is entirely his, so nobody owes anybody
        // anything once it's recorded (paid = owed = -450 for Kevin, nothing at all for the others).
        await _expenseService.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Jumbo (statiegeld)",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = -450,
            Items = [new ExpenseItemInput { Description = "Emballage", TotalCents = -450, IsDeposit = true, ParticipantPersonIdsInOrder = [_kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = -450 }],
        });

        var balances = await _balanceService.GetPersonBalancesAsync();
        Assert.Equal(0, balances.Single(b => b.PersonId == _kevin.Id).NetBalanceCents);
        Assert.DoesNotContain(balances, b => b.PersonId == _wesley.Id && b.NetBalanceCents != 0);
        Assert.DoesNotContain(balances, b => b.PersonId == _jos.Id && b.NetBalanceCents != 0);
        Assert.Empty(await _balanceService.GetSuggestedDebtsAsync());
    }

    [Fact]
    public async Task CreateAsync_NegativeTotalWithARegularPurchaseItem_IsStillRejected()
    {
        // The negative-total allowance is specifically for deposit-only receipts — a negative total
        // alongside a normal (non-deposit) item is still almost certainly a data-entry mistake.
        var ex = await Assert.ThrowsAsync<ExpenseValidationException>(() => _expenseService.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Rare bon",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = -450,
            Items =
            [
                new ExpenseItemInput { Description = "Emballage", TotalCents = -600, IsDeposit = true, ParticipantPersonIdsInOrder = [_kevin.Id] },
                new ExpenseItemInput { Description = "Kauwgom", TotalCents = 150, ParticipantPersonIdsInOrder = [_kevin.Id] },
            ],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = -450 }],
        }));

        Assert.Contains("negatief", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_PlainNegativeTotalPurchase_IsStillRejected()
    {
        var ex = await Assert.ThrowsAsync<ExpenseValidationException>(() => _expenseService.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Typefout",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = -1000,
            Items = [new ExpenseItemInput { Description = "Boodschappen", TotalCents = -1000, ParticipantPersonIdsInOrder = [_kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = -1000 }],
        }));

        Assert.Contains("negatief", ex.Message);
    }
}
