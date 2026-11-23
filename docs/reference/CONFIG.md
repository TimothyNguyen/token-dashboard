# Configuration reference

Environment variables, estimation method, and database semantics for TokenDashboard.
TokenDashboard is **localhost-only by default**.

## Server & cache

- `TOKEN_DASHBOARD_HOST` (default: `127.0.0.1`)
- `TOKEN_DASHBOARD_PORT` (default: `55423`)
- `TOKEN_DASHBOARD_LOG_LEVEL` (default: `info`) — uvicorn log level for `token-dashboard serve`; also the default behind `--log-level` (any level uvicorn accepts: `critical`, `error`, `warning`, `info`, `debug`, `trace`)
- `TOKEN_DASHBOARD_PUBLIC_BASE_PATH` (unset) — override for generated asset URLs that only works behind a proxy that strips the prefix; normalized to a leading `/` and no trailing `/`, and `/` or empty means no prefix. A request's `?base=` query and `X-Forwarded-Prefix`/`X-Script-Name` headers take precedence over the env var
- `TOKEN_DASHBOARD_CACHE_TTL` (default: `600` seconds)
- `TOKEN_DASHBOARD_CACHE_MAX_ENTRIES` (default: `256`) — bound cached API responses and their idle per-key locks
- `TOKEN_DASHBOARD_COMPUTE_CONCURRENCY` (default: `max(2, min(8, cpus // 2))`) — cap on simultaneous heavy history reparses; excess cold requests park waiting for a slot (up to `TOKEN_DASHBOARD_COMPUTE_WAIT_SECONDS`) and return `503` only when the wait times out or the waiter cap is full, instead of saturating the server under load
- `TOKEN_DASHBOARD_COMPUTE_THREAD_BUDGET` (default: `32`) — total thread budget shared by computing requests and the ones parked waiting for a slot, kept below AnyIO's default 40-thread pool so `/health` and cache hits are never starved; positive integer
- `TOKEN_DASHBOARD_COMPUTE_MAX_WAITERS` (default: `TOKEN_DASHBOARD_COMPUTE_THREAD_BUDGET - TOKEN_DASHBOARD_COMPUTE_CONCURRENCY`) — how many requests may park waiting for a compute slot at once; the default is thread budget minus concurrency, floored at `0` (e.g. `30` when budget is `32` and concurrency is `2`); non-negative integer, `0` restores the old instant-refuse behaviour
- `TOKEN_DASHBOARD_COMPUTE_WAIT_SECONDS` (default: `15` seconds, max `120`) — how long a cold request parks for a compute slot before returning `503`; positive finite float, clamped to 120s (bad or non-finite values fall back to the default)
- `TOKEN_DASHBOARD_FORCE_REFRESH_JOIN_SECONDS` (default: `60` seconds, max `300`) — how long a forced refresh (the Refresh button) waits for a fill already in flight for its key before falling back to the stale body; positive finite float, clamped to 300s
- `TOKEN_DASHBOARD_STARTUP_WARM_JOIN_SECONDS` (default: `30` seconds) — how long the first request for a key still being warmed at startup waits for that fill instead of returning `503`; at most one request per key waits
- `TOKEN_DASHBOARD_LIMIT_CONCURRENCY` (default: `64`) — uvicorn connection cap (backpressure)
- `TOKEN_DASHBOARD_KEEPALIVE` (default: `5` seconds) — uvicorn keep-alive timeout
- `TOKEN_DASHBOARD_ALLOW_ORIGINS` (comma-separated, default: empty)
- `TOKEN_DASHBOARD_ALLOW_ORIGIN_REGEX` (default CORS policy allows localhost/127.0.0.1 and same-tailnet Tailscale Serve reads; setting either CORS option replaces that default policy)
- `TOKEN_DASHBOARD_UPDATE_ORIGIN` (unset) — exact HTTPS origin allowed to pair a remote browser for click-to-update; unset means remote updates are entirely off (`token-dashboard update-enroll` mints the codes)
- `TOKEN_DASHBOARD_NO_RETENTION_NOTICE` (set to `1` to silence the history-retention reminder printed on `token-dashboard serve`)
- `TOKEN_DASHBOARD_SETUP_NO_OPEN` (set to `1` to skip the optional browser open at the end of `token-dashboard setup`)

