# PrivateExpenses

A private expense-splitting app for three housemates — Kevin, Wesley en Jos. Upload a receipt, let AI
read it, correct what it got wrong, assign each product line to the people who bought it, and the app
keeps running balances between the three of you. No login, no invoicing, no bookkeeping — just "wie
heeft wat betaald en wie is wie nog wat schuldig."

This is not business accounting software. There's no VAT filing, no invoicing, no bank integrations, no
multi-tenant support — it's a small tool for three specific people to split everyday expenses.

## What it does

- Upload a photo or PDF of a receipt; an AI vision model extracts the merchant, date, total and product
  lines.
- Review and correct the extracted lines before anything is saved — the app never silently saves
  financial data it isn't sure about.
- Assign each line to one or more of Kevin/Wesley/Jos, with quick per-person chips, bulk "alles
  gezamenlijk" assignment, and custom splits by amount or percentage when a line isn't split evenly.
- Pick who paid the receipt.
- The app computes exact, cent-accurate shares per person (no floating-point rounding drift) and tracks
  each person's running balance.
- Register settlement payments between people (full or partial) without ever altering the original
  expense history.
- Everything stays reviewable: every saved expense, its original receipt document, and the full
  settlement history remain visible and editable.
- Manual expense entry works even without a photo, and works even if AI receipt parsing is unavailable
  or unconfigured — receipt AI is a convenience layer on top of the app, not a dependency it needs to
  function.

## Tech stack

- .NET 10 (LTS) / C# 14
- ASP.NET Core, Blazor Web App with Interactive Server render mode
- Entity Framework Core 10 + SQLite (chosen for zero-setup local use; the data access layer is written
  behind repository interfaces so a later move to PostgreSQL is a swap, not a rewrite)
- Anthropic Claude (vision) for receipt/invoice parsing, via the official `Anthropic` .NET SDK — entirely
  optional; see [Configuring receipt parsing](#configuring-receipt-parsing) below
- xUnit for unit and integration tests

### Architecture

```
src/
  PrivateExpenses.Domain          — entities, money/split calculations, no external dependencies
  PrivateExpenses.Application     — use cases, DTOs, service interfaces (no EF Core, no Blazor)
  PrivateExpenses.Infrastructure  — EF Core, SQLite, file storage, the Anthropic receipt parser
  PrivateExpenses.Web             — Blazor UI, DI composition root
tests/
  PrivateExpenses.UnitTests        — money/split/balance calculation tests
  PrivateExpenses.IntegrationTests — real SQLite-backed persistence and service tests
```

Dependencies only point inward: Web → Application → Domain, with Infrastructure implementing the
interfaces Application defines. The Domain layer has zero knowledge of EF Core, Blazor, HTTP, or any AI
library — all money math and splitting logic is plain, framework-free C#.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or newer
- No database server, no Docker, no external services required to run the app with manual expense entry

## Getting started

```bash
dotnet restore
dotnet ef database update --project src/PrivateExpenses.Infrastructure --startup-project src/PrivateExpenses.Web
dotnet run --project src/PrivateExpenses.Web
```

`dotnet ef database update` is optional on a first run — the app applies pending migrations
automatically at startup — but it's a quick way to create the database ahead of time if you want to.

The app auto-creates its SQLite database and uploads folder under `src/PrivateExpenses.Web/data/` on
first run, and seeds the three people (Kevin, Wesley, Jos) plus the fixed expense categories. Open the
URL printed in the console (typically `https://localhost:7121` in Development).

By default the app runs with `ReceiptParsing:Provider = Development`, which means uploading a receipt
will always show a friendly "kon niet worden uitgelezen" message and route you to manual entry — the app
is fully usable this way, with no AI configured at all. See below to enable real AI parsing.

### Deploying so the three of you can use it over the internet

Two options, both using the same `Dockerfile`:

- [DEPLOY.md](DEPLOY.md) — Fly.io. Fastest to get running, a few euros a month.
- [DEPLOY-SELFHOST.md](DEPLOY-SELFHOST.md) — self-host for free (e.g. an Oracle Cloud Always Free VM),
  reachable only over [Tailscale](https://tailscale.com) rather than the public internet.

Both include a shared password gate (the app itself has no login) and persistent storage for the
database and receipts.

### Running the tests

```bash
dotnet test
```

This runs both the unit tests (money/split/balance calculation logic, no I/O) and the integration tests
(real file-backed SQLite databases created and torn down per test class, exercising the actual
persistence and service layer).

## Configuring receipt parsing

Three providers are available via the `ReceiptParsing:Provider` setting:

| Provider | Behavior | Needs an API key? |
|---|---|---|
| `Development` (default) | Always fails cleanly with a friendly message, forcing manual entry. Safe default — never invents data. | No |
| `Fixture` | Returns canned, clearly-fake demo receipt data, for trying out the review/assignment UI without an API key. Used automatically in the `Development` ASP.NET Core environment (`appsettings.Development.json`). | No |
| `Anthropic` | Real AI vision parsing of your uploaded receipt photos/PDFs via Claude. | Yes |

To enable real AI parsing, set the provider and your API key via `dotnet user-secrets` (never commit an
API key to `appsettings.json`):

```bash
cd src/PrivateExpenses.Web
dotnet user-secrets set "ReceiptParsing:Provider" "Anthropic"
dotnet user-secrets set "ReceiptParsing:AnthropicApiKey" "sk-ant-..."
```

You can also set `ReceiptParsing:Model` (defaults to Claude Opus 5, `claude-opus-5`) if you want to use a
different model. The app fails fast at startup with a clear error if `Provider` is set to `Anthropic`
without an API key configured — it will never silently fall back to a fake parser.

In production, set these via environment variables instead of user-secrets, e.g.
`ReceiptParsing__Provider=Anthropic` and `ReceiptParsing__AnthropicApiKey=sk-ant-...`.

## Data storage

- SQLite database: `src/PrivateExpenses.Web/data/privateexpenses.db`
- Uploaded receipts: `src/PrivateExpenses.Web/data/uploads/` (stored under randomly-generated filenames;
  the original filename is kept only as metadata, never used as a path)

Both are excluded from git via `.gitignore` — this is your real, private financial data, not sample
content. EF Core migrations themselves (`src/PrivateExpenses.Infrastructure/Persistence/Migrations/`)
*are* committed, since they define the schema rather than any data.

## Exporting your data

The "Uitgaven" (expenses) page has an "Exporteren" button that downloads the currently filtered list as a
CSV file (semicolon-delimited, comma decimals, UTF-8) — ready to open directly in Excel with Dutch
regional settings.

## Development-only demo data

Running in the `Development` ASP.NET Core environment with `SeedDemoData: true` (the default in
`appsettings.Development.json`) seeds a handful of realistic example expenses with varied splits, on top
of the always-seeded Kevin/Wesley/Jos and categories. Set `SeedDemoData` to `false` (or run outside
Development) for a clean start with your own real data — demo seeding never runs if any expense already
exists.
