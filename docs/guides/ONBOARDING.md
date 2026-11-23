# Onboarding: setup, doctor, update, uninstall

TokenDashboard ships a Python-native lifecycle so you can run it as a background service and
cleanly remove it later — no shell scripts, no `sudo`, no system-wide changes.

> **Install ≠ setup.** `pip install` / `pipx install` only installs the package. It
> creates no service, writes no config, and reaches no network. For new installs,
> **`token-dashboard setup` is the recommended first-run path**. **`token-dashboard serve` still works
> with zero setup**, so existing users (and anyone with a hand-written systemd/launchd
> unit) keep working unchanged. Everything setup creates is reversible with
> `token-dashboard uninstall`.

## Quick start

```bash
pipx install token-dashboard        # recommended: isolated env + a stable `token-dashboard` on PATH
token-dashboard setup               # configure + start a loopback-only background service
```

Then open the URL it prints (default `http://127.0.0.1:55423`). To remove everything
setup created while keeping your history:

```bash
token-dashboard uninstall
```

For scripts, agents, and bundlers, use the non-interactive contract:

```bash
token-dashboard setup --auto --json
```

## `token-dashboard setup`

Configures a user-level background service. Two routes, one engine:

| Route | Command | Behavior |
|---|---|---|
| Easy / auto | `token-dashboard setup --auto` | No prompts. Safe local-only defaults. Intended for scripts, CI, and bundlers. |
| Expert | `token-dashboard setup` | Shows the plan and asks before applying (requires a terminal). |

**Safety rule:** with no terminal *and* neither `--auto` nor `--yes`, setup prints the
plan and exits non-zero **without changing anything** — a command that wasn't explicitly
non-interactive never mutates your machine just because it ran without a TTY.

Useful flags (all reused from the global parser where noted):

| Flag | Meaning |
|---|---|
| `--auto` | Non-interactive easy route. |
| `-y`, `--yes` | Apply without prompting (interactive route, skip confirmation). |
| `--dry-run` | Print the plan and change nothing. |
| `--json` | Machine-readable result (for bundlers); non-zero exit on failure. |
| `--bind` / `--port` | Override bind address / port (default `127.0.0.1:55423`). |
| `--runtime {auto,existing,pipx,venv,binary}` | Which interpreter the service uses (see below). |
| `--service {auto,systemd,launchd,none}` / `--no-service` | Background service type. |
| `--force` | Replace a pre-existing **unmarked** `token_dashboard.service` (setup refuses by default). |

If you already have a hand-written `token_dashboard.service` or launchd plist, setup does not
silently replace it. By default it refuses unmarked service files, even if the port already
serves TokenDashboard. Keep managing that service yourself, remove it before setup, or run
`token-dashboard setup --force` after checking `token-dashboard setup --dry-run`. For pre-1.0 manual
systemd services that occupy `55423` but do not expose the new `/health` fingerprint,
`--force` rewrites and restarts the existing `token_dashboard.service`. Use `--no-service` when you
want setup state without creating a background service.

**Optional update-notices step (interactive only).** Just before the quota step, an
interactive setup also asks whether to enable opt-in update notices (default **Yes** — bare
Enter enables), calling the same `updatecheck.enable()` the dashboard's consent link uses.
The `TOKEN_DASHBOARD_UPDATE_CHECK=0` hard kill switch (see "Update checks" below) skips the prompt
entirely, and it is skipped if update checks are already enabled. Like the quota step,
`--auto`, `--yes`, `--json`, and non-TTY runs skip it so scripted setup never prompts.

**Optional quota step (interactive only).** After a successful interactive setup, the wizard
offers the same quota-tracking decisions the dashboard's Quota tab exposes: the master switch
(`quota.enabled`), per-provider quota **API** consent (default No — without opting in, quota
stays local-only), and the background poll interval (15/30/60/120 minutes). `--auto`, `--yes`,
`--json`, and non-TTY runs skip the step entirely, so scripted setup never prompts; everything
can be changed later from the Quota tab or with `token-dashboard quota consent`.

