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

        [Fact]
        public void FromWorldPosition_RoundTripsCellCenter()
        {
            // Every cell's own world center should map back to that cell.
            // Cover both sublattices (even and odd Y) and a few signs.
            for (int x = -4; x <= 4; x++)
            for (int y = -5; y <= 5; y++)
            for (int z = -4; z <= 4; z++)
            {
                var c = new ToctaCoord(x, y, z);
                var (wx, wy, wz) = c.WorldPosition;
                var back = ToctaCoord.FromWorldPosition(wx, wy, wz);
                Assert.Equal(c, back);
            }
        }

        [Fact]
        public void FromWorldPosition_SmallPerturbationStaysInCell()
        {
            // Tocta has inradius 0.5 (square-face) / sqrt(3)/2 ≈ 0.433
            // (hex-face). A 0.1-unit perturbation from the center can't
            // escape the Voronoi cell, so we must stay put.
            var c = new ToctaCoord(2, 4, -3);
            var (wx, wy, wz) = c.WorldPosition;
            foreach (var d in new[] { -0.1, 0.0, 0.1 })
            {
                Assert.Equal(c, ToctaCoord.FromWorldPosition(wx + d, wy,     wz));
                Assert.Equal(c, ToctaCoord.FromWorldPosition(wx,     wy + d, wz));
                Assert.Equal(c, ToctaCoord.FromWorldPosition(wx,     wy,     wz + d));
            }
        }

        [Fact]
        public void FromWorldPosition_EvenAndOddSublatticesDistinguished()
        {
            // Point closer to an odd-Y center picks the odd cell; point
            // closer to an even-Y center picks the even cell. Use clear
            // winners (not boundary points) so banker's-rounding ties
            // don't affect the assertion.
            Assert.Equal(new ToctaCoord(0, 0, 0),
                         ToctaCoord.FromWorldPosition(0.05, 0.05, 0.05));
            Assert.Equal(new ToctaCoord(0, 1, 0),   // world (0.5, 0.5, 0.5)
                         ToctaCoord.FromWorldPosition(0.45, 0.45, 0.45));
        }

        [Fact]
        public void FromWorldPosition_NegativeYWorks()
        {
            var c = new ToctaCoord(1, -3, 2);       // odd layer, world (1.5, -1.5, 2.5)
            var (wx, wy, wz) = c.WorldPosition;
            Assert.Equal(c, ToctaCoord.FromWorldPosition(wx, wy, wz));
            Assert.Equal(c, ToctaCoord.FromWorldPosition(wx + 0.1, wy - 0.1, wz + 0.1));
        }
    }
}
