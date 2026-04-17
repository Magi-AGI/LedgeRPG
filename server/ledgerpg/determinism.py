"""Determinism primitives: seeded RNG, canonical JSON, direction tables, action names."""

from __future__ import annotations

import json
import random

DIRECTIONS: dict[str, tuple[int, int]] = {
    "N":  (0, -1),
    "NE": (1, -1),
    "SE": (1, 0),
    "S":  (0, 1),
    "SW": (-1, 1),
    "NW": (-1, 0),
}

MOVE_ACTIONS: tuple[str, ...] = tuple(f"move-{d}" for d in DIRECTIONS)
ACTION_NAMES: tuple[str, ...] = MOVE_ACTIONS + ("examine", "rest")


def canonical_dumps(obj) -> str:
    return json.dumps(
        obj,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=False,
        allow_nan=False,
    )


def make_rng(seed: int) -> random.Random:
    return random.Random(seed)
