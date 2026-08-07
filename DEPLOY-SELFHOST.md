# Self-hosting BonSplit on Oracle Cloud's Always Free tier

Genuinely €0/month, forever — no trial timer like Fly.io's. The trade-off is more one-time setup, and
Oracle requires a credit card at signup for identity verification (you won't be charged for Always Free
resources, but only you can enter that — see Part 1).

The app is never exposed on the public internet here. It's reachable only over
[Tailscale](https://tailscale.com) — a private encrypted network between your own devices — which is
also what makes it safe to self-host a login-less app like this one at all.

This guide is split in two parts: things only you can do (account creation, clicking through Oracle's
console), and things we can do together once you hand me SSH access to the VM.

## Part 1 — Things only you can do

### 1a. Create an Oracle Cloud account

Go to [oracle.com/cloud/free](https://www.oracle.com/cloud/free/) and sign up. You'll need to enter a
credit card for identity verification — this is Oracle's requirement, not optional, but Always Free
resources genuinely never bill it as long as you stay within the free limits (which a 3-person app
comfortably does).

### 1b. Create the VM instance

In the OCI Console (after signup):

1. Go to **Compute → Instances → Create Instance**.
2. **Name**: `bonsplit-vm` (or whatever you like).
3. **Image and shape** → Edit:
   - **Image**: Ubuntu 24.04 (or 22.04) — Minimal is fine.
   - **Shape**: click "Change shape" → **Ampere** → `VM.Standard.A1.Flex` → set **2 OCPUs / 12 GB
     memory** (comfortably within the 4 OCPU / 24 GB Always Free allowance, leaves room to spare).
4. **Add SSH keys**: select "Generate a key pair for me" and **download both the private and public
   key** — you'll need the private key to connect (and to hand to me if you want help over SSH).
5. Leave networking/boot volume on the defaults (a "Always Free" VCN and boot volume are created
   automatically).
6. Click **Create**. Provisioning takes a minute or two.

> **If you get an "Out of host capacity" error**: this is a known Oracle Always Free quirk — Ampere
> capacity in a given region sometimes fills up. Try a different **Availability Domain** in the same
> region (a dropdown on the same page), or try again in a few minutes/hours. It's not a mistake on your
> part.

Once it's running, note down the instance's **public IP address** (shown on the instance detail page).

### 1c. Open port 22 for SSH only (everything else stays closed)

By default, Oracle's "Always Free" networking setup already allows inbound SSH (port 22). We will
**not** open any other port — the app itself is only ever reached through Tailscale, never through
Oracle's networking directly. If you want to double check: **Networking → Virtual Cloud Networks →
(your VCN) → Security Lists → Default Security List** should show an ingress rule for `0.0.0.0/0` TCP
port 22, and nothing else needs adding.

## Part 2 — Once the VM exists, tell me:

- The VM's **public IP address**.
- The **private key** file you downloaded (paste its contents, or tell me the local file path if you'd
  rather I not see it directly — I can also just give you the exact commands to run yourself over SSH
  if you prefer not to share the key).

From there I can either SSH in directly and do the rest, or hand you a copy-pasteable script — your
choice. Either way, here's what setup actually involves, for transparency:

1. **Install Docker** (Ubuntu's official convenience script).
2. **Install Tailscale** and join it to your tailnet (`tailscale up` — this opens a one-time browser
   login link the *first* time, tied to your Tailscale account).
3. **Copy the project source** to the VM (since it isn't on GitHub, this happens via `scp`/`rsync`
   directly from your PC, not a `git clone`).
4. **Create `.env`** on the VM from `.env.example`, with a real `SITE_AUTH_PASSWORD`.
5. **`docker compose up -d --build`** — builds the image and starts the app, bound to `127.0.0.1:8080`
   only (never reachable from the public internet).
6. **`tailscale serve --bg 8080`** — this is what actually makes the app reachable, at
   `https://bonsplit-vm.<your-tailnet-name>.ts.net`, with a real auto-renewing HTTPS certificate, over
   Tailscale's private encrypted network only.
7. Install [Tailscale](https://tailscale.com/download) on Wesley and Jos's phones/laptops too, and add
   them to the same tailnet (Tailscale's free plan supports up to 3 users / 100 devices — exactly your
   situation) — then the `https://bonsplit-vm...ts.net` URL works from their devices too, nowhere else.
8. Make sure the container survives a VM reboot: `restart: unless-stopped` in `docker-compose.yml`
   already handles that (Docker itself is set to start on boot by its own installer).

## Updating the app later

```bash
# from your PC, after making code changes:
rsync -avz --exclude 'bin' --exclude 'obj' --exclude '.git' ./ user@<vm-ip>:~/bonsplit/
# then on the VM:
cd ~/bonsplit && docker compose up -d --build
```

## Backups

Same advice as the Fly.io guide: the Docker volume is persistent across restarts/redeploys, but isn't
backed up anywhere else automatically. Use the app's own CSV export periodically, and consider Oracle's
free block volume backups or a simple `docker run --rm -v bonsplit_data:/data -v ~/backups:/backup
ubuntu tar czf /backup/bonsplit-$(date +%F).tar.gz /data` on a cron job.
