using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Domain.Calculations;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Services;

public class BalanceService(IUnitOfWorkFactory unitOfWorkFactory) : IBalanceService
{
    public async Task<List<PersonBalanceDto>> GetPersonBalancesAsync(CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var people = await uow.Persons.GetAllAsync(includeInactive: true, cancellationToken);
        var (paid, owed, settlementNet) = await LoadRawTotalsAsync(uow, cancellationToken);

        return people
            .Select(p => BuildBalance(p, paid, owed, settlementNet))
            .Where(b => b.TotalPaidCents != 0 || b.TotalOwedCents != 0 || b.SettlementNetCents != 0 || IsActive(people, b.PersonId))
            .OrderBy(b => b.Name)
            .ToList();
    }

    public async Task<List<SuggestedDebtDto>> GetSuggestedDebtsAsync(CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var people = await uow.Persons.GetAllAsync(includeInactive: true, cancellationToken);
        var (paid, owed, settlementNet) = await LoadRawTotalsAsync(uow, cancellationToken);

        var netBalances = people.ToDictionary(
            p => p.Id,
            p => paid.GetValueOrDefault(p.Id) - owed.GetValueOrDefault(p.Id) + settlementNet.GetValueOrDefault(p.Id));

        var peopleById = people.ToDictionary(p => p.Id);
        return DebtSimplifier.Simplify(netBalances)
            .Select(d => ToDto(d, peopleById))
            .ToList();
    }

    public async Task<PersonBalanceDto?> GetPersonBalanceAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var person = await uow.Persons.GetByIdAsync(personId, cancellationToken);
        if (person is null)
        {
            return null;
        }

        var (paid, owed, settlementNet) = await LoadRawTotalsAsync(uow, cancellationToken);
        return BuildBalance(person, paid, owed, settlementNet);
    }

    private static async Task<(Dictionary<Guid, long> Paid, Dictionary<Guid, long> Owed, Dictionary<Guid, long> SettlementNet)> LoadRawTotalsAsync(
        IUnitOfWork uow, CancellationToken cancellationToken)
    {
        var paid = await uow.Expenses.GetTotalPaidPerPersonAsync(cancellationToken);
        var owed = await uow.Expenses.GetTotalOwedPerPersonAsync(cancellationToken);
        var settlements = await uow.Settlements.GetAllAsync(cancellationToken);

        var settlementNet = new Dictionary<Guid, long>();
        foreach (var settlement in settlements)
        {
            // The payer's debt shrinks (balance goes up); the receiver is owed less (balance goes down).
            settlementNet[settlement.FromPersonId] = settlementNet.GetValueOrDefault(settlement.FromPersonId) + settlement.AmountCents;
            settlementNet[settlement.ToPersonId] = settlementNet.GetValueOrDefault(settlement.ToPersonId) - settlement.AmountCents;
        }

        return (paid, owed, settlementNet);
    }

    private static PersonBalanceDto BuildBalance(
        Person person, Dictionary<Guid, long> paid, Dictionary<Guid, long> owed, Dictionary<Guid, long> settlementNet)
    {
        var paidCents = paid.GetValueOrDefault(person.Id);
        var owedCents = owed.GetValueOrDefault(person.Id);
        var settlementCents = settlementNet.GetValueOrDefault(person.Id);
        var net = paidCents - owedCents + settlementCents;

        return new PersonBalanceDto(person.Id, person.Name, person.Initial, person.ColorKey, paidCents, owedCents, settlementCents, net);
    }

    private static bool IsActive(List<Person> people, Guid personId) => people.First(p => p.Id == personId).IsActive;

    private static SuggestedDebtDto ToDto(SuggestedDebt debt, Dictionary<Guid, Person> peopleById)
    {
        var from = peopleById[debt.FromPersonId];
        var to = peopleById[debt.ToPersonId];
        return new SuggestedDebtDto(
            from.Id, from.Name, from.Initial, from.ColorKey,
            to.Id, to.Name, to.Initial, to.ColorKey,
            debt.AmountCents);
    }
}
