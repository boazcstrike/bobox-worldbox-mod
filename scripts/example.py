"""Example BoBox client: print game status, then start a settler colony at the map center.

Run: uv run scripts/example.py
Port: set BOBOX_PORT if the mod was started with a custom one.
"""

import json
import os
import sys
import urllib.error
import urllib.request

BASE_URL = f"http://127.0.0.1:{os.environ.get('BOBOX_PORT', '8765')}"
TIMEOUT_SECONDS = 15


def call(method: str, path: str, payload: dict | None = None) -> dict:
    data = json.dumps(payload).encode() if payload is not None else None
    request = urllib.request.Request(
        BASE_URL + path,
        data=data,
        method=method,
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=TIMEOUT_SECONDS) as response:
            return json.load(response)
    except urllib.error.HTTPError as err:
        detail = err.read().decode(errors="replace")
        raise SystemExit(f"{method} {path} -> HTTP {err.code}: {detail}")
    except urllib.error.URLError as err:
        raise SystemExit(
            f"cannot reach {BASE_URL} ({err.reason}). Is WorldBox running with experimental mode on?"
        )
    except TimeoutError:
        raise SystemExit(f"{method} {path} timed out after {TIMEOUT_SECONDS}s. Is the game paused or busy?")


def main() -> None:
    status = call("GET", "/status")
    print("status:", json.dumps(status, indent=2))
    if not status.get("world_loaded"):
        sys.exit("no world loaded; open or create a world first")

    x = status["map_width"] // 2
    y = status["map_height"] // 2
    result = call("POST", "/scenario/settlers", {"asset": "human", "x": x, "y": y, "count": 5})
    print("settlers:", json.dumps(result, indent=2))

    colony = call("GET", "/colony")
    print("colony:", json.dumps(colony, indent=2))


if __name__ == "__main__":
    main()
