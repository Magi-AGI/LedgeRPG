using LedgeRPG.Core.World;
using Xunit;

namespace LedgeRPG.Scaled.Tests
{
    /// The load-bearing correctness tests: projection is deterministic and
    /// sum-conserving across scales. If these fail, the "one state with N
    /// views" claim is compromised and the whole spike is unsound.
    public class ProjectionTests
    {
        private static World NewPaperMirror(long seed = 42)
            => new World(seed: seed, gridSize: 8, foodCount: 5, obstacleCount: 8, stepLimit: 100);

        [Fact]
        public void Scale0To1_IsDeterministic()
        {
            var world = NewPaperMirror();
            var r1 = Projections.Scale0To1(world, regionSize: 4);
            var r2 = Projections.Scale0To1(world, regionSize: 4);

            Assert.Equal(r1.Count, r2.Count);
            for (int i = 0; i < r1.Count; i++)
                Assert.Equal(r1[i], r2[i]);
        }

        [Fact]
        public void Scale0To1_ConservesTileCount()
        {
            // Every scale-0 tile maps to exactly one region; the sum of per-region
            // TileCount must equal GridSize * GridSize. Guards against the "tile
            // counted in two regions" and "tile counted in zero regions" bugs.
            var world = NewPaperMirror();
            var regions = Projections.Scale0To1(world, regionSize: 4);

            int total = 0;
            foreach (var r in regions) total += r.TileCount;
            Assert.Equal(world.GridSize * world.GridSize, total);
        }

        [Fact]
        public void Scale0To1_ConservesFoodAndObstacleCounts()
        {
            // Sum-of-aggregates equals canonical World counts. This is THE
            // projection-correctness invariant — if it fails, UIs rendering
            // scale-1 data will mislead the player about scale-0 reality.
            var world = NewPaperMirror();
            var regions = Projections.Scale0To1(world, regionSize: 4);

            int food = 0, obstacle = 0, passable = 0;
            foreach (var r in regions)
            {
                food += r.FoodCount;
                obstacle += r.ObstacleCount;
                passable += r.PassableCount;
            }

            Assert.Equal(world.FoodCount, food);
            Assert.Equal(world.ObstacleCount, obstacle);
            // Passable = empty + food; obstacles are the complement.
            Assert.Equal(world.GridSize * world.GridSize - world.ObstacleCount, passable);
        }

        [Fact]
        public void Scale0To1_ExactlyOneRegionHasAgent()
        {
            // The agent is a point, not a distribution — HasAgent should be
            // true on exactly one region. Guards against projections that
            // accidentally stamp HasAgent across multiple regions (e.g., if
            // the "which region contains this coord" math were buggy).
            var world = NewPaperMirror();
            var regions = Projections.Scale0To1(world, regionSize: 4);

            int withAgent = 0;
            foreach (var r in regions) if (r.HasAgent) withAgent++;
            Assert.Equal(1, withAgent);
        }

        [Fact]
        public void Scale0To1_IsSortedCanonically()
        {
            // Canonical ordering is part of the projection contract — hashes,
            // diffs, and cross-run comparisons all rely on it. If output order
            // varies by Dictionary iteration order (which .NET randomizes),
            // this test catches it.
            var regions = Projections.Scale0To1(NewPaperMirror(), regionSize: 4);

            for (int i = 1; i < regions.Count; i++)
            {
                var prev = regions[i - 1].Coord;
                var cur = regions[i].Coord;
                bool ordered = prev.Q < cur.Q || (prev.Q == cur.Q && prev.R < cur.R);
                Assert.True(ordered, $"Region ordering violated at index {i}: {prev} before {cur}");
            }
        }

        [Fact]
        public void Scale1To2_IsDeterministic()
        {
            var world = NewPaperMirror();
            var regions = Projections.Scale0To1(world, regionSize: 4);

            var z1 = Projections.Scale1To2(regions, zoneSize: 2);
            var z2 = Projections.Scale1To2(regions, zoneSize: 2);

            Assert.Equal(z1.Count, z2.Count);
            for (int i = 0; i < z1.Count; i++) Assert.Equal(z1[i], z2[i]);
        }

        [Fact]
        public void Scale1To2_ConservesCountsAcrossBothHops()
        {
            // End-to-end sum conservation: scale-0 totals reached through the
            // composed scale-0 → scale-1 → scale-2 chain equal the original
            // World totals. If this fails, aggregation loses or duplicates
            // information somewhere in the chain.
            var world = NewPaperMirror();
            var regions = Projections.Scale0To1(world, regionSize: 4);
            var zones = Projections.Scale1To2(regions, zoneSize: 2);

            int food = 0, obstacle = 0, tiles = 0;
            foreach (var z in zones)
            {
                food += z.TotalFood;
                obstacle += z.TotalObstacle;
                tiles += z.TotalTiles;
            }

            Assert.Equal(world.FoodCount, food);
            Assert.Equal(world.ObstacleCount, obstacle);
            Assert.Equal(world.GridSize * world.GridSize, tiles);
        }

        [Fact]
        public void Scale1To2_ExactlyOneZoneHasAgent()
        {
            var world = NewPaperMirror();
            var regions = Projections.Scale0To1(world, regionSize: 4);
            var zones = Projections.Scale1To2(regions, zoneSize: 2);

            int withAgent = 0;
            foreach (var z in zones) if (z.HasAgent) withAgent++;
            Assert.Equal(1, withAgent);
        }

        [Fact]
        public void DifferentSeeds_ProduceDifferentProjections()
        {
            // If seeds drove identical layouts we wouldn't be proving much.
            // This is a sanity test that our projections actually vary with
            // world content, not just a degenerate identity.
            var a = Projections.Scale0To1(NewPaperMirror(seed: 42), regionSize: 4);
            var b = Projections.Scale0To1(NewPaperMirror(seed: 43), regionSize: 4);

            bool anyDiff = false;
            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                if (!a[i].Equals(b[i])) { anyDiff = true; break; }
            }
            Assert.True(anyDiff, "Expected at least one region to differ between seeds 42 and 43");
        }
    }
}
