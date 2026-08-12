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

    [Fact]
    public async Task CreateAsync_WithReceiptDocument_NotifiesEveryoneExceptTheUploader()
    {
        Guid documentId;
        await using (var uow = await _db.UnitOfWorkFactory.CreateAsync())
        {
            documentId = Guid.NewGuid();
            await uow.ReceiptDocuments.AddAsync(new ReceiptDocument
            {
                Id = documentId,
                OriginalFileName = "bon.jpg",
                StoredFileName = "bon-stored.jpg",
                MimeType = "image/jpeg",
                FileHash = "hash",
                UploadedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
            await uow.SaveChangesAsync();
        }

        var expenseId = await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Lidl",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 600,
            ReceiptDocumentId = documentId,
            CreatedByPersonId = _kevin.Id,
            Items = [new ExpenseItemInput { Description = "Boodschappen", TotalCents = 600, ParticipantPersonIdsInOrder = [_kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 600 }],
        });

        await using var verifyUow = await _db.UnitOfWorkFactory.CreateAsync();
        var kevinNotifications = await verifyUow.Notifications.GetForPersonAsync(_kevin.Id, 10);
        var wesleyNotifications = await verifyUow.Notifications.GetForPersonAsync(_wesley.Id, 10);
        var josNotifications = await verifyUow.Notifications.GetForPersonAsync(_jos.Id, 10);

        Assert.Empty(kevinNotifications);

        var wesleyNotification = Assert.Single(wesleyNotifications);
        Assert.Equal(expenseId, wesleyNotification.ExpenseId);
        Assert.Contains("Kevin", wesleyNotification.Message);
        Assert.Contains("Lidl", wesleyNotification.Message);
        Assert.False(wesleyNotification.IsRead);

        Assert.Single(josNotifications);
        Assert.Equal(1, await verifyUow.Notifications.GetUnreadCountAsync(_wesley.Id));

        await verifyUow.Notifications.MarkAllReadAsync(_wesley.Id);
        Assert.Equal(0, await verifyUow.Notifications.GetUnreadCountAsync(_wesley.Id));
    }

    [Fact]
    public async Task CreateManualExpenseAsync_DoesNotNotifyAnyone()
    {
        await _service.CreateManualExpenseAsync(new ManualExpenseRequest
        {
            Description = "Handmatig",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            AmountCents = 400,
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 400 }],
            ParticipantPersonIdsInOrder = [_kevin.Id, _wesley.Id],
        });

        await using var uow = await _db.UnitOfWorkFactory.CreateAsync();
        Assert.Empty(await uow.Notifications.GetForPersonAsync(_wesley.Id, 10));
        Assert.Empty(await uow.Notifications.GetForPersonAsync(_jos.Id, 10));
    }
}
