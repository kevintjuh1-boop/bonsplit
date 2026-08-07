using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrivateExpenses.Infrastructure;

namespace PrivateExpenses.IntegrationTests;

public class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_AnthropicProviderWithoutApiKey_ThrowsClearConfigurationError()
    {
        // The app must fail fast and loudly at startup if an operator selects the Anthropic provider
        // but forgets the API key — never silently fall back to a mock parser that pretends to work
        // (section 60/61).
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                ["ReceiptParsing:Provider"] = "Anthropic",
            })
            .Build();

        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        Assert.Contains("AnthropicApiKey", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_DevelopmentProvider_DoesNotRequireApiKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                ["ReceiptParsing:Provider"] = "Development",
            })
            .Build();

        var services = new ServiceCollection();

        var exception = Record.Exception(() => services.AddInfrastructure(configuration));

        Assert.Null(exception);
    }
}
