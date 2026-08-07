using Anthropic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrivateExpenses.Application.Abstractions.Parsing;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Storage;
using PrivateExpenses.Infrastructure.Parsing;
using PrivateExpenses.Infrastructure.Persistence;
using PrivateExpenses.Infrastructure.Storage;

namespace PrivateExpenses.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is niet geconfigureerd.");

        services.AddDbContextFactory<PrivateExpensesDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IUnitOfWorkFactory, EfUnitOfWorkFactory>();

        services.AddScoped<IReceiptStorage, LocalReceiptStorage>();
        AddReceiptParser(services, configuration);

        return services;
    }

    private static void AddReceiptParser(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["ReceiptParsing:Provider"] ?? "Development";

        switch (provider)
        {
            case "Fixture":
                services.AddScoped<IReceiptParser, FixtureReceiptParser>();
                break;
            case "Anthropic":
                AddAnthropicReceiptParser(services, configuration);
                break;
            case "Development":
            default:
                // Any unrecognized provider name also falls back here rather than silently doing
                // nothing — receipt parsing must never look like it works when it doesn't (section 60).
                services.AddScoped<IReceiptParser, DevelopmentReceiptParser>();
                break;
        }
    }

    private static void AddAnthropicReceiptParser(IServiceCollection services, IConfiguration configuration)
    {
        // The API key is never hardcoded or committed — only read from configuration, which in turn
        // only ever comes from an environment variable or `dotnet user-secrets` (section 61/94).
        var apiKey = configuration["ReceiptParsing:AnthropicApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "ReceiptParsing:Provider is 'Anthropic' but ReceiptParsing:AnthropicApiKey is not configured. " +
                "Set it via 'dotnet user-secrets set \"ReceiptParsing:AnthropicApiKey\" \"...\"' (development) " +
                "or the ReceiptParsing__AnthropicApiKey environment variable (production). See README.md.");
        }

        var model = configuration["ReceiptParsing:Model"];
        var modelId = string.IsNullOrWhiteSpace(model) ? "claude-opus-5" : model;

        // One client per process — the SDK owns its own HttpClient internally, so this avoids the
        // "new HttpClient per request" anti-pattern (section 82).
        services.AddSingleton(new AnthropicClient { ApiKey = apiKey });
        services.AddScoped<IReceiptParser>(sp => new AnthropicVisionReceiptParser(
            sp.GetRequiredService<AnthropicClient>(),
            modelId,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AnthropicVisionReceiptParser>>()));
    }
}