For remote access through Tailscale Serve, SSH forwarding, or an explicit
network bind, see [`docs/guides/REMOTE_ACCESS.md`](../guides/REMOTE_ACCESS.md).
Interactive `token-dashboard setup` can configure and record the Tailscale Serve rule
after you opt in.

## Background warming

Warm-ups run in daemon threads when `token-dashboard serve` starts and on a daily timer.
They are best-effort: a failure is logged and never crashes the server, and
fixture mode (`--dev-fixture`) never starts them.

- `TOKEN_DASHBOARD_WARM_ON_START` (default: `1`) — set to `0` to skip the startup warm of the Overview, Stats, Sessions and Report cache keys (roughly a minute of background CPU against a year of history); the startup warm is the fill a first load can join; without the warm there is nothing to join
- `TOKEN_DASHBOARD_DAILY_WARM` (default: `1`) — set to `0` to disable the daily warm that warms yesterday's closed day plus only the Report tab's week, month and year windows (skipping single-day ones) after local midnight
- `TOKEN_DASHBOARD_DAILY_WARM_MINUTE` (default: `5`) — minutes past local midnight at which the daily warm fires; non-negative integer, `0` is valid (warm exactly at midnight), and any value outside a day (≥ 1440) falls back to `5` rather than wrapping
- `TOKEN_DASHBOARD_DAILY_WARM_JOIN_SECONDS` (default: `10` seconds) — how long a foreground request waits for a daily-warm fill for its key instead of returning `503`; deliberately shorter than `TOKEN_DASHBOARD_STARTUP_WARM_JOIN_SECONDS` because the daily warm runs on a live server; positive integer

## Session active time (estimated)

Every session reports `active_ms` alongside `span_ms`. Span is first-to-last
event; active time subtracts idle by counting each gap between consecutive token
events only up to an idle cap, so a session left open overnight no longer reads
as a 14-hour session.

It is an estimate, and the API says so: `summary.active_time_estimated` is
`true` and `summary.active_time_method` is `capped-inter-event-gap`. The limits
follow from the method — a short pause between events is indistinguishable from
work, a single operation longer than the cap is truncated to it, and a session
with one token event measures zero because nothing precedes it.

Concurrent work is counted two ways. `active_ms` is clock time, with overlap
counted once; `active_ms_sum` adds the overlap up, i.e. agent time. Both appear
per session and per tool in `summary`. Kimi agents and Claude subagents run
alongside the main agent, and each is timed as its own stream: a subagent
working one minute in parallel adds a minute of agent time and none of clock
time.

- `TOKEN_DASHBOARD_ACTIVE_GAP_CAP_SECONDS` (default: `300`) — idle cap in seconds; gaps longer than this contribute only the cap. Clamped to 1s–6h.

## Sessions & parsing

- `TOKEN_DASHBOARD_SESSION_CACHE_TURNS` (default: `500000`) — turns the merged-session assembly cache may hold; budgeted in turns rather than sessions because one long session can outweigh a thousand short ones. Non-negative integer, `0` empties the cache and keeps it empty; empty or invalid values fall back to the default
- `TOKEN_DASHBOARD_SIG_TTL` (default: `5.0` seconds) — TTL of the source file-signature cache that avoids repeated glob/stat work when several requests arrive in a short window; float seconds, `0` disables the cache. The value must parse as a float — a non-numeric value fails at import. Sessions API transcript inventories refresh in the background after this TTL so cache hits do not walk source trees; `refresh=true` bypasses inventory reuse
- `TOKEN_DASHBOARD_INCLUDE_CODEX_GUARDIAN` (default: off) — default for the `include_review_sessions` API parameter when a request omits it; set to `1`, `true`, `yes`, or `on` to include Codex guardian/review subagent sessions in Codex session listings instead of hiding them. An explicit `include_review_sessions` query parameter always wins

## Persistent usage DB (default on)

TokenDashboard maintains a local SQLite index at `~/.token-dashboard/usage.sqlite3` by default.
It stores parsed token rows and Codex/Claude/Kimi/DeepSeek Harness/Reasonix
session summaries so repeated dashboard and API reads can use indexed SQL
instead of reparsing every source log. Source logs remain the source of truth;
the DB is a local performance index, and TokenDashboard falls back to live parsing if
it is disabled or unavailable.

