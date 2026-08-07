using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Services;

public class PersonService(IUnitOfWorkFactory unitOfWorkFactory) : IPersonService
{
    public async Task<List<Person>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Persons.GetAllAsync(includeInactive, cancellationToken);
    }

    public async Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Persons.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Guid> CreateAsync(string name, string colorKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ExpenseValidationException("Vul een naam in.");
        }

        var person = new Person
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Initial = name.Trim()[..1].ToUpperInvariant(),
            ColorKey = colorKey,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        await uow.Persons.AddAsync(person, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return person.Id;
    }

    public async Task UpdateAsync(Guid id, string name, string colorKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ExpenseValidationException("Vul een naam in.");
        }

        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var person = await uow.Persons.GetByIdAsync(id, cancellationToken)
            ?? throw new ExpenseValidationException("Deze persoon bestaat niet (meer).");

        person.Name = name.Trim();
        person.Initial = name.Trim()[..1].ToUpperInvariant();
        person.ColorKey = colorKey;

        await uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var person = await uow.Persons.GetByIdAsync(id, cancellationToken)
            ?? throw new ExpenseValidationException("Deze persoon bestaat niet (meer).");

        // Soft-deactivation only: historical expenses, shares and payments referencing this person
        // must stay intact and keep showing correctly (section 4).
        person.IsActive = isActive;
        await uow.SaveChangesAsync(cancellationToken);
    }
}
