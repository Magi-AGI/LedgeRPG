using System.Linq;
using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;
using Xunit;

namespace LedgeRPG.Core.Tests
{
    public class ActionTests
    {
        [Fact]
        public void UnknownActionEnumValueThrows()
        {
            var world = new World.World(seed: 42);
            // Cast to simulate a bad wire value that slipped past the parser.
            var bogus = (RPGActionKind)999;
            Assert.Throws<InvalidActionException>(() => world.ApplyAction(bogus));
        }

        [Fact]
        public void ActionsAfterTerminationThrow()
        {
            var world = new World.World(seed: 42, stepLimit: 2);
            world.ApplyAction(RPGActionKind.Rest);
            world.ApplyAction(RPGActionKind.Rest);
            Assert.True(world.Done);
            Assert.Throws<InvalidActionException>(() => world.ApplyAction(RPGActionKind.Rest));
        }

        [Fact]
        public void BlockedMoveEmitsBothDeltaKinds()
        {
            // Place the agent at (0,0) on an obstacle-free tiny world so we can
            // force a specific edge-of-grid blocked move. We scan for a seed
            // whose starting agent position lets us attempt MoveNW off the
            // grid's top-left corner.
            for (int seed = 0; seed < 200; seed++)
            {
                var world = new World.World(seed, gridSize: 4, foodCount: 0, obstacleCount: 0, stepLimit: 50);
                if (world.AgentPos.Q != 0 || world.AgentPos.R != 0) continue;

                double energyBefore = world.Energy;
                var deltas = world.ApplyAction(RPGActionKind.MoveNW);

                Assert.Equal(2, deltas.Count);
                var blocked = Assert.IsType<MovementBlockedDelta>(deltas[0]);
                Assert.Equal("NW", blocked.Direction);
                Assert.Equal(world.AgentPos, blocked.At);

                var pos = Assert.IsType<PositionDelta>(deltas[1]);
                Assert.Equal(pos.From, pos.To);

                // No energy delta, no energy charge.
                Assert.DoesNotContain(deltas, d => d is EnergyDelta);
                Assert.Equal(energyBefore, world.Energy);
                return;
            }
            Assert.Fail("no seed produced agent at (0,0) — test needs broader search");
        }

        [Fact]
        public void SuccessfulMoveChargesEnergyAndRecordsDiscovery()
        {
            // Find a seed whose agent starts far from the edges and has an open
            // NE neighbour (not an obstacle, not off-grid).
            for (int seed = 0; seed < 200; seed++)
            {
                var world = new World.World(seed, gridSize: 6, foodCount: 2, obstacleCount: 4, stepLimit: 50);
                var target = new HexCoord(world.AgentPos.Q + 1, world.AgentPos.R - 1);
                if (!world.InBounds(target) || world.TileAt(target) == TileType.Obstacle) continue;

                double energyBefore = world.Energy;
                var deltas = world.ApplyAction(RPGActionKind.MoveNE);

                var pos = Assert.IsType<PositionDelta>(deltas[0]);
                Assert.Equal(target, pos.To);

                var energy = deltas.OfType<EnergyDelta>().Single();
                Assert.Equal(-World.World.MoveEnergyCost, energy.Delta, precision: 10);
                Assert.Equal(energyBefore, energy.From, precision: 10);
                Assert.Equal(energyBefore - World.World.MoveEnergyCost, energy.To, precision: 10);

                // The starting tile was already visited, so the new tile must
                // emit tile-discovered.
                Assert.Contains(deltas, d => d is TileDiscoveredDelta td && td.At == target);
                return;
            }
            Assert.Fail("no seed produced an agent with an open NE neighbour — broaden the search");
        }

        [Fact]
        public void ExamineOnNonFoodTileIsNoOp()
        {
            // After a Rest the agent sits on whatever its spawn tile is — which
            // is whatever tile was at cursor after obstacle+food placement, so
            // it's not food. Examine there should produce zero deltas but still
            // advance the step counter.
            var world = new World.World(seed: 42);
            int stepBefore = world.Step;
            double energyBefore = world.Energy;

            var deltas = world.ApplyAction(RPGActionKind.Examine);

            Assert.Empty(deltas);
            Assert.Equal(stepBefore + 1, world.Step);
            Assert.Equal(energyBefore, world.Energy);
        }
    }
}
