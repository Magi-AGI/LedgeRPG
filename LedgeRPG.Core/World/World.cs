using System;
using System.Collections.Generic;
using LedgeRPG.Core.Determinism;

namespace LedgeRPG.Core.World
{
    /// Authoritative state for one LedgeRPG episode. Mutable — ApplyAction
    /// advances the state in place and returns the deltas produced. Matches
    /// the Python paper server's World class one-to-one in rules, with C#
    /// types swapped in (HexCoord readonly struct, StateDelta records, enums
    /// for TileType / TerminalReason / RPGActionKind). The adapter (M2)
    /// handles copy-on-apply semantics for the IRulesAdapter contract; Core
    /// stays mutate-in-place for a faithful port.
    public sealed class World
    {
        public const double MoveEnergyCost = 0.05;

        // Seed is long rather than int so it matches MagiGameServer.Contracts.GameConfig.Seed
        // directly — the adapter can hand it through without narrowing, and 64 bits of seed
        // space avoids accidental collisions in large session fleets.
        public long Seed { get; }
        public int GridSize { get; }
        public int FoodCount { get; }
        public int ObstacleCount { get; }
        public int StepLimit { get; }

        public int Step { get; private set; }
        public double Energy { get; private set; }
        public bool Done { get; private set; }
        public bool Success { get; private set; }
        public TerminalReason TerminalReason { get; private set; }

        public HexCoord AgentPos { get; private set; }
        public int FoodRemaining { get; private set; }
        public int TotalPassable { get; }

        private readonly Dictionary<HexCoord, TileType> _grid;
        private readonly HashSet<HexCoord> _visited;

        public World(long seed, int gridSize = 8, int foodCount = 5, int obstacleCount = 8, int stepLimit = 100)
        {
            if (gridSize <= 0) throw new ArgumentOutOfRangeException(nameof(gridSize));
            if (foodCount < 0) throw new ArgumentOutOfRangeException(nameof(foodCount));
            if (obstacleCount < 0) throw new ArgumentOutOfRangeException(nameof(obstacleCount));
            if (stepLimit <= 0) throw new ArgumentOutOfRangeException(nameof(stepLimit));

            int capacity = gridSize * gridSize;
            if (foodCount + obstacleCount + 1 > capacity)
                throw new ArgumentException(
                    "food + obstacle + agent placements exceed grid capacity");

            Seed = seed;
            GridSize = gridSize;
            FoodCount = foodCount;
            ObstacleCount = obstacleCount;
            StepLimit = stepLimit;

            Energy = 1.0;
            Done = false;
            Success = false;
            TerminalReason = TerminalReason.None;

            // Deterministic world gen: enumerate coords in canonical (Q, R)
            // order, seeded-shuffle the array, then assign obstacles, food,
            // and the agent position by taking slots off the front. Matches
            // the Python world-gen sequence structurally — see SplitMix64Rng
            // comment re: why the per-seed output differs from Python's MT.
            var rng = new SplitMix64Rng(seed);
            var coords = new HexCoord[capacity];
            int idx = 0;
            for (int q = 0; q < gridSize; q++)
                for (int r = 0; r < gridSize; r++)
                    coords[idx++] = new HexCoord(q, r);
            Array.Sort(coords);
            rng.Shuffle(coords);

            _grid = new Dictionary<HexCoord, TileType>(capacity);
            foreach (var c in coords) _grid[c] = TileType.Empty;

            int cursor = 0;
            for (int i = 0; i < obstacleCount; i++)
                _grid[coords[cursor++]] = TileType.Obstacle;
            for (int i = 0; i < foodCount; i++)
                _grid[coords[cursor++]] = TileType.Food;
            AgentPos = coords[cursor];

            FoodRemaining = foodCount;
            _visited = new HashSet<HexCoord> { AgentPos };
            TotalPassable = capacity - obstacleCount;
        }

        // Deep-copy constructor backing Clone(). Private because external
        // callers go through Clone() — that gives us one well-named entry
        // point for "I want a snapshot-advance-without-mutating" flow without
        // surfacing a second constructor that could be mistaken for a
        // full-state rehydration API (which intentionally doesn't exist —
        // the canonical construction path runs world gen from a seed).
        private World(World source)
        {
            Seed = source.Seed;
            GridSize = source.GridSize;
            FoodCount = source.FoodCount;
            ObstacleCount = source.ObstacleCount;
            StepLimit = source.StepLimit;
            Step = source.Step;
            Energy = source.Energy;
            Done = source.Done;
            Success = source.Success;
            TerminalReason = source.TerminalReason;
            AgentPos = source.AgentPos;
            FoodRemaining = source.FoodRemaining;
            TotalPassable = source.TotalPassable;
            _grid = new Dictionary<HexCoord, TileType>(source._grid);
            _visited = new HashSet<HexCoord>(source._visited);
        }

        /// Deep-copies the world so the original can serve as an immutable
        /// snapshot while the copy advances under a new action. Backs the
        /// M2 adapter's copy-on-apply semantics for the IRulesAdapter contract.
        public World Clone() => new World(this);

