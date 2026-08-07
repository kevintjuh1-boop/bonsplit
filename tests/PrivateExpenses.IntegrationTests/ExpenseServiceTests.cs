using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Application.Services;
using PrivateExpenses.Domain.Entities;
using PrivateExpenses.IntegrationTests.TestSupport;

namespace PrivateExpenses.IntegrationTests;

public class ExpenseServiceTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;
    private ExpenseService _service = null!;
    private Person _kevin = null!;
    private Person _wesley = null!;
    private Person _jos = null!;

    public async Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        _service = new ExpenseService(_db.UnitOfWorkFactory);
        var people = await _db.GetPeopleAsync();
        _kevin = people.Single(p => p.Name == "Kevin");
        _wesley = people.Single(p => p.Name == "Wesley");
        _jos = people.Single(p => p.Name == "Jos");
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task CreateAsync_SavesExpense_CanBeRetrievedWithFullItemShareAndPaymentGraph()
    {
        var request = new CreateExpenseRequest
        {
            MerchantName = "Jumbo",
            ExpenseDate = new DateOnly(2026, 1, 15),
            TotalCents = 1000,
            Items =
            [
                new ExpenseItemInput
                {
                    Description = "Boodschappen",
                    Quantity = 1m,
                    TotalCents = 1000,
                    ParticipantPersonIdsInOrder = [_kevin.Id, _wesley.Id, _jos.Id],
                },
            ],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 1000 }],
        };

        var expenseId = await _service.CreateAsync(request);
        var detail = await _service.GetDetailAsync(expenseId);

        Assert.NotNull(detail);
        Assert.Equal("Jumbo", detail.MerchantName);
        Assert.Equal(1000, detail.TotalCents);

        var item = Assert.Single(detail.Items);
        Assert.Equal("Boodschappen", item.Description);
        Assert.Equal(3, item.Shares.Count);

        // Acceptance scenario: equal 3-way split of €10 apportions the leftover cent deterministically
        // (334/333/333), never as a fraction and never dropped.
        var sharesByPerson = item.Shares.ToDictionary(s => s.PersonId, s => s.AmountCents);
        Assert.Equal(1000, sharesByPerson.Values.Sum());
        Assert.Contains(334L, sharesByPerson.Values);
        Assert.Equal(2, sharesByPerson.Values.Count(v => v == 333L));

        var payment = Assert.Single(detail.Payments);
        Assert.Equal(_kevin.Id, payment.PersonId);
        Assert.Equal(1000, payment.AmountCents);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentReceiptDocumentId_ThrowsAndRollsBackWithoutSavingTheExpense()
    {
        // The receipt-document lookup happens inside the same ExecuteInTransactionAsync block as
        // AddAsync(expense) — if it throws, the whole transaction (including the expense row itself)
        // must roll back, not just skip the document link.
        var request = new CreateExpenseRequest
        {
            MerchantName = "Onbestaand bondocument",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 500,
            ReceiptDocumentId = Guid.NewGuid(),
            Items = [new ExpenseItemInput { Description = "Item", TotalCents = 500, ParticipantPersonIdsInOrder = [_kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 500 }],
        };

        await Assert.ThrowsAsync<ExpenseValidationException>(() => _service.CreateAsync(request));

        var allExpenses = await _service.GetListAsync(new ExpenseFilter());
        Assert.DoesNotContain(allExpenses, e => e.MerchantName == "Onbestaand bondocument");
    }

    [Fact]
    public async Task UpdateAsync_ReassignsSharesAndPayments_OldSharesDoNotLinger()
    {
        var expenseId = await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Albert Heijn",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 900,
            Items = [new ExpenseItemInput { Description = "Boodschappen", TotalCents = 900, ParticipantPersonIdsInOrder = [_kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 900 }],
        });

        await _service.UpdateAsync(expenseId, new CreateExpenseRequest
        {
            MerchantName = "Albert Heijn",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 900,
            Items = [new ExpenseItemInput { Description = "Boodschappen", TotalCents = 900, ParticipantPersonIdsInOrder = [_wesley.Id, _jos.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _wesley.Id, AmountCents = 900 }],
        });

        var detail = await _service.GetDetailAsync(expenseId);
        Assert.NotNull(detail);
        var item = Assert.Single(detail.Items);
        Assert.Equal(2, item.Shares.Count);
        Assert.DoesNotContain(item.Shares, s => s.PersonId == _kevin.Id);

        var payment = Assert.Single(detail.Payments);
        Assert.Equal(_wesley.Id, payment.PersonId);
    }

    [Fact]
    public async Task SoftDeleteAsync_ExpenseIsExcludedFromListAndTotals_ButStillLoadableById()
    {
        var expenseId = await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Te verwijderen",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 250,
            Items = [new ExpenseItemInput { Description = "Item", TotalCents = 250, ParticipantPersonIdsInOrder = [_kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 250 }],
        });

        await _service.SoftDeleteAsync(expenseId);

        var list = await _service.GetListAsync(new ExpenseFilter());
        Assert.DoesNotContain(list, e => e.Id == expenseId);

        var detail = await _service.GetDetailAsync(expenseId);
        Assert.NotNull(detail);
        Assert.True(detail.IsDeleted);
    }
}
