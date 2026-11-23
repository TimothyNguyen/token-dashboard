# Troubleshooting

Common errors and how to fix them.

## Port already in use (EADDRINUSE)

**Symptom:** `token-dashboard serve` fails with `OSError: [Errno 10048] Address already in use` or similar.

**Cause:** Another process (often another TokenDashboard instance) is bound to port 55423.

**Fix:**
1. Run diagnostics to check if TokenDashboard's managed service is active and whether the port answers with TokenDashboard's `/health` fingerprint:
```bash
token-dashboard doctor
```
2. If the port belongs to TokenDashboard's own managed service, the dashboard is already running there — opening `http://127.0.0.1:55423` is usually the fix. If you need to stop it:

```bash
# Linux / WSL2
systemctl --user stop token-dashboard

# macOS
launchctl unload ~/Library/LaunchAgents/com.token_dashboard.token_dashboard.plist

# Windows (Task Scheduler)
schtasks /End /TN TokenDashboard
```

To remove the managed service entirely, run `token-dashboard uninstall`.

3. If another process is holding the port, terminate it manually:

```bash
# Windows: find and kill the process
netstat -ano | findstr 55423
taskkill /PID <pid> /F

# macOS/Linux
lsof -i :55423
kill <pid>
```

Or use a different port:
```bash
token-dashboard serve --port 55424
```

## UsageDatabaseSchemaTooNewError

**Symptom:** `token-dashboard serve` or `token-dashboard db status` fails with `UsageDatabaseSchemaTooNewError`.

**Cause:** The usage database was written by a newer version of TokenDashboard than the one currently running.

**Fix:** Update TokenDashboard:
```bash
token-dashboard update
```

Then restart any other TokenDashboard processes using this data directory.

Do NOT delete the database — it contains your full usage history.

## SQLite WAL lock / database is locked

**Symptom:** `token-dashboard db status` or `token-dashboard serve` fails with `sqlite3.OperationalError: database is locked`.

**Cause:** WAL mode lets readers and a writer coexist, so the error means another process is writing: an active sync, `token-dashboard db resync`, or a second server on the same data directory.

**Fix:** Wait for the writing operation to finish, or stop that process.

