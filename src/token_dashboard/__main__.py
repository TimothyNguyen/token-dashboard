"""Collision-proof runtime entrypoint: ``python -m token_dashboard ...``.

The pip console script and (future) npm bin both expose a bare ``token_dashboard`` on
PATH, so anything launching the runtime — the setup engine, a managed service's
ExecStart, a bundling parent — should invoke ``<python> -m token_dashboard`` instead of
the bare name. This module makes that work by delegating to the CLI.
"""

from __future__ import annotations

from .cli import main

if __name__ == "__main__":
    main()
