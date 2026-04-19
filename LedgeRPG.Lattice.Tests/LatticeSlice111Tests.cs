using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class LatticeSlice111Tests
    {
        [Fact]
        public void LayerIndex_Origin_IsZero()
        {
            Assert.Equal(0, LatticeSlice111.LayerIndex(new ToctaCoord(0, 0, 0)));
        }

        [Fact]
        public void LayerIndex_EvenParityCellsYieldEvenK()
        {
            // Y even → k even: k = 2(X+Z) + Y + 0.
            Assert.Equal(2, LatticeSlice111.LayerIndex(new ToctaCoord(1, 0, 0)));
            Assert.Equal(2, LatticeSlice111.LayerIndex(new ToctaCoord(0, 2, 0)));
            Assert.Equal(0, LatticeSlice111.LayerIndex(new ToctaCoord(1, 0, -1)));
            Assert.Equal(-4, LatticeSlice111.LayerIndex(new ToctaCoord(-1, -2, 0)));
        }

        [Fact]
        public void LayerIndex_OddParityCellsYieldOddK()
        {
            // Y odd → k odd: k = 2(X+Z) + Y + 2.
            Assert.Equal(3, LatticeSlice111.LayerIndex(new ToctaCoord(0, 1, 0)));
            Assert.Equal(1, LatticeSlice111.LayerIndex(new ToctaCoord(-1, 1, 0)));
            Assert.Equal(5, LatticeSlice111.LayerIndex(new ToctaCoord(0, 3, 0)));
            // Odd-layer cells offset world by (+0.5, 0, +0.5), so (0,-1,0)
            // sits at world (0.5, -0.5, 0.5) — height along (1,1,1) = 0.5,
            // giving k = +1, not -1.
            Assert.Equal(1, LatticeSlice111.LayerIndex(new ToctaCoord(0, -1, 0)));
            Assert.Equal(-1, LatticeSlice111.LayerIndex(new ToctaCoord(0, -3, 0)));
        }

        [Fact]
        public void LayerIndex_MatchesWorldHeightAlong111()
        {
            // k / 2 should equal (wx + wy + wz) to floating-point precision
            // (integer k lives at exact half-integer world heights).
            var cells = new[]
            {
                new ToctaCoord(0, 0, 0),
                new ToctaCoord(3, 2, -1),
                new ToctaCoord(-4, 1, 7),
                new ToctaCoord(2, -3, 5),
            };
            foreach (var c in cells)
            {
                int k = LatticeSlice111.LayerIndex(c);
                var (wx, wy, wz) = c.WorldPosition;
                Assert.Equal(k / 2.0, wx + wy + wz, 9);
            }
        }

        [Fact]
        public void HexNeighbors_PreserveLayerIndex_EvenSource()
        {
            var center = new ToctaCoord(3, 4, -2);
            int k = LatticeSlice111.LayerIndex(center);
            foreach (var n in LatticeSlice111.HexNeighbors(center))
                Assert.Equal(k, LatticeSlice111.LayerIndex(n));
        }

        [Fact]
        public void HexNeighbors_PreserveLayerIndex_OddSource()
        {
            var center = new ToctaCoord(3, 5, -2);
            int k = LatticeSlice111.LayerIndex(center);
            foreach (var n in LatticeSlice111.HexNeighbors(center))
                Assert.Equal(k, LatticeSlice111.LayerIndex(n));
        }

        [Fact]
        public void HexNeighbors_AreUnitRootTwoDistanceInWorld()
        {
            var center = new ToctaCoord(7, 3, 2);
            var (cx, cy, cz) = center.WorldPosition;
            foreach (var n in LatticeSlice111.HexNeighbors(center))
            {
                var (nx, ny, nz) = n.WorldPosition;
                double d = Math.Sqrt((nx - cx) * (nx - cx) + (ny - cy) * (ny - cy) + (nz - cz) * (nz - cz));
                Assert.InRange(d, Math.Sqrt(2.0) - 1e-9, Math.Sqrt(2.0) + 1e-9);
            }
        }

        [Fact]
        public void HexNeighbors_AreSixDistinct()
        {
            var center = new ToctaCoord(0, 0, 0);
            var ns = LatticeSlice111.HexNeighbors(center).ToList();
            Assert.Equal(6, ns.Count);
            Assert.Equal(6, ns.Distinct().Count());
            Assert.DoesNotContain(center, ns);
        }

        [Fact]
        public void HexNeighbors_Reciprocal()
        {
            var center = new ToctaCoord(2, 3, 4);
            foreach (var n in LatticeSlice111.HexNeighbors(center))
                Assert.Contains(center, LatticeSlice111.HexNeighbors(n));
        }

        [Fact]
        public void HexNeighbors_AreNotBccFaceNeighbors()
        {
            // Confirms the "each hex step = 2 BCC steps" invariant: no hex-
            // delta lands on a BCC face-neighbor. If this ever fires, the
            // Option-B movement cost assumption needs revisiting.
            var center = new ToctaCoord(0, 0, 0);
            var bccFaces = new HashSet<ToctaCoord>(ToctaNeighbors.FaceNeighbors(center));
            foreach (var n in LatticeSlice111.HexNeighbors(center))
                Assert.DoesNotContain(n, bccFaces);
        }

        [Fact]
        public void CellsInLayer_FiltersByLayerIndex()
        {
            var world = new LatticeWorld(seed: 1, sizeX: 6, sizeY: 6, sizeZ: 6, blockedCount: 0);
            var inLayer0 = LatticeSlice111.CellsInLayer(world, 0).ToList();
            Assert.NotEmpty(inLayer0);
            foreach (var c in inLayer0) Assert.Equal(0, LatticeSlice111.LayerIndex(c));
        }

        [Fact]
        public void CellsInSlab_IncludesAllLayersInRange()
        {
            var world = new LatticeWorld(seed: 1, sizeX: 6, sizeY: 6, sizeZ: 6, blockedCount: 0);
            var slab = LatticeSlice111.CellsInSlab(world, kCenter: 4, halfThickness: 1).ToList();
            Assert.NotEmpty(slab);
            foreach (var c in slab)
            {
                int k = LatticeSlice111.LayerIndex(c);
                Assert.InRange(k, 3, 5);
            }
            // Thickness-1 slab should span 3 distinct layers when the world
            // is big enough.
            var distinctKs = slab.Select(LatticeSlice111.LayerIndex).Distinct().ToList();
            Assert.Equal(3, distinctKs.Count);
        }

        [Fact]
        public void CellsInSlab_HalfThicknessZero_MatchesCellsInLayer()
        {
            var world = new LatticeWorld(seed: 1, sizeX: 5, sizeY: 5, sizeZ: 5, blockedCount: 0);
            var layer = LatticeSlice111.CellsInLayer(world, 4).ToList();
            var slab = LatticeSlice111.CellsInSlab(world, kCenter: 4, halfThickness: 0).ToList();
            Assert.Equal(layer, slab);
        }
    }
}
