using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Domain.Entities;

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

        if (await uow.Persons.GetByIdAsync(fromPersonId, cancellationToken) is null)
        {
            throw new ExpenseValidationException("De betaler bestaat niet (meer).");
        }

        if (await uow.Persons.GetByIdAsync(toPersonId, cancellationToken) is null)
        {
            throw new ExpenseValidationException("De ontvanger bestaat niet (meer).");
        }

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
        await uow.SaveChangesAsync(cancellationToken);

        return settlement.Id;
    }
}
