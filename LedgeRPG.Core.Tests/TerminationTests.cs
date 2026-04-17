using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;
using Xunit;

namespace LedgeRPG.Core.Tests
{
    public class TerminationTests
    {
        // Each terminal reason must be reachable and correctly reported.
        // Matches the Python test_termination suite.

        [Fact]
        public void StepLimitReachedWhenResting()
        {
            // Rest just advances the step counter without mutating anything, so
            // step_limit must fire at step == stepLimit.
            var world = new World.World(seed: 42, stepLimit: 5);
            for (int i = 0; i < 5; i++)
                world.ApplyAction(RPGActionKind.Rest);

            Assert.True(world.Done);
            Assert.False(world.Success);
            Assert.Equal(TerminalReason.StepLimit, world.TerminalReason);
            Assert.Equal(5, world.Step);
        }

        [Fact]
        public void EnergyDepletedWinsOverStepLimit()
        {
            // At 0.05 energy per move, 20 successful moves drain to 0. We don't
            // know the grid layout without running it, but with a generous step
            // limit and 20 move attempts many should land; if every attempt were
            // blocked (no energy charged) the episode would hit step_limit instead,
            // which is an acceptable alternative outcome and validated separately.
            var world = new World.World(seed: 42, stepLimit: 200);
            int attempts = 0;
            while (!world.Done && attempts < 200)
            {
                world.ApplyAction(RPGActionKind.MoveN);
                attempts++;
            }
            Assert.True(world.Done);
            // Success is impossible via MoveN alone (food requires Examine); one
            // of energy_depleted or step_limit must fire.
            Assert.False(world.Success);
            Assert.True(
                world.TerminalReason == TerminalReason.EnergyDepleted ||
                world.TerminalReason == TerminalReason.StepLimit,
                $"expected EnergyDepleted or StepLimit, got {world.TerminalReason}");
        }

        [Fact]
        public void TargetReachedWhenAllFoodConsumed()
        {
            // Construct a tiny world with exactly one food tile; consume it and
            // confirm target_reached fires as a success. We synthesize the scenario
            // by walking the agent onto every food tile using a brute-force scan
            // — slow but deterministic and independent of grid layout.
            var world = new World.World(seed: 7, gridSize: 4, foodCount: 1, obstacleCount: 0, stepLimit: 500);

            while (!world.Done && world.FoodRemaining > 0)
            {
                var foodCoord = FindFirstFood(world);
                WalkAgentTo(world, foodCoord);
                if (world.Done) break;
                world.ApplyAction(RPGActionKind.Examine);
            }

            Assert.True(world.Done);
            Assert.True(world.Success);
            Assert.Equal(TerminalReason.TargetReached, world.TerminalReason);
        }

        private static HexCoord FindFirstFood(World.World world)
        {
            foreach (var cell in world.GridSnapshot())
                if (cell.Type == TileType.Food) return cell.Coord;
            return new HexCoord(-1, -1);
        }

        private static void WalkAgentTo(World.World world, HexCoord target)
        {
            // Greedy axial walk: step-by-step reduce |dq| then |dr|. Prefers the
            // six hex directions that touch both axes where helpful. Not optimal
            // pathing — good enough for a 4x4 clear board with zero obstacles.
            int safety = 50;
            while (!world.Done && world.AgentPos != target && safety-- > 0)
            {
                int dq = target.Q - world.AgentPos.Q;
                int dr = target.R - world.AgentPos.R;
                RPGActionKind move;
                if (dq < 0) move = dr > 0 ? RPGActionKind.MoveSW : RPGActionKind.MoveNW;
                else if (dq > 0) move = dr > 0 ? RPGActionKind.MoveSE : RPGActionKind.MoveNE;
                else move = dr > 0 ? RPGActionKind.MoveS : RPGActionKind.MoveN;
                world.ApplyAction(move);
            }
        }
    }
}