Cached session rows are price-neutral: they hold each turn's billing inputs
(model, fresh input, cache reads and writes, output), and cost is calculated
when they are read, with whatever pricing that process has loaded. Editing a
rate therefore reprices instantly instead of rereading gigabytes of logs, and
two TokenDashboard versions sharing one database do not invalidate each other's rows
over pricing. Sharing is only safe while both builds support the same database
schema, though: schema migrations run forward only, so once the newer build
migrates the file the older one refuses to open it until it is upgraded, and
every cached read fails until the versions match. `token-dashboard doctor` reports the
schema on disk alongside the one the running build supports. Parser and
source-file changes still reparse normally. Rows written before this (including
any kept by `TOKEN_DASHBOARD_USAGE_DB_DURABLE` after their log is gone) reprice from
their stored totals, which reproduces the same figure but cannot separate a
Claude or Kimi cache write from fresh input; only a reparse of those logs can
restore that distinction. Codex bills under `provider/model` and stores the bare
name, so its older rows are reparsed once instead of reused.

Run a checkout against its own data directory so it never migrates the installed
service's database:

```bash
TOKEN_DASHBOARD_DATA_DIR=output/dev-data PYTHONPATH=src python3 main.py
```

- `TOKEN_DASHBOARD_USAGE_DB` (default: `1`) — set to `0`, `false`, `no`, or `off` to disable the persistent usage DB
- `TOKEN_DASHBOARD_DATA_DIR` (default: `~/.token-dashboard`) — base directory for TokenDashboard local state
- `TOKEN_DASHBOARD_USAGE_DB_PATH` (default: `$TOKEN_DASHBOARD_DATA_DIR/usage.sqlite3`) — explicit SQLite file path
- `TOKEN_DASHBOARD_USAGE_DB_DURABLE` (default: `1`) — keep already indexed rows if a source file temporarily disappears or a parser returns no rows; set to `0` for strict source replacement
- `TOKEN_DASHBOARD_USAGE_DB_WATCH` (default: `0`) — set to `1` to run a background sync loop inside `token-dashboard serve`
- `TOKEN_DASHBOARD_USAGE_DB_WATCH_INTERVAL` (default: `30` seconds) — sync interval for `token-dashboard db watch` and the serve-time watch loop

## Output speed cache

Output timing is built on demand in a separate `output_speed.sqlite3` beside the
configured usage database. Ordinary usage, Overview and core session reads do
not create it or start timing work. Model and session speed share one worker;
visible views observe its progress through events. Reader versions, native
DB/WAL identities and each file's usage-store state invalidate timing
independently of pricing, and a build reads only the inputs that changed.

- `TOKEN_DASHBOARD_SPEED_CPU_FRACTION` (default: `0.25`, clamped to `0.05`–`1`) — duty-cycle target applied between file inputs; a large file can use one CPU continuously before its balancing pause. Native projection and SQL publication use the one-CPU/niceness limits without this pause.
- `TOKEN_DASHBOARD_SPEED_MEMORY_MB` (default: `1024`) — worker address-space limit in MiB on platforms with resource limits. A failed build retains its last successful snapshot and reports the failure.
- `TOKEN_DASHBOARD_SPEED_SETTLE_SECONDS` (default: `300`) — how long a changed input must go unwritten before a model build reads it. Until then its last timing stands and the view stays ready, so a live session's newest responses are timed once it goes quiet. A manual refresh reads it at once; `0` reads every change immediately.

On Linux the worker also uses nice 10 and one available CPU. These controls apply
to the timing child, not the API process. The derived cache is disposable and
contains scalar measurements, verdicts and session memberships, rather than
transcripts. Primary schema migration removes old timing payloads and makes
their pages reusable; it does not automatically shrink the physical SQLite file.

Model/hour reads use a scalar covering index in the derived database. Existing
caches receive that index through the speed worker, rather than an HTTP request
or ordinary usage migration. Speed database connections allow a bounded 16 MiB
SQLite page cache. This memory and index storage are separate from Overview's
ordinary accounting path.

The timing worker records a process creation identity and probes Windows without
sending signals. Memory statistics are optional: lack of Unix `resource` support
does not fail extraction or publication, and macOS RSS units are normalized to MiB.
Failed input scopes do not suppress unrelated builds. Model/hour aggregate results
use a demand-only process cache (32 entries, 8 MiB encoded results), invalidated by
publication revision and filters; Overview does not import or access that cache.

## DB maintenance commands

