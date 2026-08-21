using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Domain.Entities;
using PrivateExpenses.Domain.Money;

namespace PrivateExpenses.Application.Services;

public class SettlementService(IUnitOfWorkFactory unitOfWorkFactory) : ISettlementService
{
    public async Task<List<Settlement>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Settlements.GetAllAsync(cancellationToken);
    }

    public async Task<List<Settlement>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Settlements.GetRecentAsync(count, cancellationToken);
    }

    public async Task<Guid> CreateAsync(
        Guid fromPersonId, Guid toPersonId, long amountCents, DateOnly date, string? note, CancellationToken cancellationToken = default)
    {
        if (amountCents <= 0)
        {
            throw new ExpenseValidationException("Het bedrag van een betaling moet positief zijn.");
        }

        if (fromPersonId == toPersonId)
        {
            throw new ExpenseValidationException("Je kunt geen betaling aan jezelf registreren.");
        }

        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var fromPerson = await uow.Persons.GetByIdAsync(fromPersonId, cancellationToken)
            ?? throw new ExpenseValidationException("De betaler bestaat niet (meer).");

        var toPerson = await uow.Persons.GetByIdAsync(toPersonId, cancellationToken)
            ?? throw new ExpenseValidationException("De ontvanger bestaat niet (meer).");

        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            FromPersonId = fromPersonId,
            ToPersonId = toPersonId,
            AmountCents = amountCents,
            SettlementDate = date,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        await uow.Settlements.AddAsync(settlement, cancellationToken);

        var now = DateTime.UtcNow;
        var formattedAmount = MoneyFormatter.Format(amountCents);
        await uow.Notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(),
            Message = $"{fromPerson.Name} heeft {formattedAmount} aan jou betaald.",
            RecipientPersonId = toPersonId,
            ActorPersonId = fromPersonId,
            CreatedAt = now,
        }, cancellationToken);
        await uow.Notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(),
            Message = $"Je hebt {formattedAmount} aan {toPerson.Name} betaald.",
            RecipientPersonId = fromPersonId,
            ActorPersonId = fromPersonId,
            CreatedAt = now,
        }, cancellationToken);

        await uow.SaveChangesAsync(cancellationToken);

        return settlement.Id;
    }

    public async Task DeleteAsync(Guid settlementId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var settlement = await uow.Settlements.GetByIdAsync(settlementId, cancellationToken)
            ?? throw new ExpenseValidationException("De betaling die je probeert te verwijderen bestaat niet (meer).");

        await uow.Settlements.DeleteAsync(settlement, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