### Runtime selection

- **`auto` / `existing`** (default): point the service at the interpreter that ran setup.
  No install, no network. Kept on uninstall.
- **`pipx`**: use an existing `pipx install token-dashboard` environment (run `pipx install
  token-dashboard` first). Kept on uninstall.
- **`venv`**: setup creates a dedicated managed venv under `<data_dir>/runtime/python-venv`,
  pinned to the version that ran setup, and **owns** it (removed on uninstall). Use this to
  isolate the service from a churn-prone conda/system environment.
- **`binary`**: reserved for future standalone binaries.

Setup never creates a `pipx` environment — pipx is *your* install path, so uninstall never
touches it.

### Migrating an existing setup to a managed venv

If setup currently records `install_method: existing` (common for conda, system Python, or
`python -m pip install --user` installs), `token-dashboard update` will not mutate that interpreter.
To let TokenDashboard own the service runtime and manage future upgrades, re-run setup with a
managed venv:

```bash
# Upgrade the token-dashboard command you are about to run, for example:
python3 -m pip install --user -U token-dashboard
# or, for a conda base install:
conda run -n base python -m pip install -U token-dashboard
token-dashboard setup --runtime venv --force
token-dashboard doctor
```

This keeps usage history and config under `<data_dir>`, rewrites the user service to run
`<data_dir>/runtime/python-venv/bin/python -m token-dashboard`, and records
`install_method: managed-venv` in `install.json`. Future `token-dashboard update` runs
`pip install -U token-dashboard` inside that venv and restarts the service. If your runtime is pipx,
you can keep it instead; `token-dashboard update` knows how to call `pipx upgrade token-dashboard`.

### Background service by platform

| Platform | Backend | Notes |
|---|---|---|
| Linux / WSL2 (systemd) | systemd **user** service (`~/.config/systemd/user/token-dashboard.service`) | `--service auto` picks it when a user systemd manager is available |
| macOS | launchd **user** agent (`~/Library/LaunchAgents/com.token_dashboard.token_dashboard.plist`) | `--service auto` picks it when `launchctl` is available |
| WSL2 without systemd / other | none | setup records the manifest + prints foreground guidance |

Both backends write a unit/plist carrying an ownership marker, bring it up, and are reverted
the same way by `token-dashboard uninstall`. No `sudo` for the local service, no system-wide units.

### Network behavior & remote access

Defaults are loopback-first: `127.0.0.1:55423`. `--auto` is **strictly local-only** and
refuses a non-loopback bind. The local API is unauthenticated, so **write endpoints are
automatically disabled unless the server is bound to loopback** (see `docs/SECURITY.md`).

Interactive `token-dashboard setup` keeps offering Tailscale Serve after a successful loopback
setup, with **No selected by default**, and records the exact targeted teardown for
`token-dashboard uninstall` when confirmed. `--auto`/`--yes` never configure Serve; they only print
remote-access guidance. Other access methods — SSH forwarding (the only one that keeps
browser writes), Cloudflare Tunnel behind Cloudflare Access, and authenticated reverse
proxies — are configured outside onboarding. An explicit non-loopback bind remains
read-only.

See [`REMOTE_ACCESS.md`](REMOTE_ACCESS.md) for commands, URLs, WSL2 guidance, and the
trade-offs between SSH forwarding, Tailscale Serve, Cloudflare Tunnel, reverse proxies, and
wildcard binding. See [`SECURITY.md`](../SECURITY.md) for the complete write-protection
model.

## `token-dashboard doctor`

