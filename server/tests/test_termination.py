"""All three terminal_reason enums must fire correctly."""

from __future__ import annotations

from ledgerpg.determinism import DIRECTIONS
from ledgerpg.world import (
    TERMINAL_ENERGY_DEPLETED,
    TERMINAL_STEP_LIMIT,
    TERMINAL_TARGET_REACHED,
    TILE_FOOD,
    World,
)


def _food_tiles(world: World) -> list[tuple[int, int]]:
    return sorted(c for c, t in world.grid.items() if t == TILE_FOOD)


def test_target_reached_on_all_food_consumed():
    world = World(seed=42, food_count=2, obstacle_count=0)
    # Teleport + examine each food tile deterministically (rules path stays honest for energy/step).
    for coord in _food_tiles(world):
        world.agent_pos = coord
        world.apply_action("examine")
    assert world.done is True
    assert world.success is True
    assert world.terminal_reason == TERMINAL_TARGET_REACHED


def test_step_limit_fires_when_agent_stalls():
    world = World(seed=7, food_count=5, obstacle_count=8, step_limit=3)
    for _ in range(3):
        world.apply_action("rest")
    assert world.done is True
    assert world.success is False
    assert world.terminal_reason == TERMINAL_STEP_LIMIT


def test_energy_depleted_from_repeated_moves():
    world = World(seed=11, food_count=5, obstacle_count=0, step_limit=1000)
    # Keep moving until energy hits zero; try each direction in order until one succeeds.
    safety = 2000
    directions = list(DIRECTIONS.keys())
    while not world.done and safety > 0:
        safety -= 1
        for d in directions:
            name = f"move-{d}"
            prev_energy = world.energy
            world.apply_action(name)
            if world.energy < prev_energy or world.done:
                break
    assert world.done is True
    assert world.success is False
    assert world.terminal_reason == TERMINAL_ENERGY_DEPLETED
