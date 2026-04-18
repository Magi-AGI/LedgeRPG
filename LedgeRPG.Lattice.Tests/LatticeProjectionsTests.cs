using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class LatticeProjectionsTests
    {
        [Fact]
        public void Project_Conservation_ScaleZeroToOne()
        {
            var w = new LatticeWorld(seed: 99, sizeX: 8, sizeY: 6, sizeZ: 8, blockedCount: 50);
            var scale1 = LatticeProjections.Project(w, parentScaleFactor: 3);

            int children = scale1.Values.Sum(a => a.ChildCount);
            int passable = scale1.Values.Sum(a => a.PassableCount);
            int blocked = scale1.Values.Sum(a => a.BlockedCount);

            Assert.Equal(w.TotalToctas, children);
            Assert.Equal(w.PassableCount, passable);
            Assert.Equal(w.BlockedCount, blocked);
        }

        [Fact]
        public void Project_AgentAppearsInExactlyOneAggregate()
        {
            var w = new LatticeWorld(seed: 99, sizeX: 8, sizeY: 6, sizeZ: 8, blockedCount: 50);
            var scale1 = LatticeProjections.Project(w, parentScaleFactor: 3);

            int withAgent = scale1.Values.Count(a => a.HasAgent);
            Assert.Equal(1, withAgent);
        }

        [Fact]
        public void Project_Determinism_SameInputSameOutput()
        {
            var w = new LatticeWorld(seed: 123, sizeX: 6, sizeY: 4, sizeZ: 6, blockedCount: 20);
            var a = LatticeProjections.Project(w, parentScaleFactor: 3);
            var b = LatticeProjections.Project(w, parentScaleFactor: 3);

            Assert.Equal(a.Count, b.Count);
            foreach (var key in a.Keys)
            {
                Assert.True(b.ContainsKey(key));
                Assert.Equal(a[key].ChildCount, b[key].ChildCount);
                Assert.Equal(a[key].PassableCount, b[key].PassableCount);
                Assert.Equal(a[key].BlockedCount, b[key].BlockedCount);
                Assert.Equal(a[key].HasAgent, b[key].HasAgent);
            }
        }

        [Fact]
        public void Project_Chained_ConservationAcrossTwoScales()
        {
            var w = new LatticeWorld(seed: 99, sizeX: 12, sizeY: 8, sizeZ: 12, blockedCount: 200);
            var scale1 = LatticeProjections.Project(w, parentScaleFactor: 3);
            var scale2 = LatticeProjections.Project(scale1, parentScaleFactor: 3);

            int children = scale2.Values.Sum(a => a.ChildCount);
            int passable = scale2.Values.Sum(a => a.PassableCount);
            int blocked = scale2.Values.Sum(a => a.BlockedCount);

            Assert.Equal(w.TotalToctas, children);
            Assert.Equal(w.PassableCount, passable);
            Assert.Equal(w.BlockedCount, blocked);
            Assert.Equal(1, scale2.Values.Count(a => a.HasAgent));
        }

        [Fact]
        public void Project_AggregateCountStrictlyLessThanSource()
        {
            // Any factor > 1 should produce fewer aggregates than scale-0 cells,
            // because multiple children collapse onto the same parent.
            var w = new LatticeWorld(seed: 7, sizeX: 10, sizeY: 6, sizeZ: 10, blockedCount: 30);
            var scale1 = LatticeProjections.Project(w, parentScaleFactor: 3);
            Assert.True(scale1.Count < w.TotalToctas);
        }

        [Fact]
        public void NearestParent_RoundTripForIntegerMultiple()
        {
            // A child at world-position k*factor away from origin should map to
            // the parent coord (k, 0, k) (even-layer), independent of factor.
            for (int factor = 2; factor <= 10; factor++)
            {
                var child = new ToctaCoord(factor, 0, factor);
                var parent = LatticeProjections.NearestParent(child, factor);
                Assert.Equal(new ToctaCoord(1, 0, 1), parent);
            }
        }

        [Fact]
        public void HexSlice_SingleYLayer_YieldsPlanarAggregate()
        {
            // Hex-slice mode: fix Y and project only that layer. Confirms the
            // "hex grid falls out of a 3D lattice slice" architectural claim.
            var w = new LatticeWorld(seed: 42, sizeX: 8, sizeY: 4, sizeZ: 8, blockedCount: 20);

            var layer0 = w.AllCoords().Where(c => c.Y == 0).ToList();
            Assert.Equal(64, layer0.Count);
            Assert.All(layer0, c => Assert.False(c.IsOddLayer));

            var layer1 = w.AllCoords().Where(c => c.Y == 1).ToList();
            Assert.Equal(64, layer1.Count);
            Assert.All(layer1, c => Assert.True(c.IsOddLayer));

            int layer0Passable = layer0.Count(c => w.TypeAt(c) == ToctaType.Passable);
            int layer1Passable = layer1.Count(c => w.TypeAt(c) == ToctaType.Passable);
            int totalPassable = w.AllCoords().Count(c => w.TypeAt(c) == ToctaType.Passable);
            int sumAcrossLayers = Enumerable.Range(0, 4)
                .Sum(y => w.AllCoords().Where(c => c.Y == y).Count(c => w.TypeAt(c) == ToctaType.Passable));
            Assert.Equal(totalPassable, sumAcrossLayers);
            Assert.Equal(w.PassableCount, layer0Passable + layer1Passable
                + w.AllCoords().Where(c => c.Y == 2).Count(c => w.TypeAt(c) == ToctaType.Passable)
                + w.AllCoords().Where(c => c.Y == 3).Count(c => w.TypeAt(c) == ToctaType.Passable));
        }
    }
}
