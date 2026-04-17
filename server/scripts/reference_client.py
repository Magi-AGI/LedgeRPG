"""Reference client: runs a hardcoded action sequence against a running LedgeRPG server
and prints the trace stream. Earliest "the contract works" signal before MAGUS+HERMES plug in."""

from __future__ import annotations

import argparse
import json
import urllib.request


def post(base: str, path: str, body: dict) -> dict:
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        base + path,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode("utf-8"))


def get(base: str, path: str) -> dict:
    with urllib.request.urlopen(base + path) as resp:
        return json.loads(resp.read().decode("utf-8"))


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--seed", type=int, default=42)
    args = ap.parse_args()
    base = f"http://{args.host}:{args.port}"

    start = post(base, "/episode/start", {"seed": args.seed})
    episode_id = start["episode_id"]
    print(json.dumps({"event": "start", **start}, sort_keys=True, indent=2))

    actions = ["move-N", "move-NE", "examine", "move-SE", "rest"]
    for action_name in actions:
        resp = post(base, "/episode/step", {
            "episode_id": episode_id,
            "action": {"name": action_name},
        })
        print(json.dumps({"event": "step", **resp}, sort_keys=True, indent=2))
        if resp.get("done"):
            break

    end = post(base, "/episode/end", {"episode_id": episode_id})
    print(json.dumps({"event": "end", **end}, sort_keys=True, indent=2))


if __name__ == "__main__":
    main()
