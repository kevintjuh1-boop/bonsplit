using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Validation;
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

// Blazor Server's circuit runs over a SignalR connection, which by default caps incoming messages at
// 32 KB — well under the size of an actual receipt photo. Without this, InputFile uploads above ~32 KB
// don't fail with any visible error; they just silently never complete. Match the SignalR limit to
// the same cap ReceiptFileValidator already enforces (20 MB) so the two never disagree, plus a little
// headroom for the base64/interop framing overhead around the raw file bytes.
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = ReceiptFileValidator.MaxFileSizeBytes + 64 * 1024;
});

// Without this, ASP.NET Core generates a fresh Data Protection key in the container's local
// filesystem on every start — on a host like Fly.io where the container itself is ephemeral (only
// the mounted volume persists), that means every redeploy silently invalidates antiforgery tokens
// for anyone with a page still open. Persisting the key ring to the same durable data directory as
// the database and receipts fixes that; on plain `dotnet run` it just lands next to them locally.
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"] ?? "data/keys";
var absoluteKeyRingPath = Path.IsPathRooted(keyRingPath) ? keyRingPath : Path.Combine(builder.Environment.ContentRootPath, keyRingPath);
Directory.CreateDirectory(absoluteKeyRingPath);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(absoluteKeyRingPath));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddScoped<ViewAsState>();
builder.Services.AddScoped<CurrentPersonState>();

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
// Behind a reverse proxy that terminates TLS itself (Fly.io, and most other hosts) the app only ever
// sees plain HTTP from the proxy — without trusting the proxy's forwarded headers, UseHttpsRedirection
// below would try to redirect every request, including ones that already arrived over HTTPS at the
// edge, producing a redirect loop. This is a no-op for plain `dotnet run` locally, since nothing sends
// these headers there.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
// ASP.NET Core only trusts forwarded headers from loopback by default. Fly's edge proxy isn't
// loopback but is the only thing that can reach this container — clearing these makes every proxy
// hop trusted, which is safe specifically because there's no way to reach the app except through it.
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// This app has no login by design (Kevin/Wesley/Jos are domain records, not accounts). That's fine
// on a private network, but the moment it's reachable on the public internet, "no login" means
// "anyone with the URL can read and edit everyone's expenses". SiteAuth:Password (set only in
// production, e.g. via a Fly.io secret) gates the whole app behind one shared HTTP Basic Auth
// password known to the three of you — not a real user system, just a door lock. Leaving it unset
// (the default for local development) disables this entirely.
// Host health checks (Render, and any similar platform) probe a fixed path with no credentials —
// map it before the password gate below so deploys don't get stuck waiting for a 200 that a
// password-protected "/" would never give back.
app.MapGet("/healthz", () => Results.Ok());

var sharedSitePassword = app.Configuration["SiteAuth:Password"];
if (!string.IsNullOrWhiteSpace(sharedSitePassword))
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/healthz")
        {
            await next();
            return;
        }

        if (!HasValidSharedPassword(context.Request, sharedSitePassword))
        {
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"BonSplit\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next();
    });
}

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

static bool HasValidSharedPassword(HttpRequest request, string expectedPassword)
{
    if (!request.Headers.TryGetValue("Authorization", out var header) ||
        !header.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    try
    {
        var encoded = header.ToString()["Basic ".Length..];
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var separatorIndex = decoded.IndexOf(':');
        if (separatorIndex < 0)
        {
            return false;
        }

        // Any username is accepted — this isn't a real user system, just one shared password for the
        // three of you. Compared in fixed time so response timing can't leak how much of the guess matched.
        var suppliedPassword = decoded[(separatorIndex + 1)..];
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedPassword);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedPassword);
        return suppliedBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
    catch (FormatException)
    {
        return false;
    }
}

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
