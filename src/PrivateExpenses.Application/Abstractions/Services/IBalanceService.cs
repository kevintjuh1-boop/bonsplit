using PrivateExpenses.Application.Dtos;

namespace PrivateExpenses.Application.Abstractions.Services;

/// <summary>
/// The single source of truth for "who owes whom". Every page that shows a balance (dashboard, saldi
/// page, person detail) goes through this service instead of recomputing the paid/owed/settled math
/// itself (section 31/78).
/// </summary>
public interface IBalanceService
{
    Task<List<PersonBalanceDto>> GetPersonBalancesAsync(CancellationToken cancellationToken = default);
    Task<List<SuggestedDebtDto>> GetSuggestedDebtsAsync(CancellationToken cancellationToken = default);
    Task<PersonBalanceDto?> GetPersonBalanceAsync(Guid personId, CancellationToken cancellationToken = default);
}
