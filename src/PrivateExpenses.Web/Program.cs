using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Infrastructure;
using PrivateExpenses.Infrastructure.Persistence;
using PrivateExpenses.Infrastructure.Persistence.Seed;
using PrivateExpenses.Web.Components;
using PrivateExpenses.Web.Services;

// Dutch date/number formatting everywhere (section 74) — money itself always goes through
// MoneyFormatter regardless of thread culture, but dates and any other culture-aware display should
// default to nl-NL too.
var dutchCulture = CultureInfo.GetCultureInfo("nl-NL");
CultureInfo.DefaultThreadCurrentCulture = dutchCulture;
CultureInfo.DefaultThreadCurrentUICulture = dutchCulture;

var builder = WebApplication.CreateBuilder(args);

// The SQLite file and upload folder live under a relative "data/" path so a plain `dotnet run` (or
// the published exe) just works without extra setup — resolve that against the app's content root
// and make sure the folders exist, since SQLite will not create missing parent directories itself.
ResolveAndEnsureDataDirectories(builder.Configuration, builder.Environment.ContentRootPath);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddScoped<ViewAsState>();

var app = builder.Build();

// Apply pending EF Core migrations (never EnsureCreated/EnsureDeleted — migrations are the only
// sanctioned way this app touches schema, per section 65) and seed the fixed reference data.
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PrivateExpensesDbContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await context.Database.MigrateAsync();
    await DbSeeder.SeedCoreDataAsync(context);

    if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedDemoData"))
    {
        await DevelopmentDataSeeder.SeedDemoExpensesAsync(context);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Receipt files live outside wwwroot and are only ever served through this controlled endpoint
// (section 9/85) — never a direct static file path, and 404s for anything that isn't a known document.
app.MapGet("/api/receipts/{id:guid}/file", async (Guid id, IReceiptImportService receiptImportService, CancellationToken cancellationToken) =>
{
    var file = await receiptImportService.OpenFileAsync(id, cancellationToken);
    return file is null ? Results.NotFound() : Results.File(file.Content, file.MimeType, enableRangeProcessing: true);
});

// CSV export (section 67) — reuses the exact same ExpenseFilter the "Uitgaven" list page applies, so
// "export what I'm currently looking at" always matches what's on screen.
app.MapGet("/api/export/expenses", async (
    string? search, DateOnly? from, DateOnly? to, Guid? categoryId, Guid? payerPersonId, Guid? involvesPersonId,
    long? minAmountCents, long? maxAmountCents,
    IExportService exportService, CancellationToken cancellationToken) =>
{
    var filter = new PrivateExpenses.Application.Dtos.ExpenseFilter
    {
        SearchText = search,
        FromDate = from,
        ToDate = to,
        CategoryId = categoryId,
        PayerPersonId = payerPersonId,
        InvolvesPersonId = involvesPersonId,
        MinAmountCents = minAmountCents,
        MaxAmountCents = maxAmountCents,
    };

    var csvBytes = await exportService.ExportExpensesToCsvAsync(filter, cancellationToken);
    var fileName = $"uitgaven-{DateTime.Now:yyyy-MM-dd}.csv";
    return Results.File(csvBytes, "text/csv", fileName);
});

app.Run();

static void ResolveAndEnsureDataDirectories(ConfigurationManager configuration, string contentRootPath)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!Path.IsPathRooted(builder.DataSource))
        {
            var absoluteDbPath = Path.Combine(contentRootPath, builder.DataSource);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteDbPath)!);
            builder.DataSource = absoluteDbPath;
            configuration["ConnectionStrings:DefaultConnection"] = builder.ConnectionString;
        }
    }

    var uploadsPath = configuration["ReceiptStorage:RootPath"];
    if (!string.IsNullOrWhiteSpace(uploadsPath))
    {
        var absoluteUploadsPath = Path.IsPathRooted(uploadsPath) ? uploadsPath : Path.Combine(contentRootPath, uploadsPath);
        Directory.CreateDirectory(absoluteUploadsPath);
        configuration["ReceiptStorage:RootPath"] = absoluteUploadsPath;
    }
}