If entries appear missing, run `token-dashboard db sync` first. For database maintenance command definitions (`sync`, `repair`, `resync`), see [DB maintenance commands in CONFIG.md](../reference/CONFIG.md#db-maintenance-commands).

## macOS Keychain prompts during Claude quota polling

**Symptom:** macOS shows Keychain consent prompts when TokenDashboard polls Claude Code quota.

**Cause:** The prompt comes from reading the Claude Code credential item in macOS Keychain to determine the active plan. That read is gated on `credential_scan`, not `claude_api`.

**Fix:**
- Grant access when prompted, or
- Disable credential scanning:
```bash
token-dashboard quota consent --credential-scan off
```
- Or turn off all quota tracking:
```bash
token-dashboard quota consent --enabled off
```

These consent commands save to `config.json` and apply immediately to the running server. For credential scanning semantics and quota configuration, see [Quota tracking internals](../reference/QUOTA.md#consent-semantics).

## Claude quota: "refused requests from this network"

**Symptom:** The Claude Code quota card says its quota API refused requests from this network (HTTP 403) and its bars stop updating, while Claude Code itself works.

**Cause:** Anthropic answered the poll with `403 {"error": {"type": "forbidden", "message": "Request not allowed"}}`, which it sends to a network it does not serve. The sign-in is fine. This usually means your terminal reaches Anthropic through a proxy but the TokenDashboard background service does not: a systemd or launchd service does not read your shell profile, and WSL2's `autoProxy` sets `HTTPS_PROXY` in terminal sessions only. On Linux and WSL2, Python takes its proxy from those environment variables alone; on macOS and Windows it also reads the system proxy settings.

**Fix (Linux / WSL2, systemd):** give the service the proxy with a drop-in. `token-dashboard setup` and `token-dashboard update` leave drop-ins alone:

```bash
mkdir -p ~/.config/systemd/user/token-dashboard.service.d
cat > ~/.config/systemd/user/token-dashboard.service.d/proxy.conf <<'EOF'
[Service]
Environment=HTTPS_PROXY=http://127.0.0.1:7890
Environment=NO_PROXY=localhost,127.0.0.1,::1
EOF
systemctl --user daemon-reload && systemctl --user restart token-dashboard
```

Replace `http://127.0.0.1:7890` with your proxy's address (`echo $HTTPS_PROXY` in a terminal where Claude Code works). The next poll, or the dashboard's **Refresh** button, fills the card again. `token-dashboard uninstall` does not remove the drop-in; delete the `token_dashboard.service.d` directory yourself.

## WSL2 localhost binding

**Symptom:** Dashboard is accessible from WSL2 but not from Windows browser.

**Cause:** Localhost forwarding from Windows to WSL2 is disabled (for example, `localhostForwarding=false` in `%USERPROFILE%\.wslconfig`).

**Fix:**
Windows' localhost forwarding reaches a WSL2 loopback bind by default, so `token-dashboard serve`'s default `127.0.0.1` bind works directly from Windows. Ensure `localhostForwarding=true` (or unset, the default) in `%USERPROFILE%\.wslconfig` on the Windows host (not in the WSL home directory). `.wslconfig` changes only take effect after the WSL VM restarts — run `wsl --shutdown` from Windows, then reopen your distro.

Do **not** use `--bind 0.0.0.0` — binding to non-loopback disables writes entirely (see [SECURITY.md](../SECURITY.md)).

For setups where localhost forwarding is not available or for advanced access methods, see the [Remote access guide](REMOTE_ACCESS.md).

## Enabling OTel exports for GitHub Copilot

**Symptom:** GitHub Copilot input and cache tokens are zero (only output tokens appear from the `session-state` fallback).

**Cause:** Copilot does not write token usage by default without OpenTelemetry export enabled.

**Fix:**
```bash
# Windows (use setx or System Properties for persistence — `set` only lasts for that console)
setx COPILOT_OTEL_FILE_EXPORTER_PATH "%USERPROFILE%\.copilot\otel\usage.jsonl"

# macOS/Linux
export COPILOT_OTEL_FILE_EXPORTER_PATH=~/.copilot/otel/usage.jsonl
```

`COPILOT_OTEL_FILE_EXPORTER_PATH` names a single file, not a directory.

## Dashboard shows no data / empty state

**Symptom:** Dashboard loads but shows no usage data.

**Cause:** No supported clients found, or usage database is empty.

**Fix:**
1. Run diagnostics:
```bash
token-dashboard doctor
```
2. Check what's in the database:
```bash
token-dashboard db status
```
3. If the database is empty, force a sync:
```bash
token-dashboard db sync
```

## Update check fails

**Symptom:** The update check in `token-dashboard doctor` or the dashboard reports an error or no result.

**Cause:** No network connectivity to PyPI, or the opt-in update check is disabled.

**Fix:**
```bash
# See what an upgrade would run, without changing anything
token-dashboard update --dry-run
```

The update check is opt-in and off by default. It runs only when requested (`token-dashboard doctor` or the dashboard's check) and only when enabled. `TOKEN_DASHBOARD_UPDATE_CHECK=0` turns that opt-in check off — it does not affect manual upgrades via `token-dashboard update`.

If the check reports a network error, confirm the machine can reach PyPI. `token-dashboard update --dry-run` does not itself contact PyPI; it only prints the command an upgrade would run.

## See also

- [Onboarding guide](ONBOARDING.md) — initial setup and `token-dashboard doctor`
- [Remote access guide](REMOTE_ACCESS.md) — reaching your instance from another machine
- [Configuration reference](../reference/CONFIG.md) — all environment variables and database commands
- [Quota tracking internals](../reference/QUOTA.md) — consent semantics and credential scanning
- [API reference](../reference/API.md) — HTTP API endpoints
- [Security policy](../SECURITY.md) — write-protection model
