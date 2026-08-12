using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Domain.Calculations;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Services;

public class ExpenseService(IUnitOfWorkFactory unitOfWorkFactory) : IExpenseService
{
    public async Task<ExpenseDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var expense = await uow.Expenses.GetByIdWithDetailsAsync(id, cancellationToken);
        return expense is null ? null : MapToDetail(expense);
    }

    public async Task<List<ExpenseListItemDto>> GetListAsync(ExpenseFilter filter, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Expenses.GetListAsync(filter, cancellationToken);
    }

    public async Task<List<ExpenseListItemDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Expenses.GetRecentAsync(count, cancellationToken);
    }

    public async Task<long> GetMonthTotalCentsAsync(DateOnly monthStart, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Expenses.GetTotalCentsThisMonthAsync(monthStart, monthStart.AddMonths(1), cancellationToken);
    }

    public async Task<Guid> CreateManualExpenseAsync(ManualExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var item = new ExpenseItemInput
        {
            Description = request.Description,
            Quantity = 1m,
            TotalCents = request.AmountCents,
            ParticipantPersonIdsInOrder = request.ParticipantPersonIdsInOrder,
            CustomShareCents = request.CustomShareCents,
        };

        var createRequest = new CreateExpenseRequest
        {
            MerchantName = string.IsNullOrWhiteSpace(request.MerchantName) ? request.Description : request.MerchantName,
            ExpenseDate = request.ExpenseDate,
            TotalCents = request.AmountCents,
            CategoryId = request.CategoryId,
            Notes = request.Notes,
            Items = [item],
            Payments = request.Payments,
        };

        return await CreateAsync(createRequest, cancellationToken);
    }

    public async Task<Guid> CreateAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var persons = await uow.Persons.GetAllAsync(includeInactive: true, cancellationToken);
        var knownPersonIds = persons.Select(p => p.Id).ToHashSet();

        var now = DateTime.UtcNow;
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            MerchantName = request.MerchantName.Trim(),
            ExpenseDate = request.ExpenseDate,
            TotalCents = request.TotalCents,
            CategoryId = request.CategoryId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var item in BuildItemEntities(request.Items, knownPersonIds))
        {
            expense.Items.Add(item);
        }

        foreach (var payment in BuildPaymentEntities(request.Payments, request.TotalCents, knownPersonIds))
        {
            expense.Payments.Add(payment);
        }

        await uow.ExecuteInTransactionAsync(async () =>
        {
            await uow.Expenses.AddAsync(expense, cancellationToken);

            if (request.ReceiptDocumentId is { } documentId)
            {
                var document = await uow.ReceiptDocuments.GetByIdAsync(documentId, cancellationToken)
                    ?? throw new ExpenseValidationException("Het gekoppelde bondocument kon niet worden gevonden.");
                document.ExpenseId = expense.Id;
                document.ParsingStatus = Domain.Enums.ParsingStatus.Confirmed;
                uow.ReceiptDocuments.Update(document);

                var actorName = persons.FirstOrDefault(p => p.Id == request.CreatedByPersonId)?.Name ?? "Iemand";
                var recipientIds = persons.Select(p => p.Id).Where(id => id != request.CreatedByPersonId);
                foreach (var recipientId in recipientIds)
                {
                    await uow.Notifications.AddAsync(new Notification
                    {
                        Id = Guid.NewGuid(),
                        Message = $"{actorName} heeft een bon toegevoegd: {expense.MerchantName}",
                        ExpenseId = expense.Id,
                        RecipientPersonId = recipientId,
                        ActorPersonId = request.CreatedByPersonId,
                        CreatedAt = now,
                    }, cancellationToken);
                }
            }
        }, cancellationToken);

        return expense.Id;
    }

    public async Task UpdateAsync(Guid expenseId, CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var expense = await uow.Expenses.GetByIdWithDetailsAsync(expenseId, cancellationToken)
            ?? throw new ExpenseValidationException("De uitgave die je probeert te wijzigen bestaat niet (meer).");

        var knownPersonIds = (await uow.Persons.GetAllAsync(includeInactive: true, cancellationToken))
            .Select(p => p.Id)
            .ToHashSet();

        expense.MerchantName = request.MerchantName.Trim();
        expense.ExpenseDate = request.ExpenseDate;
        expense.TotalCents = request.TotalCents;
        expense.CategoryId = request.CategoryId;
        expense.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        expense.UpdatedAt = DateTime.UtcNow;

        // Clearing these tracked collections deletes the orphaned rows on save (both FKs are
        // required/non-nullable), so old shares never linger after a re-assignment.
        expense.Items.Clear();
        expense.Payments.Clear();

        foreach (var item in BuildItemEntities(request.Items, knownPersonIds))
        {
            expense.Items.Add(item);
        }

        foreach (var payment in BuildPaymentEntities(request.Payments, request.TotalCents, knownPersonIds))
        {
            expense.Payments.Add(payment);
        }

        await uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var expense = await uow.Expenses.GetByIdWithDetailsAsync(expenseId, cancellationToken)
            ?? throw new ExpenseValidationException("De uitgave die je probeert te verwijderen bestaat niet (meer).");

        expense.IsDeleted = true;
        expense.DeletedAt = DateTime.UtcNow;
        expense.UpdatedAt = expense.DeletedAt.Value;

        uow.Expenses.Update(expense);
        await uow.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRequest(CreateExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MerchantName))
        {
            throw new ExpenseValidationException("Vul een winkel/omschrijving in.");
        }

        if (request.TotalCents < 0)
        {
            throw new ExpenseValidationException("Het totaalbedrag mag niet negatief zijn.");
        }

        if (request.Items.Count == 0)
        {
            throw new ExpenseValidationException("Een uitgave moet minstens één regel bevatten.");
        }

        if (request.Payments.Count == 0)
        {
            throw new ExpenseValidationException("Kies wie deze uitgave heeft betaald.");
        }

        var paidTotal = request.Payments.Sum(p => p.AmountCents);
        if (paidTotal != request.TotalCents)
        {
            throw new ExpenseValidationException(
                $"De betalingen ({paidTotal} cent) komen niet exact overeen met het totaalbedrag ({request.TotalCents} cent).");
        }

        foreach (var payment in request.Payments)
        {
            if (payment.AmountCents <= 0)
            {
                throw new ExpenseValidationException("Een betaling moet een positief bedrag zijn.");
            }
        }
    }

    private static List<ExpenseItem> BuildItemEntities(IReadOnlyList<ExpenseItemInput> itemInputs, HashSet<Guid> knownPersonIds)
    {
        var items = new List<ExpenseItem>();
        var sortOrder = 0;

        foreach (var input in itemInputs)
        {
            if (string.IsNullOrWhiteSpace(input.Description))
            {
                throw new ExpenseValidationException("Elke regel moet een omschrijving hebben.");
            }

            IReadOnlyDictionary<Guid, long> shares;
            if (input.CustomShareCents is { Count: > 0 } custom)
            {
                MoneySplitter.ValidateExactSplit(input.TotalCents, custom);
                shares = custom;
            }
            else
            {
                if (input.ParticipantPersonIdsInOrder.Count == 0)
                {
                    throw new ExpenseValidationException($"Regel '{input.Description}' is nog niet toegewezen aan iemand.");
                }

                shares = MoneySplitter.SplitEqually(input.TotalCents, input.ParticipantPersonIdsInOrder);
            }

            foreach (var personId in shares.Keys)
            {
                if (!knownPersonIds.Contains(personId))
                {
                    throw new ExpenseValidationException("Eén van de toegewezen personen bestaat niet (meer).");
                }
            }

            var item = new ExpenseItem
            {
                Id = Guid.NewGuid(),
                Description = input.Description.Trim(),
                Quantity = input.Quantity,
                UnitPriceCents = input.UnitPriceCents,
                TotalCents = input.TotalCents,
                SortOrder = sortOrder++,
                IsDiscount = input.IsDiscount,
                IsDeposit = input.IsDeposit,
                CreatedAt = DateTime.UtcNow,
            };

            foreach (var (personId, amount) in shares)
            {
                item.Shares.Add(new ExpenseItemShare { Id = Guid.NewGuid(), PersonId = personId, AmountCents = amount });
            }

            items.Add(item);
        }

        return items;
    }

    private static List<ExpensePayment> BuildPaymentEntities(
        IReadOnlyList<ExpensePaymentInput> paymentInputs, long expectedTotalCents, HashSet<Guid> knownPersonIds)
    {
        foreach (var payment in paymentInputs)
        {
            if (!knownPersonIds.Contains(payment.PersonId))
            {
                throw new ExpenseValidationException("De geselecteerde betaler bestaat niet (meer).");
            }
        }

        return paymentInputs
            .Select(p => new ExpensePayment { Id = Guid.NewGuid(), PersonId = p.PersonId, AmountCents = p.AmountCents })
            .ToList();
    }

    private static ExpenseDetailDto MapToDetail(Expense expense)
    {
        var items = expense.Items
            .OrderBy(i => i.SortOrder)
            .Select(i => new ExpenseItemDto(
                i.Id, i.Description, i.Quantity, i.UnitPriceCents, i.TotalCents, i.IsDiscount, i.IsDeposit, i.SortOrder,
                i.Shares.Select(s => new ExpenseItemShareDto(s.PersonId, s.Person!.Name, s.Person.Initial, s.Person.ColorKey, s.AmountCents)).ToList()))
            .ToList();

        var payments = expense.Payments
            .Select(p => new ExpensePaymentDto(p.PersonId, p.Person!.Name, p.Person.Initial, p.Person.ColorKey, p.AmountCents))
            .ToList();

        var personTotals = expense.Items
            .SelectMany(i => i.Shares)
            .GroupBy(s => new { s.PersonId, s.Person!.Name, s.Person.Initial, s.Person.ColorKey })
            .Select(g => new { g.Key.PersonId, g.Key.Name, g.Key.Initial, g.Key.ColorKey, OwedCents = g.Sum(s => s.AmountCents) })
            .ToDictionary(x => x.PersonId);

        var paidPerPerson = expense.Payments
            .GroupBy(p => new { p.PersonId, p.Person!.Name, p.Person.Initial, p.Person.ColorKey })
            .Select(g => new { g.Key.PersonId, g.Key.Name, g.Key.Initial, g.Key.ColorKey, PaidCents = g.Sum(p => p.AmountCents) })
            .ToDictionary(x => x.PersonId);

        var allPersonIds = personTotals.Keys.Union(paidPerPerson.Keys);
        var personTotalDtos = allPersonIds.Select(personId =>
        {
            var owed = personTotals.GetValueOrDefault(personId)?.OwedCents ?? 0;
            var paid = paidPerPerson.GetValueOrDefault(personId)?.PaidCents ?? 0;
            var name = personTotals.GetValueOrDefault(personId)?.Name ?? paidPerPerson[personId].Name;
            var initial = personTotals.GetValueOrDefault(personId)?.Initial ?? paidPerPerson[personId].Initial;
            var colorKey = personTotals.GetValueOrDefault(personId)?.ColorKey ?? paidPerPerson[personId].ColorKey;
            return new PersonNetDto(personId, name, initial, colorKey, paid, owed, paid - owed);
        })
        .OrderBy(p => p.PersonName)
        .ToList();

        return new ExpenseDetailDto(
            expense.Id,
            expense.MerchantName,
            expense.ExpenseDate,
            expense.TotalCents,
            expense.CategoryId,
            expense.Category?.Name,
            expense.Category?.IconKey,
            expense.Notes,
            expense.IsDeleted,
            items,
            payments,
            personTotalDtos,
            expense.Documents.Select(d => d.Id).ToList());
    }
}
