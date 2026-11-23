#!/usr/bin/env python3
"""TokenDashboard source entrypoint.

Kept for convenience/backwards-compat:
- `python3 main.py --bind 0.0.0.0 --port 55424`

The packaged entrypoint will be:
- `token_dashboard serve --bind 0.0.0.0 --port 55424`
"""

from __future__ import annotations

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO_ROOT / "src"))

from token_dashboard.cli import cli  # noqa: E402


def main() -> None:
    raise SystemExit(cli(["serve", *sys.argv[1:]], prog="python3 main.py"))


if __name__ == "__main__":
    main()
