using PrivateExpenses.Domain.Exceptions;

namespace PrivateExpenses.Application.Exceptions;

/// <summary>Thrown when a request to create/update an expense, settlement, or person violates a
/// server-side business rule (section 47). The message is safe to show directly to the user.</summary>
public sealed class ExpenseValidationException : DomainException
{
    public ExpenseValidationException(string message) : base(message)
    {
    }
}
