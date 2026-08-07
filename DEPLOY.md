# Deploying BonSplit to Fly.io

This gets the app running on the public internet for the three of you, with the SQLite database and
uploaded receipts stored on a persistent Fly volume (survives restarts and redeploys — Fly's
containers themselves are ephemeral, the mounted volume isn't).

Cost: for three people's normal usage this comfortably fits inside Fly's free monthly allowance. Worst
case it's a few dollars a month — nowhere near what a "real" always-on VM from a big cloud would cost.

> **Trial accounts (no payment method on file) get stopped after 5 minutes.** Fly enforces this
> regardless of `auto_stop_machines`/`min_machines_running` in `fly.toml` — the next request still wakes
> the machine back up (a few seconds' delay), so light testing works fine, but it's not suitable for the
> three of you actually relying on this day to day. Add a card at
> [fly.io/dashboard](https://fly.io/dashboard) → Billing to lift the limit — this is something only you
> can do, not something that can be automated on your behalf.

## 1. One-time setup

Install the Fly CLI and sign up (no credit card required to start):

```bash
# Windows (PowerShell)
iwr https://fly.io/install.ps1 -useb | iex

# macOS/Linux
curl -L https://fly.io/install.sh | sh
```

Then log in (opens a browser):

```bash
fly auth login
```

## 2. Pick a unique app name

Fly app names are globally unique across *all* Fly users. Open [fly.toml](fly.toml) and change:

```toml
app = "bonsplit-CHANGE-ME"
```

to something unique, e.g. `bonsplit-kwj-4471`. Also check `primary_region` — `ams` (Amsterdam) is a
sensible default for NL; run `fly platform regions` to see others.

## 3. Create the app and the persistent volume

From the repository root (where `fly.toml` and `Dockerfile` live):

```bash
fly apps create <your-app-name-from-fly.toml>
fly volumes create bonsplit_data --region ams --size 1
```

`--size 1` is 1 GB — plenty for a SQLite database and years of receipt photos for three people. The
volume name (`bonsplit_data`) must match `source` in `fly.toml`'s `[[mounts]]` section.

## 4. Set the secrets

Two things must **never** go in `fly.toml` or get committed to git — set them as Fly secrets instead:

```bash
# A shared password that gates the whole app behind one login, since BonSplit itself has no
# accounts. Pick something the three of you can remember; anyone with this password gets full access.
fly secrets set SiteAuth__Password="choose-a-real-passphrase-here"

# Optional: only set this if you want real AI receipt scanning instead of manual entry.
# Get a key from https://console.anthropic.com — costs a small amount per receipt scanned.
fly secrets set ReceiptParsing__Provider="Anthropic"
fly secrets set ReceiptParsing__AnthropicApiKey="sk-ant-..."
```

If you skip the `ReceiptParsing` secrets, the app defaults to `Development` mode (per `fly.toml`),
which means every upload routes straight to manual entry — the app is still fully usable, just without
automatic receipt scanning.

## 5. Deploy

```bash
fly deploy
```

This builds the `Dockerfile`, pushes the image, and starts the app. First deploy takes a few minutes
(downloading the .NET SDK/runtime layers); later deploys are much faster since Docker layers are cached.

When it's done:

```bash
fly status
```

shows you the URL (`https://<your-app-name>.fly.dev`). Open it, log in with the shared password from
step 4, and you should see the Dashboard.

## 6. Redeploying after changes

Any time you (or Claude) change the code:

```bash
fly deploy
```

That's it — same command every time. Migrations run automatically on startup (see `Program.cs`), so
schema changes are picked up without any manual step.

## 7. Custom domain (optional)

If you own a domain and want e.g. `bonsplit.jouwdomein.nl` instead of the `.fly.dev` URL:

```bash
fly certs add bonsplit.jouwdomein.nl
```

then follow the DNS instructions it prints (usually a CNAME to `<your-app-name>.fly.dev`).

## Backups

The Fly volume is persistent but isn't automatically backed up elsewhere. Fly takes periodic volume
snapshots you can restore from (`fly volumes list`, `fly volumes snapshots list`), but for real peace
of mind, use the app's own CSV export ("Uitgaven" → "Exporteren") every so often and keep a copy
somewhere else too.

## Turning the shared password off

If you ever want to go back to no login (e.g. testing locally), just don't set `SiteAuth__Password` —
locally it's already unset by default, so `dotnet run` never asks for a password.
