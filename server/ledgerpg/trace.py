"""Trace and state payload builders. Matches HERMES/docs/ledgerpg-server-mvp.md shapes."""

from __future__ import annotations

from .determinism import ACTION_NAMES
from .world import World


def build_trace(
    episode_id: str,
    world: World,
    action_name: str,
    action_args: dict,
    deltas: list[dict],
) -> dict:
    q, r = world.agent_pos
    return {
        "episode_id": episode_id,
        "t": world.step,
        "action": {"name": action_name, "args": action_args},
        "valid_actions": list(ACTION_NAMES),
        "state": {
            "agent": {"q": q, "r": r, "energy": world.energy},
            "tile": {"type": world.tile_at(q, r)},
            "visited_count": len(world.visited),
            "food_remaining": world.food_remaining,
        },
        "state_delta": deltas,
        "goals": world.goals(),
        "context": {"scenario": "LedgeRPG-MVP", "seed": world.seed},
    }


def build_full_state(world: World) -> dict:
    q, r = world.agent_pos
    return {
        "agent_position": [q, r],
        "energy": world.energy,
        "visited_count": len(world.visited),
        "step": world.step,
        "last_tile_type": world.tile_at(q, r),
        "grid": [{"q": cq, "r": cr, "type": t} for (cq, cr), t in sorted(world.grid.items())],
    }
