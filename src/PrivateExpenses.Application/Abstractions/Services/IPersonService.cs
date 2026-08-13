using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Services;

public interface IPersonService
{
    Task<List<Person>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(string name, string colorKey, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, string name, string colorKey, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task UpdateAvatarAsync(Guid id, Stream content, string originalFileName, string mimeType, long fileSize, CancellationToken cancellationToken = default);
    Task RemoveAvatarAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PersonAvatarFileContent?> OpenAvatarAsync(Guid id, CancellationToken cancellationToken = default);
}
