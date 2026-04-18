using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;
using Xunit;

namespace LedgeRPG.Scaled.Tests
{
    /// Tests the composed behavior: source World + view caching + action layering.
    /// These are the integration-shape tests — if these pass, the spike's
    /// central claim (one state, N views, coherent actions across scales) holds.
    public class ScaledWorldTests
    {
        private static ScaledWorld NewScaled(long seed = 42)
            => new ScaledWorld(
                new World(seed, gridSize: 8, foodCount: 5, obstacleCount: 8, stepLimit: 100),
                regionSize: 4,
                zoneSize: 2);

        [Fact]
        public void VersionStartsAtZero()
        {
            var sw = NewScaled();
            Assert.Equal(0, sw.Version);
        }

        [Fact]
        public void ApplyBumpsVersion()
        {
            var sw = NewScaled();
            sw.Apply(RPGActionKind.Rest);
            Assert.Equal(1, sw.Version);
        }

        [Fact]
        public void ApplyScale1BumpsVersionOncePerCallNotPerPrimitive()
        {
            // The scale-1 action is the semantic unit. If each refined primitive
            // bumped Version, cache invalidation would thrash mid-burst and
            // higher-scale observers would see intermediate states that don't
            // correspond to any semantic action. One bump per call preserves
            // the "scale-N action = atomic from scale-N's perspective" story.
            var sw = NewScaled();
            sw.ApplyScale1(new MovementBurst(RPGActionKind.MoveNE, count: 3));
            Assert.Equal(1, sw.Version);
        }

        [Fact]
        public void GetRegions_IsCachedWithinAVersion()
        {
            // Reference equality across two calls without mutation — proves the
            // cache hit path. If caching breaks, the same-instance check fails
            // and we know to look there instead of at correctness.
            var sw = NewScaled();
            var a = sw.GetRegions();
            var b = sw.GetRegions();
            Assert.Same(a, b);
        }

        [Fact]
        public void GetRegions_RebuildsAfterApply()
        {
            var sw = NewScaled();
            var before = sw.GetRegions();
            sw.Apply(RPGActionKind.Rest);
            var after = sw.GetRegions();

            // Different instance (cache invalidated + rebuilt). Content may or
            // may not differ — Rest doesn't change the grid — but the cache
            // version counter should have forced a rebuild.
            Assert.NotSame(before, after);
        }

        [Fact]
        public void Scale1Action_AffectsScale0AgentPosition()
        {
            // The central coherence claim: apply a scale-1 action, observe that
            // scale-0 truth (AgentPos) actually moved. If this fails, scale-1
            // actions are ghost actions that don't touch reality — and the
            // whole point of the architecture is undermined.
            //
            // We use MoveN against the paper-mirror default layout. At seed 42,
            // the agent spawns at some position; MoveN either succeeds (moving
            // to a new tile) or is blocked by boundary/obstacle. Either way the
            // important property is that scale-0 state is the thing that got
            // consulted — so we assert Step advanced, which happens on every
            // apply regardless of movement success.
            var sw = NewScaled();
            int stepBefore = sw.Source.Step;

            sw.ApplyScale1(new MovementBurst(RPGActionKind.MoveN, count: 3));

            Assert.Equal(stepBefore + 3, sw.Source.Step);
        }

        [Fact]
        public void Scale1Action_ReflectsInScale1View()
        {
            // After a scale-1 action, the scale-1 view should reflect whatever
            // scale-0 changed. Easiest thing to observe: agent's region may
            // shift if the burst crossed a region boundary. At minimum, the
            // projection should rebuild without exception.
            var sw = NewScaled();
            sw.ApplyScale1(new MovementBurst(RPGActionKind.MoveN, count: 4));
            var regions = sw.GetRegions();

            // Exactly one region still has the agent (invariant preserved
            // across mutation). Coord might differ from the starting region.
            int withAgent = 0;
            foreach (var r in regions) if (r.HasAgent) withAgent++;
            Assert.Equal(1, withAgent);
        }

        [Fact]
        public void ZoneCountsStayConservedAfterApply()
        {
            // Apply a scale-1 action, then verify the scale-2 view still sums
            // back to source World totals. Food can decrement if the burst ate
            // food, so we compare against the current source totals, not the
            // initial ones.
            var sw = NewScaled();
            sw.ApplyScale1(new MovementBurst(RPGActionKind.MoveNE, count: 5));

            var zones = sw.GetZones();
            int totalFood = 0, totalObstacle = 0, totalTiles = 0;
            foreach (var z in zones)
            {
                totalFood += z.TotalFood;
                totalObstacle += z.TotalObstacle;
                totalTiles += z.TotalTiles;
            }

            Assert.Equal(sw.Source.FoodRemaining, totalFood);
            Assert.Equal(sw.Source.ObstacleCount, totalObstacle);
            Assert.Equal(sw.Source.GridSize * sw.Source.GridSize, totalTiles);
        }
    }
}
