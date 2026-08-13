using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Abstractions.Storage;
using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Application.Validation;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Services;

public class PersonService(IUnitOfWorkFactory unitOfWorkFactory, IPersonAvatarStorage avatarStorage) : IPersonService
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

    public async Task UpdateAvatarAsync(
        Guid id, Stream content, string originalFileName, string mimeType, long fileSize, CancellationToken cancellationToken = default)
    {
        // Mirrors ReceiptImportService.UploadAsync: the incoming stream isn't seekable, but validation
        // needs to peek at the header and storage needs to read the whole thing — buffer once into a
        // seekable copy.
        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        var header = new byte[16];
        var headerLength = await ReadHeaderAsync(buffered, header, cancellationToken);
        buffered.Position = 0;

        var validation = PersonAvatarFileValidator.Validate(originalFileName, mimeType, fileSize, header.AsSpan(0, headerLength));
        if (!validation.IsValid)
        {
            throw new ExpenseValidationException(validation.ErrorMessage!);
        }

        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var person = await uow.Persons.GetByIdAsync(id, cancellationToken)
            ?? throw new ExpenseValidationException("Deze persoon bestaat niet (meer).");

        var previousStoredFileName = person.AvatarStoredFileName;

        var stored = await avatarStorage.SaveAsync(buffered, originalFileName, cancellationToken);
        person.AvatarStoredFileName = stored.StoredFileName;
        person.AvatarMimeType = mimeType;
        await uow.SaveChangesAsync(cancellationToken);

        if (previousStoredFileName is not null)
        {
            await avatarStorage.DeleteAsync(previousStoredFileName, cancellationToken);
        }
    }

    public async Task RemoveAvatarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var person = await uow.Persons.GetByIdAsync(id, cancellationToken)
            ?? throw new ExpenseValidationException("Deze persoon bestaat niet (meer).");

        var storedFileName = person.AvatarStoredFileName;
        if (storedFileName is null)
        {
            return;
        }

        person.AvatarStoredFileName = null;
        person.AvatarMimeType = null;
        await uow.SaveChangesAsync(cancellationToken);

        await avatarStorage.DeleteAsync(storedFileName, cancellationToken);
    }

    public async Task<PersonAvatarFileContent?> OpenAvatarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var person = await uow.Persons.GetByIdAsync(id, cancellationToken);
        if (person?.AvatarStoredFileName is not { } storedFileName)
        {
            return null;
        }

        var stream = await avatarStorage.OpenAsync(storedFileName, cancellationToken);
        return new PersonAvatarFileContent(stream, person.AvatarMimeType ?? "application/octet-stream");
    }

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
