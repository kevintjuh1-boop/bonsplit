namespace PrivateExpenses.Domain.Exceptions;

/// <summary>Base type for violations of a business rule. Caught at the application boundary and
/// translated into a user-friendly message; never exposes internals to the end user.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

public sealed class MoneySplitException : DomainException
{
    public MoneySplitException(string message) : base(message)
    {
    }
}

public sealed class InvalidSettlementException : DomainException
{
    public InvalidSettlementException(string message) : base(message)
    {
    }
}