Diagnoses runtime fitness, the service (systemd or launchd), the recorded port,
prerequisites, and config/data locations — plus, if update checks are enabled, whether a
newer version is available. It also reports quota-tracking state: the master switch,
per-provider API consent, the `TOKEN_DASHBOARD_QUOTA_POLL=0` kill switch, the effective poll
interval and its source (env/config/default), the last poll time, and the stored snapshot
count. `--json` for machine-readable output. It probes the **port
recorded in the manifest** (setup may have auto-picked a free one), and flags a
`TOKEN_DASHBOARD_DATA_DIR` mismatch between the manifest and the current environment.

```bash
token-dashboard doctor          # human summary
token-dashboard doctor --json   # machine-readable
```

## `token-dashboard update`

Upgrades the runtime **in place** and restarts the service if it is managed. Because you
asked to upgrade, this works regardless of who owns the runtime — but only for an install
method TokenDashboard knows how to drive:

| Recorded install method | What `update` does |
|---|---|
| `pipx` | `pipx upgrade token-dashboard`, then restart a managed service |
| `managed-venv` | `pip install -U token-dashboard` inside the managed venv, then restart |
| `existing` (unknown package manager) | **Prints guidance, changes nothing** — upgrade it the way you installed it |
| `binary` | Deferred (prints guidance) |
| no manifest | Prints guidance (`token-dashboard setup` first, or upgrade manually) |

```bash
token-dashboard update            # upgrade + restart
token-dashboard update --dry-run  # show the exact command(s), change nothing
token-dashboard update --json     # machine-readable result
```

`update` never upgrades an interpreter setup did not create — it will not run
`pip install -U` against your system/conda Python. If the upgrade installs but the managed
service fails to restart, `update` reports failure (non-zero exit) rather than a misleading
success — the service would otherwise keep running the old code.

Human output and `--json` include the TokenDashboard runtime version before and after the upgrade
when it can be read. A real version change is shown as `old -> new`; an unchanged version is
reported as already current, while still noting whether the background service was restarted.

### Update checks (opt-in, default-off)

TokenDashboard never checks for updates on its own. You can opt in, and then a check runs only when
you explicitly ask:

- Enable with `TOKEN_DASHBOARD_UPDATE_CHECK=1`, or persist consent (`POST /api/update-check/consent`,
  which the dashboard can call). `TOKEN_DASHBOARD_UPDATE_CHECK=0` is a hard kill switch that overrides
  saved consent.
- When enabled, `token-dashboard doctor` reports whether a newer version is on PyPI, and
  `GET /api/update-check` (read-only — PyPI read + in-memory cache, not write-gated, so it
  works over Tailscale/WSL/any forward) returns the comparison.
  Results are cached for hours; there are no automatic background checks. The check itself
  only *reports* availability. On an eligible managed install the dashboard can go one step
  further and apply the update for you — see "Dashboard updates" below — and anywhere else
  you run `token-dashboard update`.

### Dashboard updates (click to update)

On a **managed install** (setup-created pipx/venv runtime with a managed service on
Linux/systemd), the update notice carries an **Update to vX.Y.Z** button instead of only the
terminal command. What the click runs is decided server-side — the page's claims prove nothing:

- Only when eligibility passes (recorded install method, intact service identity); everything
  else keeps the copyable `token-dashboard update` command as the whole story.
- The apply does **not** run inside the server (the upgrade replaces the code that server is
  serving). The server stages the dependency-free updater OUTSIDE the package tree and hands it
  to `systemd-run --user`, which survives the service it is about to stop: stop → usage-DB
  backup → exact-version install → start → bounded application-readiness probe, with service
  recovery attempted on every failure path and the outcome recorded in a durable journal.
- The popover shows live phases; a response lost to the planned outage recovers the job without
  a second click, a failure offers Try again, and the success notice dismisses permanently once
  acknowledged.

Remote browsers can drive the same flow, but only with an explicit opt-in:

1. **Pin the exact public origin**: `TOKEN_DASHBOARD_UPDATE_ORIGIN=https://<host>.ts.net` (env, or
   `update_origin` in `config.json`). Unset = the remote update plane is completely off.