        public bool InBounds(HexCoord c)
            => c.Q >= 0 && c.Q < GridSize && c.R >= 0 && c.R < GridSize;

        public TileType TileAt(HexCoord c) => _grid[c];

        public int VisitedCount => _visited.Count;

        /// Exploration incentive and energy regulation, matching the Python
        /// world.goals() shape. Returned as a struct so the adapter can
        /// translate to the wire dict without tying Core to a JSON library.
        public Goals Goals => new Goals(
            explorationIncentive: (double)_visited.Count / TotalPassable,
            energyRegulation: Energy);

        /// Snapshot of the grid as a sorted (Q, R) sequence — for the
        /// /episode/state endpoint and for state-hash computation. Returns
        /// new entries; the caller may not mutate the underlying grid.
        public IReadOnlyList<GridCell> GridSnapshot()
        {
            var cells = new List<GridCell>(_grid.Count);
            var keys = new HexCoord[_grid.Count];
            _grid.Keys.CopyTo(keys, 0);
            Array.Sort(keys);
            foreach (var k in keys)
                cells.Add(new GridCell(k, _grid[k]));
            return cells;
        }

        public IReadOnlyList<StateDelta> ApplyAction(RPGActionKind action)
        {
            // Mirrors the Python "if action_name not in ACTION_NAMES: raise" guard.
            // C# enums accept any underlying integer via cast, so treat unknown
            // enum values as invalid rather than silently falling through to Rest.
            if (!Enum.IsDefined(typeof(RPGActionKind), action))
                throw new InvalidActionException($"unknown action: {(int)action}");

            if (Done)
                throw new InvalidActionException(
                    $"episode already terminated ({TerminalReason})");

            Step++;

            List<StateDelta> deltas;
            if (RPGActions.IsMove(action))
            {
                deltas = ApplyMove(RPGActions.ToDirection(action));
            }
            else if (action == RPGActionKind.Examine)
            {
                deltas = ApplyExamine();
            }
            else // Rest: step advances, nothing else changes.
            {
                deltas = new List<StateDelta>();
            }

            CheckTermination();
            return deltas;
        }

        private List<StateDelta> ApplyMove(Direction direction)
        {
            var offset = Directions.Offset(direction);
            var oldPos = AgentPos;
            var newPos = oldPos.Translate(offset);

            if (!InBounds(newPos) || TileAt(newPos) == TileType.Obstacle)
            {
                // Blocked: emit both the movement-blocked signal AND a no-op
                // position delta (from == to). Clients that only listen on
                // position updates still see the failed attempt; clients that
                // react to blocked moves get the richer signal. Energy cost
                // is NOT charged on blocked moves — matches Python exactly.
                return new List<StateDelta>
                {
                    new MovementBlockedDelta(Directions.ToWireName(direction), oldPos),
                    new PositionDelta(oldPos, oldPos)
                };
            }

            var deltas = new List<StateDelta>
            {
                new PositionDelta(oldPos, newPos)
            };

            double oldEnergy = Energy;
            Energy = Math.Max(0.0, Energy - MoveEnergyCost);
            deltas.Add(new EnergyDelta(-MoveEnergyCost, oldEnergy, Energy));

            AgentPos = newPos;
            if (_visited.Add(newPos))
                deltas.Add(new TileDiscoveredDelta(newPos));

            return deltas;
        }

        private List<StateDelta> ApplyExamine()
        {
            var tile = TileAt(AgentPos);
            if (tile != TileType.Food)
                return new List<StateDelta>();

            double oldEnergy = Energy;
            Energy = 1.0;
            _grid[AgentPos] = TileType.Empty;
            FoodRemaining--;

            return new List<StateDelta>
            {
                new FoodConsumedDelta(AgentPos),
                new EnergyDelta(1.0 - oldEnergy, oldEnergy, 1.0)
            };
        }

        private void CheckTermination()
        {
            // Priority order matches the Python server exactly:
            //   1. target_reached  — all food consumed (the only success path)
            //   2. energy_depleted — energy hit zero; strict > step_limit so a
            //      zero-energy state on the final step reports depletion, not
            //      a timeout, which is the more informative failure mode
            //   3. step_limit      — hit the step cap without success
            if (FoodRemaining == 0)
            {
                Done = true;
                Success = true;
                TerminalReason = TerminalReason.TargetReached;
            }
            else if (Energy <= 0.0)
            {
                Done = true;
                Success = false;
                TerminalReason = TerminalReason.EnergyDepleted;
            }
            else if (Step >= StepLimit)
            {
                Done = true;
                Success = false;
                TerminalReason = TerminalReason.StepLimit;
            }
        }
    }

    public readonly struct GridCell
    {
        public HexCoord Coord { get; }
        public TileType Type { get; }
        public GridCell(HexCoord coord, TileType type) { Coord = coord; Type = type; }
    }

    public readonly struct Goals
    {
        public double ExplorationIncentive { get; }
        public double EnergyRegulation { get; }
        public Goals(double explorationIncentive, double energyRegulation)
        {
            ExplorationIncentive = explorationIncentive;
            EnergyRegulation = energyRegulation;
        }
    }
}
