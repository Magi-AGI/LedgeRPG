"""Two seed-42 episodes driven by the same action sequence must produce logically identical traces."""

from __future__ import annotations

from ledgerpg.determinism import canonical_dumps
from ledgerpg.trace import build_full_state, build_trace
from ledgerpg.world import World


def run_episode(seed: int, actions: list[str]) -> tuple[str, list[str]]:
    world = World(seed=seed)
    episode_id = f"ep-{seed:06d}"
    initial = canonical_dumps({
        "episode_id": episode_id,
        "state": build_full_state(world),
        "goals": world.goals(),
    })
    traces: list[str] = []
    for name in actions:
        if world.done:
            break
        deltas = world.apply_action(name)
        trace = build_trace(episode_id, world, name, {}, deltas)
        traces.append(canonical_dumps({
            "trace": trace,
            "done": world.done,
            "success": world.success if world.done else False,
            "terminal_reason": world.terminal_reason,
        }))
    return initial, traces


def test_two_seed42_episodes_identical():
    actions = ["move-N", "move-NE", "examine", "rest", "move-S",
               "move-SW", "move-NW", "examine", "move-SE", "rest"]
    init_a, traces_a = run_episode(42, actions)
    init_b, traces_b = run_episode(42, actions)
    assert init_a == init_b
    assert traces_a == traces_b


def test_different_seeds_produce_different_initial_state():
    a_init, _ = run_episode(42, [])
    b_init, _ = run_episode(43, [])
    assert a_init != b_init


def test_no_wallclock_fields_in_trace():
    _, traces = run_episode(42, ["move-N", "examine"])
    banned = ("timestamp", "wallclock", "server_pid", "wall_time", "real_time")
    for t in traces:
        for b in banned:
            assert b not in t, f"banned wallclock field {b!r} present in trace: {t}"