```bash
token-dashboard db status --pretty
token-dashboard db sync --pretty
token-dashboard db verify --verify-period today --pretty
token-dashboard db repair --dry-run --pretty
token-dashboard db resync --pretty
token-dashboard db watch --pretty
```

What each command does:

- `token-dashboard db sync` — incrementally re-reads the source logs and indexes new or changed rows. This is the right first step when entries look missing.
- `token-dashboard db repair` — recomputes derived counters and checkpoints the WAL. It does **not** repair physical SQLite corruption; if SQLite's integrity check fails, run a full resync instead.
- `token-dashboard db resync` — rebuilds the database from the source logs. It can drop durable rows whose logs have since been pruned, so use it only when `db sync` cannot recover the missing entries.

## Quota polling

Quota snapshots live in the usage DB and feed the history charts; boundary
polling samples just before and after each fixed-reset window's reset so the
running-high consumption model sees the true pre-reset peak and post-reset
baseline. See [`QUOTA.md`](QUOTA.md) for the full quota polling design.

- `TOKEN_DASHBOARD_CLAUDE_PROFILES` — path-separated list of Claude Code config directories for quota; default `~/.claude` (or `$CLAUDE_CONFIG_DIR`) is always included and this only replaces the sibling `~/.claude*` scan (only quota reads it; usage and session parsing always scan `~/.claude*/projects`, so listing a directory does not add its usage); see [`QUOTA.md`](QUOTA.md) for details
- `TOKEN_DASHBOARD_QUOTA_POLL` — master kill switch (`0`) to disable all quota polling, scans, and writes; see [`QUOTA.md`](QUOTA.md) for details
- `TOKEN_DASHBOARD_QUOTA_POLL_INTERVAL` — background quota poll interval in seconds (floor `300`); see [`QUOTA.md`](QUOTA.md) for details
- `TOKEN_DASHBOARD_QUOTA_BOUNDARY_POLL` — toggle for sampling around quota reset boundaries (`0` to disable); see [`QUOTA.md`](QUOTA.md) for details
- `TOKEN_DASHBOARD_QUOTA_BOUNDARY_POST` — disable only the post-reset boundary sample (`0`); see [`QUOTA.md`](QUOTA.md) for details
- `TOKEN_DASHBOARD_QUOTA_BOUNDARY_PRE_SECONDS` — lead time in seconds before a reset boundary to sample quota; see [`QUOTA.md`](QUOTA.md) for details
- `TOKEN_DASHBOARD_QUOTA_BOUNDARY_POST_SECONDS` — delay in seconds after a reset boundary to sample quota; see [`QUOTA.md`](QUOTA.md) for details
- `TOKEN_DASHBOARD_QUOTA_RETENTION_DAYS` — days of quota history to keep before pruning (`0` keeps indefinitely); see [`QUOTA.md`](QUOTA.md) for details

## TUI & update checks

- `TOKEN_DASHBOARD_TUI_COLOR` (unset) — `always` (also `1`/`true`) keeps colour in `token-dashboard tui` even where the [`NO_COLOR`](https://no-color.org) convention has it off. The convention is written for line-oriented output, and a `NO_COLOR` exported into a shell or a tmux session greys a full-screen dashboard out with no way to tell it apart from an empty one; the TUI's own monochrome layout stays the default. The palette the terminal agreed to is on the top row as `colour: …`
- `TOKEN_DASHBOARD_TUI_NO_REMOTE` (unset) — set to any non-empty value and `token-dashboard tui` never probes the local server for a same-version `token-dashboard serve` to delegate read-only GETs to; every fetch then answers exactly as the in-process path does (fail-closed to in-process on any doubt regardless)
- `TOKEN_DASHBOARD_UPDATE_CHECK` — opt-in switch (`1`) or hard disable (`0`) for PyPI update checks; see [`ONBOARDING.md`](../guides/ONBOARDING.md) for details

Inside `tmux`, `COLORTERM` does not reach the pane, so the palette is capped at 256
colours and this dashboard's lavender, mint and navy round onto grey steps. `colour:
256 colours` in the top row is that case. tmux 3.2 and newer pass 24-bit through once
the client terminal is known to speak it:

```tmux
set -as terminal-features ',xterm*:truecolour'
```

`TEXTUAL_COLOR_SYSTEM` (`auto`) is the underlying override — `truecolor`, `256` or
`standard` — and is honoured as given, including when it says less than the terminal
could.
