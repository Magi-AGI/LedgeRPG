"""World model: 8x8 hex grid, three tile types, eight actions, deterministic gen."""

from __future__ import annotations

from typing import Optional

from .determinism import ACTION_NAMES, DIRECTIONS, MOVE_ACTIONS, make_rng

TILE_EMPTY = "empty"
TILE_FOOD = "food"
TILE_OBSTACLE = "obstacle"

MOVE_ENERGY_COST = 0.05

TERMINAL_TARGET_REACHED = "target_reached"
TERMINAL_STEP_LIMIT = "step_limit"
TERMINAL_ENERGY_DEPLETED = "energy_depleted"


class InvalidAction(Exception):
    """Raised when a requested action name is not in the eight-action set."""


class World:
    def __init__(
        self,
        seed: int,
        grid_size: int = 8,
        food_count: int = 5,
        obstacle_count: int = 8,
        step_limit: int = 100,
    ) -> None:
        self.seed = seed
        self.grid_size = grid_size
        self.food_count = food_count
        self.obstacle_count = obstacle_count
        self.step_limit = step_limit

        self.step = 0
        self.energy = 1.0
        self.done = False
        self.success = False
        self.terminal_reason: Optional[str] = None

        # Deterministic world gen: canonical coord order, seeded shuffle, sequential assignment.
        rng = make_rng(seed)
        coords = sorted((q, r) for q in range(grid_size) for r in range(grid_size))
        rng.shuffle(coords)

        self.grid: dict[tuple[int, int], str] = {c: TILE_EMPTY for c in coords}
        idx = 0
        for _ in range(obstacle_count):
            self.grid[coords[idx]] = TILE_OBSTACLE
            idx += 1
        for _ in range(food_count):
            self.grid[coords[idx]] = TILE_FOOD
            idx += 1
        self.agent_pos: tuple[int, int] = coords[idx]

        self.food_remaining = food_count
        self.visited: set[tuple[int, int]] = {self.agent_pos}
        self.total_passable = grid_size * grid_size - obstacle_count

    def in_bounds(self, q: int, r: int) -> bool:
        return 0 <= q < self.grid_size and 0 <= r < self.grid_size

    def tile_at(self, q: int, r: int) -> str:
        return self.grid[(q, r)]

    def goals(self) -> dict[str, float]:
        return {
            "EXPLORATION_INCENTIVE": len(self.visited) / self.total_passable,
            "ENERGY_REGULATION": self.energy,
        }

    def apply_action(self, action_name: str) -> list[dict]:
        """Apply an action; return its state_delta list. Advances step and sets terminal state."""
        if action_name not in ACTION_NAMES:
            raise InvalidAction(action_name)
        if self.done:
            raise InvalidAction(f"episode already terminated ({self.terminal_reason})")

        self.step += 1

        if action_name in MOVE_ACTIONS:
            direction = action_name.split("-", 1)[1]
            deltas = self._apply_move(direction)
        elif action_name == "examine":
            deltas = self._apply_examine()
        else:  # rest
            deltas = []

        self._check_termination()
        return deltas

    def _apply_move(self, direction: str) -> list[dict]:
        dq, dr = DIRECTIONS[direction]
        old_q, old_r = self.agent_pos
        new_q, new_r = old_q + dq, old_r + dr

        if not self.in_bounds(new_q, new_r) or self.tile_at(new_q, new_r) == TILE_OBSTACLE:
            return [
                {"kind": "movement-blocked", "direction": direction, "at": [old_q, old_r]},
                {"kind": "position", "from": [old_q, old_r], "to": [old_q, old_r]},
            ]

        deltas: list[dict] = [
            {"kind": "position", "from": [old_q, old_r], "to": [new_q, new_r]},
        ]
        old_e = self.energy
        self.energy = max(0.0, self.energy - MOVE_ENERGY_COST)
        deltas.append({"kind": "energy", "delta": -MOVE_ENERGY_COST, "from": old_e, "to": self.energy})
        self.agent_pos = (new_q, new_r)
        if self.agent_pos not in self.visited:
            self.visited.add(self.agent_pos)
            deltas.append({"kind": "tile-discovered", "at": [new_q, new_r]})
        return deltas

    def _apply_examine(self) -> list[dict]:
        q, r = self.agent_pos
        tile = self.tile_at(q, r)
        if tile != TILE_FOOD:
            return []
        old_e = self.energy
        self.energy = 1.0
        self.grid[(q, r)] = TILE_EMPTY
        self.food_remaining -= 1
        return [
            {"kind": "food-consumed", "at": [q, r]},
            {"kind": "energy", "delta": 1.0 - old_e, "from": old_e, "to": 1.0},
        ]

    def _check_termination(self) -> None:
        # Priority: success first, then energy failure, then step-limit failure.
        if self.food_remaining == 0:
            self.done = True
            self.success = True
            self.terminal_reason = TERMINAL_TARGET_REACHED
        elif self.energy <= 0.0:
            self.done = True
            self.success = False
            self.terminal_reason = TERMINAL_ENERGY_DEPLETED
        elif self.step >= self.step_limit:
            self.done = True
            self.success = False
            self.terminal_reason = TERMINAL_STEP_LIMIT