2. **Mint a pairing code on the host**: `token-dashboard update-enroll`. Codes are single-use —
   consumed whether the browser's guess is right or wrong — and enrollment attempts are
   rate-limited.
3. **Enter the code once** in the browser's update popover. The browser receives a `Secure`
   `HttpOnly` host-only session cookie and an in-memory CSRF token (re-minted automatically
   after a reload over the still-valid session); then it can click Update wherever that HTTPS
   origin serves the dashboard.

The security model for all of this is `docs/SECURITY.md` → "The one authenticated-write
exception".

## `token-dashboard uninstall`

Reverts **exactly** what setup created, driven by the `install.json` manifest — never by
guessing. It is an interactive wizard (mirror of setup) that asks the two decisions that
matter, with these confirmed defaults:

- Remove the setup-owned managed runtime? **default yes** (only when setup owns it)
- Delete usage history / config? **default no** (your data is kept)

```bash
token-dashboard uninstall              # interactive wizard
token-dashboard uninstall --auto       # apply the defaults non-interactively (bundler teardown)
token-dashboard uninstall --dry-run    # print the revert plan, change nothing
token-dashboard uninstall --purge      # also delete usage history + config
token-dashboard uninstall --keep-runtime   # remove the service but keep a managed runtime
token-dashboard uninstall --force      # remove an unmarked / replaced token_dashboard.service (see below)
```

### Safety guarantees

- **Never** uninstalls Python.
- **Never** removes a TokenDashboard install you made yourself (pipx / existing interpreter).
- **Never** deletes usage history unless you pass `--purge`.
- Removes a `token_dashboard.service` only when the on-disk unit still carries setup's ownership
  marker (`X-TokenDashboard-Managed`). If the manifest is missing, or the unit was replaced by a
  hand-written one, uninstall refuses and asks you to confirm with `--force`. The same
  marker rule protects the managed runtime tree (`.token-dashboard-managed`).
- Idempotent and partial-safe: a half-finished setup still converges to a clean state.

## State and the manifest

All setup state lives under the resolved data dir (`$TOKEN_DASHBOARD_DATA_DIR`, else `~/.token-dashboard`):

| Path | What |
|---|---|
| `<data_dir>/install.json` | The revert manifest (what setup created, ownership flags). |
| `<data_dir>/runtime/python-venv` | The managed venv (only with `--runtime venv`). |
| `<data_dir>/usage.sqlite3` (+ `-wal`/`-shm`) | Usage history (kept unless `--purge`). |
| `<data_dir>/pricing_db.json` | Dashboard pricing edits (override; fully replaces the packaged baseline when present, survives `token-dashboard update`; delete it to restore defaults). |
| `~/.config/systemd/user/token-dashboard.service` | The systemd user unit (honors `$XDG_CONFIG_HOME`). |

Set `TOKEN_DASHBOARD_DATA_DIR` to redirect **all** of the above (handy for testing setup/uninstall
safely against a throwaway directory).

`XDG_CONFIG_HOME` changes where TokenDashboard writes the systemd unit file, but it is not a full
systemd namespace. If your user manager already has `token_dashboard.service` loaded from another
path, setup verifies the loaded unit path and the configured `/health` endpoint before it
reports success.

## Bundling TokenDashboard from another package

A parent installer can onboard and tear down TokenDashboard through the `--auto --json` contract,
always invoking the exact interpreter it installed (never a bare `token-dashboard`):

```bash
# onboard
<that-python> -m token-dashboard setup --auto --json
# teardown (the runtime is parent-owned, so uninstall keeps it)
<that-python> -m token-dashboard uninstall --auto --json
# then remove what you installed, e.g. `pipx uninstall token-dashboard`
```

`--json` results carry `ok` plus the resolved URL/service details; a non-zero exit means
the action failed (or was refused). Check `ok` / the exit code, not the prose.

See also: [Troubleshooting guide](TROUBLESHOOTING.md) for common errors.
