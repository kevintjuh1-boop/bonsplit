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
    public async Task CreateAsync_WithReceiptDocument_NotifiesEveryoneIncludingTheUploader()
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

        var kevinNotification = Assert.Single(kevinNotifications);
        Assert.Equal(expenseId, kevinNotification.ExpenseId);
        Assert.Contains("Je bon is opgeslagen", kevinNotification.Message);
        Assert.Contains("Lidl", kevinNotification.Message);

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
    public async Task GetMonthSavingsCentsAsync_SumsDiscountLinesAsAPositiveAmount_ExcludingOtherMonthsAndDeletedExpenses()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var inMonthExpenseId = await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Lidl",
            ExpenseDate = today,
            TotalCents = 500,
            Items =
            [
                new ExpenseItemInput { Description = "Kiwi", TotalCents = 600, ParticipantPersonIdsInOrder = [_kevin.Id] },
                new ExpenseItemInput { Description = "1+1 gratis", TotalCents = -100, IsDiscount = true, ParticipantPersonIdsInOrder = [_kevin.Id] },
            ],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 500 }],
        });

        // A discount from last month must not count toward this month's total.
        await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Lidl vorige maand",
            ExpenseDate = monthStart.AddDays(-1),
            TotalCents = 100,
            Items =
            [
                new ExpenseItemInput { Description = "Product", TotalCents = 150, ParticipantPersonIdsInOrder = [_kevin.Id] },
                new ExpenseItemInput { Description = "Korting", TotalCents = -50, IsDiscount = true, ParticipantPersonIdsInOrder = [_kevin.Id] },
            ],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 100 }],
        });

        Assert.Equal(100, await _service.GetMonthSavingsCentsAsync(monthStart));

        // A soft-deleted expense's discounts must not count either.
        await _service.SoftDeleteAsync(inMonthExpenseId);
        Assert.Equal(0, await _service.GetMonthSavingsCentsAsync(monthStart));
    }

    [Fact]
    public async Task UpdateCategoryAsync_ChangesCategoryEvenForAMultiItemMultiPayerExpense()
    {
        // The category picker on the expense detail page must work regardless of item/payer count —
        // unlike the full edit flow, which is gated to single-item, single-payment expenses only.
        var expenseId = await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Jumbo",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 1000,
            Items =
            [
                new ExpenseItemInput { Description = "Brood", TotalCents = 500, ParticipantPersonIdsInOrder = [_kevin.Id] },
                new ExpenseItemInput { Description = "Melk", TotalCents = 500, ParticipantPersonIdsInOrder = [_wesley.Id] },
            ],
            Payments =
            [
                new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 500 },
                new ExpensePaymentInput { PersonId = _wesley.Id, AmountCents = 500 },
            ],
        });

        await using var uow = await _db.UnitOfWorkFactory.CreateAsync();
        var category = (await uow.Categories.GetAllAsync()).First();

        await _service.UpdateCategoryAsync(expenseId, category.Id);

        var detail = await _service.GetDetailAsync(expenseId);
        Assert.Equal(category.Id, detail!.CategoryId);

        await _service.UpdateCategoryAsync(expenseId, null);
        detail = await _service.GetDetailAsync(expenseId);
        Assert.Null(detail!.CategoryId);
    }

    [Fact]
    public async Task CreateAsync_ItemWithExternalRecipient_GetsNoSharesAndIsExcludedFromTheGroupSplit()
    {
        var expenseId = await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Etentje met vrienden",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 3000,
            Items =
            [
                new ExpenseItemInput { Description = "Onze pizza's", TotalCents = 2000, ParticipantPersonIdsInOrder = [_kevin.Id, _wesley.Id] },
                new ExpenseItemInput { Description = "Pizza van Jan", TotalCents = 1000, ExternalRecipientName = "Jan" },
            ],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 3000 }],
        });

        var detail = await _service.GetDetailAsync(expenseId);
        Assert.NotNull(detail);

        var externalItem = detail.Items.Single(i => i.Description == "Pizza van Jan");
        Assert.Equal("Jan", externalItem.ExternalRecipientName);
        Assert.Empty(externalItem.Shares);

        var normalItem = detail.Items.Single(i => i.Description == "Onze pizza's");
        Assert.Null(normalItem.ExternalRecipientName);
        Assert.Equal(2, normalItem.Shares.Count);

        // Kevin fronted the whole €30, but only €20 was ever "the group's" — the other €10 is Jan's,
        // tracked separately, so it must not inflate what Wesley owes Kevin.
        var kevinNet = detail.PersonTotals.Single(p => p.PersonId == _kevin.Id);
        var wesleyNet = detail.PersonTotals.Single(p => p.PersonId == _wesley.Id);
        Assert.Equal(1000, kevinNet.OwedCents);
        Assert.Equal(1000, wesleyNet.OwedCents);
        Assert.Equal(3000, kevinNet.PaidCents);
    }

    [Fact]
    public async Task CreateAsync_ItemWithExternalRecipientAndParticipants_ThrowsValidationException()
    {
        var ex = await Assert.ThrowsAsync<ExpenseValidationException>(() => _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Verkeerd ingevuld",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 1000,
            Items = [new ExpenseItemInput { Description = "Item", TotalCents = 1000, ExternalRecipientName = "Jan", ParticipantPersonIdsInOrder = [_kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 1000 }],
        }));

        Assert.Contains("extern", ex.Message);
    }

    [Fact]
    public async Task GetExternalSharesAsync_GroupsAcrossExpenses_AndTracksWhoFrontedEach()
    {
        await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Bon 1",
            ExpenseDate = new DateOnly(2026, 1, 1),
            TotalCents = 1000,
            Items = [new ExpenseItemInput { Description = "Voor Jan", TotalCents = 1000, ExternalRecipientName = "Jan" }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 1000 }],
        });
        await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Bon 2",
            ExpenseDate = new DateOnly(2026, 1, 2),
            TotalCents = 500,
            Items = [new ExpenseItemInput { Description = "Ook voor Jan, door Wesley", TotalCents = 500, ExternalRecipientName = "Jan" }],
            Payments = [new ExpensePaymentInput { PersonId = _wesley.Id, AmountCents = 500 }],
        });

        var shares = await _service.GetExternalSharesAsync();
        Assert.Equal(2, shares.Count);
        Assert.All(shares, s => Assert.Equal("Jan", s.RecipientName));

        var kevinsShare = shares.Single(s => s.ItemDescription == "Voor Jan");
        Assert.Equal(_kevin.Id, kevinsShare.OwedToPersonId);
        Assert.Equal("Kevin", kevinsShare.OwedToPersonName);

        var wesleysShare = shares.Single(s => s.ItemDescription == "Ook voor Jan, door Wesley");
        Assert.Equal(_wesley.Id, wesleysShare.OwedToPersonId);
    }

    [Fact]
    public async Task RegisterExternalPaymentAsync_ReducesWhatIsOpen_AndDeleteExternalPaymentAsyncUndoesIt()
    {
        await _service.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Bon",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 1000,
            Items = [new ExpenseItemInput { Description = "Voor Jan", TotalCents = 1000, ExternalRecipientName = "Jan" }],
            Payments = [new ExpensePaymentInput { PersonId = _kevin.Id, AmountCents = 1000 }],
        });

        var paymentId = await _service.RegisterExternalPaymentAsync("Jan", _kevin.Id, 400, DateOnly.FromDateTime(DateTime.Today), note: "eerste deel");

        var payments = await _service.GetExternalPaymentsAsync();
        var payment = Assert.Single(payments);
        Assert.Equal("Jan", payment.RecipientName);
        Assert.Equal(_kevin.Id, payment.OwedToPersonId);
        Assert.Equal(400, payment.AmountCents);
        Assert.Equal("eerste deel", payment.Note);

        await _service.DeleteExternalPaymentAsync(paymentId);
        Assert.Empty(await _service.GetExternalPaymentsAsync());
    }

    [Fact]
    public async Task RegisterExternalPaymentAsync_NonPositiveAmount_Throws()
    {
        var ex = await Assert.ThrowsAsync<ExpenseValidationException>(() =>
            _service.RegisterExternalPaymentAsync("Jan", _kevin.Id, 0, DateOnly.FromDateTime(DateTime.Today), note: null));

        Assert.Contains("positief", ex.Message);
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
