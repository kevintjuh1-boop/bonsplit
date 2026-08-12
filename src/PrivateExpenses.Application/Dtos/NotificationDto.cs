namespace PrivateExpenses.Application.Dtos;

public sealed record NotificationDto(Guid Id, string Message, Guid? ExpenseId, bool IsRead, DateTime CreatedAt);
