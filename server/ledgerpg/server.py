"""HTTP server for LedgeRPG MVP. stdlib http.server only — no external web framework."""

from __future__ import annotations

import argparse
import json
import logging
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

from .determinism import canonical_dumps
from .trace import build_full_state, build_trace
from .world import InvalidAction, World

DEFAULT_PORT = 8765
DEFAULT_SEED = 42


class Episode:
    __slots__ = ("episode_id", "world", "trace_count")

    def __init__(self, episode_id: str, world: World) -> None:
        self.episode_id = episode_id
        self.world = world
        self.trace_count = 0


class ServerState:
    def __init__(self) -> None:
        self.episodes: dict[str, Episode] = {}


def make_handler(state: ServerState) -> type[BaseHTTPRequestHandler]:
    class Handler(BaseHTTPRequestHandler):
        # Silence default access log; clients drive logging volume via --log-level.
        def log_message(self, format: str, *args) -> None:  # noqa: A002 (stdlib signature)
            logging.debug("%s - %s", self.address_string(), format % args)

        def _write_json(self, obj: dict, status: int = 200) -> None:
            body = canonical_dumps(obj).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def _read_json(self) -> dict:
            n = int(self.headers.get("Content-Length", "0"))
            if not n:
                return {}
            raw = self.rfile.read(n)
            return json.loads(raw.decode("utf-8"))

        def do_POST(self) -> None:  # noqa: N802 (stdlib signature)
            path = urlparse(self.path).path
            try:
                if path == "/episode/start":
                    self._do_start()
                elif path == "/episode/step":
                    self._do_step()
                elif path == "/episode/end":
                    self._do_end()
                else:
                    self._write_json({"error": "not_found", "path": path}, 404)
            except InvalidAction as e:
                self._write_json({"error": "invalid_action", "detail": str(e)}, 400)
            except (KeyError, ValueError, TypeError) as e:
                self._write_json({"error": "bad_request", "detail": str(e)}, 400)
            except Exception as e:
                logging.exception("server error on %s", path)
                self._write_json({"error": "internal", "detail": str(e)}, 500)

        def do_GET(self) -> None:  # noqa: N802
            parsed = urlparse(self.path)
            if parsed.path != "/episode/state":
                self._write_json({"error": "not_found", "path": parsed.path}, 404)
                return
            params = parse_qs(parsed.query)
            ep_id = (params.get("episode_id") or [None])[0]
            ep = state.episodes.get(ep_id) if ep_id else None
            if ep is None:
                self._write_json({"error": "not_found", "episode_id": ep_id}, 404)
                return
            self._write_json({
                "state": build_full_state(ep.world),
                "goals": ep.world.goals(),
                "done": ep.world.done,
            })

        def _do_start(self) -> None:
            data = self._read_json()
            seed = int(data.get("seed", DEFAULT_SEED))
            grid_size = int(data.get("grid_size", 8))
            step_limit = int(data.get("step_limit", 100))
            food_count = int(data.get("food_count", 5))
            obstacle_count = int(data.get("obstacle_count", 8))
            world = World(
                seed=seed,
                grid_size=grid_size,
                food_count=food_count,
                obstacle_count=obstacle_count,
                step_limit=step_limit,
            )
            episode_id = f"ep-{seed:06d}"
            state.episodes[episode_id] = Episode(episode_id, world)
            self._write_json({
                "episode_id": episode_id,
                "state": build_full_state(world),
                "goals": world.goals(),
            })

        def _do_step(self) -> None:
            data = self._read_json()
            episode_id = data["episode_id"]
            ep = state.episodes.get(episode_id)
            if ep is None:
                self._write_json({"error": "not_found", "episode_id": episode_id}, 404)
                return
            action = data["action"]
            action_name = action["name"]
            action_args = action.get("args", {})
            deltas = ep.world.apply_action(action_name)
            ep.trace_count += 1
            trace = build_trace(ep.episode_id, ep.world, action_name, action_args, deltas)
            self._write_json({
                "trace": trace,
                "done": ep.world.done,
                "success": ep.world.success if ep.world.done else False,
                "terminal_reason": ep.world.terminal_reason,
            })

        def _do_end(self) -> None:
            data = self._read_json()
            episode_id = data["episode_id"]
            ep = state.episodes.pop(episode_id, None)
            if ep is None:
                self._write_json({"error": "not_found", "episode_id": episode_id}, 404)
                return
            self._write_json({"ok": True, "final_trace_count": ep.trace_count})

    return Handler


def build_server(port: int = DEFAULT_PORT, host: str = "127.0.0.1") -> tuple[ThreadingHTTPServer, ServerState]:
    state = ServerState()
    httpd = ThreadingHTTPServer((host, port), make_handler(state))
    return httpd, state


def run(port: int = DEFAULT_PORT, host: str = "127.0.0.1") -> None:
    httpd, _ = build_server(port=port, host=host)
    logging.info("LedgeRPG server listening on %s:%s", host, port)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        httpd.server_close()


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(prog="ledgerpg.server")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    parser.add_argument("--host", default="127.0.0.1")
    # --seed is accepted for spec compatibility; seed is always supplied per-episode
    # via /episode/start, so this flag is informational only.
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args(argv)
    logging.basicConfig(level=args.log_level, format="%(levelname)s %(message)s")
    run(port=args.port, host=args.host)


if __name__ == "__main__":
    main()
