using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class ToctaCoordTests
    {
        [Fact]
        public void Equality_SameComponents_AreEqual()
        {
            var a = new ToctaCoord(3, -2, 7);
            var b = new ToctaCoord(3, -2, 7);
            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.False(a != b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void IsOddLayer_HandlesNegativeY()
        {
            Assert.False(new ToctaCoord(0, 0, 0).IsOddLayer);
            Assert.True(new ToctaCoord(0, 1, 0).IsOddLayer);
            Assert.False(new ToctaCoord(0, -2, 0).IsOddLayer);
            Assert.True(new ToctaCoord(0, -1, 0).IsOddLayer);
            Assert.True(new ToctaCoord(0, -3, 0).IsOddLayer);
        }

        [Fact]
        public void WorldPosition_EvenLayer_HasNoOffset()
        {
            var (x, y, z) = new ToctaCoord(2, 4, 3).WorldPosition;
            Assert.Equal(2.0, x);
            Assert.Equal(2.0, y);
            Assert.Equal(3.0, z);
        }

        [Fact]
        public void WorldPosition_OddLayer_HalfOffsetsXZ()
        {
            var (x, y, z) = new ToctaCoord(2, 3, 3).WorldPosition;
            Assert.Equal(2.5, x);
            Assert.Equal(1.5, y);
            Assert.Equal(3.5, z);
        }

        [Fact]
        public void FaceNeighbors_Returns14UniqueCoords()
        {
            var center = new ToctaCoord(5, 2, 5);
            var neighbors = ToctaNeighbors.FaceNeighbors(center).ToList();
            Assert.Equal(14, neighbors.Count);
            Assert.Equal(ToctaNeighbors.FaceCount, neighbors.Count);
            Assert.Equal(neighbors.Count, neighbors.Distinct().Count());
            Assert.DoesNotContain(center, neighbors);
        }

        [Fact]
        public void FaceNeighbors_Reciprocal_EvenLayer()
        {
            var center = new ToctaCoord(5, 2, 5); // even layer
            foreach (var n in ToctaNeighbors.FaceNeighbors(center))
            {
                var backRefs = ToctaNeighbors.FaceNeighbors(n).ToList();
                Assert.Contains(center, backRefs);
            }
        }

        [Fact]
        public void FaceNeighbors_Reciprocal_OddLayer()
        {
            var center = new ToctaCoord(5, 3, 5); // odd layer
            foreach (var n in ToctaNeighbors.FaceNeighbors(center))
            {
                var backRefs = ToctaNeighbors.FaceNeighbors(n).ToList();
                Assert.Contains(center, backRefs);
            }
        }

        [Fact]
        public void FaceNeighbors_AreUnitDistanceInWorldSpace()
        {
            // All 14 face-neighbors in a BCC lattice share a face with the
            // center — square faces are 1 unit away, hex faces are sqrt(3)/2
            // ≈ 0.866 away (measuring center-to-center). Sanity: all within
            // 1.0 of center.
            var center = new ToctaCoord(10, 4, 10);
            var (cx, cy, cz) = center.WorldPosition;
            foreach (var n in ToctaNeighbors.FaceNeighbors(center))
            {
                var (nx, ny, nz) = n.WorldPosition;
                double d = System.Math.Sqrt((nx - cx) * (nx - cx) + (ny - cy) * (ny - cy) + (nz - cz) * (nz - cz));
                Assert.InRange(d, 0.85, 1.05);
            }
        }

        [Fact]
        public void FaceNeighbors_StableOrder_AcrossCalls()
        {
            var center = new ToctaCoord(7, 5, 9);
            var a = ToctaNeighbors.FaceNeighbors(center).ToList();
            var b = ToctaNeighbors.FaceNeighbors(center).ToList();
            Assert.Equal(a, b);
        }
    }
}
