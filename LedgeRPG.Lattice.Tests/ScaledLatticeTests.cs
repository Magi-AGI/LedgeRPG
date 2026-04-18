using System;
using System.Linq;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class ScaledLatticeTests
    {
        [Fact]
        public void GetScale_0_ReturnsNull_UseSourceDirectly()
        {
            var w = new LatticeWorld(seed: 1, sizeX: 4, sizeY: 4, sizeZ: 4, blockedCount: 5);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);
            Assert.Null(s.GetScale(0));
            Assert.Same(w, s.Source);
        }

        [Fact]
        public void GetScale_Conservation_AtEveryLevel()
        {
            var w = new LatticeWorld(seed: 42, sizeX: 12, sizeY: 8, sizeZ: 12, blockedCount: 150);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 4);

            for (int scale = 1; scale < s.ScaleCount; scale++)
            {
                var layer = s.GetScale(scale);
                Assert.Equal(w.TotalToctas, layer.Values.Sum(a => a.ChildCount));
                Assert.Equal(w.PassableCount, layer.Values.Sum(a => a.PassableCount));
                Assert.Equal(w.BlockedCount, layer.Values.Sum(a => a.BlockedCount));
                Assert.Equal(1, layer.Values.Count(a => a.HasAgent));
            }
        }

        [Fact]
        public void GetScale_CachesBetweenInvalidations()
        {
            var w = new LatticeWorld(seed: 1, sizeX: 6, sizeY: 4, sizeZ: 6, blockedCount: 20);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);

            var a = s.GetScale(1);
            var b = s.GetScale(1);
            Assert.Same(a, b);

            s.Invalidate();
            var c = s.GetScale(1);
            Assert.NotSame(a, c);
        }

        [Fact]
        public void GetScale_VersionBumps_OnInvalidate()
        {
            var w = new LatticeWorld(seed: 1, sizeX: 4, sizeY: 4, sizeZ: 4, blockedCount: 5);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);
            long v0 = s.Version;
            s.Invalidate();
            Assert.Equal(v0 + 1, s.Version);
        }

        [Fact]
        public void GetScale_OutOfRange_Throws()
        {
            var w = new LatticeWorld(seed: 1, sizeX: 4, sizeY: 4, sizeZ: 4, blockedCount: 5);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetScale(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetScale(3));
        }

        [Fact]
        public void Constructor_RejectsBadArgs()
        {
            var w = new LatticeWorld(seed: 1, sizeX: 4, sizeY: 4, sizeZ: 4, blockedCount: 5);
            Assert.Throws<ArgumentNullException>(() => new ScaledLattice(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScaledLattice(w, scaleFactor: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScaledLattice(w, scaleCount: 0));
        }

        [Fact]
        public void GetScale_ShrinksWithEachScale()
        {
            // A larger-factor scale should have fewer aggregates than a smaller-factor.
            // And scale-2 should have fewer aggregates than scale-1 at the same factor.
            var w = new LatticeWorld(seed: 42, sizeX: 20, sizeY: 8, sizeZ: 20, blockedCount: 200);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);
            int c1 = s.GetScale(1).Count;
            int c2 = s.GetScale(2).Count;
            Assert.True(c2 < c1, $"Expected scale-2 count ({c2}) < scale-1 count ({c1})");
            Assert.True(c1 < w.TotalToctas);
        }
    }
}
