"""Action validation and blocked-move signaling."""

from __future__ import annotations

import pytest

from ledgerpg.world import InvalidAction, TILE_OBSTACLE, World


def test_invalid_action_name_rejected():
    world = World(seed=42)
    with pytest.raises(InvalidAction):
        world.apply_action("teleport")


def test_blocked_out_of_bounds_emits_movement_blocked():
    world = World(seed=42, obstacle_count=0, food_count=1)
    world.agent_pos = (0, 0)  # NW (-1, 0) and N (0, -1) both leave the grid
    deltas = world.apply_action("move-NW")
    kinds = [d["kind"] for d in deltas]
    assert "movement-blocked" in kinds
    # Blocked move still emits a position delta with from == to so consumers see intent-to-move.
    position = next(d for d in deltas if d["kind"] == "position")
    assert position["from"] == position["to"] == [0, 0]


def test_blocked_by_obstacle_emits_movement_blocked():
    world = World(seed=42)
    # Place agent adjacent to an obstacle we choose, then move toward it.
    obstacle_coord = next(c for c, t in world.grid.items() if t == TILE_OBSTACLE)
    oq, orr = obstacle_coord
    # Drop agent at a neighbor of the obstacle using direction N (dq=0, dr=-1)
    # means obstacle at (oq, orr) would be reached from (oq, orr+1) via move-N.
    world.agent_pos = (oq, orr + 1)
    # If out of bounds, fall back to any in-bounds neighbor.
    if not world.in_bounds(*world.agent_pos):
        world.agent_pos = (oq, orr - 1)
        direction = "move-S"
    else:
        direction = "move-N"
    deltas = world.apply_action(direction)
    kinds = [d["kind"] for d in deltas]
    assert "movement-blocked" in kinds


def test_examine_on_food_emits_food_consumed_and_energy_restore():
    world = World(seed=42, food_count=1, obstacle_count=0)
    food_coord = next(c for c, t in world.grid.items() if t == "food")
    world.agent_pos = food_coord
    world.energy = 0.5
    deltas = world.apply_action("examine")
    kinds = [d["kind"] for d in deltas]
    assert "food-consumed" in kinds
    assert "energy" in kinds
    assert world.energy == 1.0
    assert world.food_remaining == 0


def test_rest_emits_no_deltas_but_advances_step():
    world = World(seed=42)
    start_step = world.step
    deltas = world.apply_action("rest")
    assert deltas == []
    assert world.step == start_step + 1
