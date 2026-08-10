# Deploying BonSplit to Render

This gets the app running on the public internet for the three of you, with the SQLite database and
uploaded receipts stored on a persistent Render disk (survives restarts and redeploys — Render's
containers themselves are ephemeral, the mounted disk isn't).

> **Render's free tier has no persistent disk at all.** A free web service's filesystem is wiped on
> every restart, redeploy, and inactivity spin-down — for an app storing real financial data, that
> means losing the database and every uploaded receipt sooner or later. This guide uses Render's
> **Starter** plan (~$7/month) plus a small disk (~$0.25/GB/month, 1 GB is plenty), which keeps the
> service always-on with real persistent storage — similar cost to running this on Fly.io.

## 1. One-time setup

The code needs to live in a GitHub repository Render can connect to — it doesn't support deploying
straight from your machine like `fly deploy` does. If you haven't already:

```bash
git remote add origin https://github.com/<your-username>/<your-repo>.git
git push -u origin master
```

## 2. Create the Blueprint on Render

1. Go to [dashboard.render.com](https://dashboard.render.com) and sign in (or sign up — no credit
   card required just to create an account, only to activate a paid service).
2. Click **New** → **Blueprint**.
3. Connect your GitHub account if you haven't yet, and pick the repository you just pushed.
4. Render finds [render.yaml](render.yaml) automatically and shows you the `bonsplit` service it
   defines (Docker, Starter plan, 1 GB disk mounted at `/data`).
5. Click **Apply** — Render creates the service and kicks off the first build.

If you'd rather not use a Blueprint, you can create the Web Service manually instead: **New** → **Web
Service** → pick the repo → Runtime: **Docker** → Plan: **Starter** → add a disk (name `bonsplit-data`,
mount path `/data`, size `1 GB`) → add the environment variables listed in [render.yaml](render.yaml)
under `envVars`.

## 3. Wait for the first build

The first build takes a few minutes (downloading the .NET SDK/runtime layers, same as any fresh Docker
build); later ones are faster since Render caches layers. Watch progress in the **Logs** tab.

## 4. Set the shared password

`SiteAuth__Password` is marked `sync: false` in `render.yaml`, which means Render creates the
environment variable but leaves it blank — it deliberately never gets committed to git. Set it
yourself:

1. Open the `bonsplit` service in the Render dashboard.
2. Go to **Environment**.
3. Find `SiteAuth__Password` and set it to a real passphrase the three of you will remember — anyone
   with this password gets full access, since BonSplit itself has no accounts.
4. Save — this triggers a redeploy with the new value.

Optional: to enable real AI receipt scanning instead of manual entry, also add:

```
ReceiptParsing__Provider = Anthropic
ReceiptParsing__AnthropicApiKey = sk-ant-...
```

(get a key from [console.anthropic.com](https://console.anthropic.com) — costs a small amount per
receipt scanned). Skip this and the app stays in `Development` mode, where every upload routes
straight to manual entry — fully usable, just without automatic scanning.

## 5. Open the app

Render shows the URL at the top of the service page, something like
`https://bonsplit-xxxx.onrender.com`. Open it, log in with the shared password from step 4, and you
should see the Dashboard.

## 6. Redeploying after changes

Push to the branch Render is watching (`master` by default):

```bash
git push
```

Render auto-deploys on every push. Migrations run automatically on startup (see `Program.cs`), so
schema changes are picked up without any manual step.

## 7. Custom domain (optional)

**Settings** → **Custom Domains** on the service page, then follow the DNS instructions it gives you
(usually a CNAME to the `onrender.com` address).

## Backups

The Render disk is persistent but isn't automatically backed up elsewhere. For real peace of mind, use
the app's own CSV export ("Uitgaven" → "Exporteren") every so often and keep a copy somewhere else too.

## Moving away from Render later

If you get the free Oracle Cloud VM working (see [DEPLOY-SELFHOST.md](DEPLOY-SELFHOST.md)) and want to
stop paying for Render: export your data first (step above), copy `/data` off the Render disk (or just
re-enter the handful of expenses manually if there aren't many), then delete the Render service from
the dashboard to stop billing.
