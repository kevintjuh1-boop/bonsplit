using Microsoft.Extensions.DependencyInjection;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Services;

namespace PrivateExpenses.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPersonService, PersonService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<ISettlementService, SettlementService>();
        services.AddScoped<IReceiptImportService, ReceiptImportService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
