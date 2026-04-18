using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class ToctaMeshDataTests
    {
        [Fact]
        public void Vertices_Are24UniqueDistinctPoints()
        {
            Assert.Equal(24, ToctaMeshData.Vertices.Length);
            Assert.Equal(ToctaMeshData.VertexCount, ToctaMeshData.Vertices.Length);

            var seen = new HashSet<(float, float, float)>();
            foreach (var v in ToctaMeshData.Vertices)
                Assert.True(seen.Add(v), $"Duplicate vertex {v}");
        }

        [Fact]
        public void Vertices_AllAtSameDistanceFromOrigin()
        {
            // Truncated octahedron is vertex-transitive: every vertex is
            // sqrt(0 + 1 + 4) = sqrt(5) from origin unscaled, times Scale.
            double expected = Math.Sqrt(5) * ToctaMeshData.Scale;
            foreach (var v in ToctaMeshData.Vertices)
            {
                double d = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
                Assert.InRange(d, expected - 1e-5, expected + 1e-5);
            }
        }

        [Fact]
        public void SquareFaces_HaveCorrectCountAndIndexing()
        {
            Assert.Equal(6, ToctaMeshData.SquareFaces.Length);
            Assert.Equal(ToctaMeshData.SquareFaceCount, ToctaMeshData.SquareFaces.Length);
            foreach (var face in ToctaMeshData.SquareFaces)
            {
                Assert.Equal(4, face.Length);
                Assert.Equal(4, face.Distinct().Count());
                Assert.All(face, i => Assert.InRange(i, 0, ToctaMeshData.VertexCount - 1));
            }
        }

        [Fact]
        public void HexFaces_HaveCorrectCountAndIndexing()
        {
            Assert.Equal(8, ToctaMeshData.HexFaces.Length);
            Assert.Equal(ToctaMeshData.HexFaceCount, ToctaMeshData.HexFaces.Length);
            foreach (var face in ToctaMeshData.HexFaces)
            {
                Assert.Equal(6, face.Length);
                Assert.Equal(6, face.Distinct().Count());
                Assert.All(face, i => Assert.InRange(i, 0, ToctaMeshData.VertexCount - 1));
            }
        }

        [Fact]
        public void EveryVertex_AppearsInExactly3Faces()
        {
            // Truncated octahedron is 3-regular on faces: each vertex borders
            // 2 hexagons + 1 square (or equivalently, all 3 faces meeting at
            // a vertex share it).
            var counts = new int[ToctaMeshData.VertexCount];
            foreach (var face in ToctaMeshData.SquareFaces)
                foreach (var i in face) counts[i]++;
            foreach (var face in ToctaMeshData.HexFaces)
                foreach (var i in face) counts[i]++;

            Assert.All(counts, c => Assert.Equal(3, c));
        }

        [Fact]
        public void EveryVertex_AppearsInExactly2Hexes_And1Square()
        {
            var hexCounts = new int[ToctaMeshData.VertexCount];
            var squareCounts = new int[ToctaMeshData.VertexCount];
            foreach (var face in ToctaMeshData.HexFaces)
                foreach (var i in face) hexCounts[i]++;
            foreach (var face in ToctaMeshData.SquareFaces)
                foreach (var i in face) squareCounts[i]++;

            Assert.All(hexCounts, c => Assert.Equal(2, c));
            Assert.All(squareCounts, c => Assert.Equal(1, c));
        }

        [Fact]
        public void SquareFaces_WindingProducesDeclaredOutwardNormal()
        {
            // (v[1]-v[0]) × (v[2]-v[0]) should align with the declared normal.
            for (int f = 0; f < ToctaMeshData.SquareFaces.Length; f++)
            {
                var face = ToctaMeshData.SquareFaces[f];
                var n = ComputeNormal(face);
                var expected = ToctaMeshData.SquareFaceNormals[f];
                double dot = n.X * expected.X + n.Y * expected.Y + n.Z * expected.Z;
                Assert.True(dot > 0, $"Square face {f} winding opposes declared normal");
            }
        }

        [Fact]
        public void HexFaces_WindingProducesDeclaredOutwardNormal()
        {
            for (int f = 0; f < ToctaMeshData.HexFaces.Length; f++)
            {
                var face = ToctaMeshData.HexFaces[f];
                var n = ComputeNormal(face);
                var expected = ToctaMeshData.HexFaceNormals[f];
                double dot = n.X * expected.X + n.Y * expected.Y + n.Z * expected.Z;
                Assert.True(dot > 0, $"Hex face {f} winding opposes declared normal");
            }
        }

        [Fact]
        public void SquareFaceCentroids_AtExpectedBCCHalfStep()
        {
            // Square faces must be at ±0.5 along an axis (half of unit BCC spacing).
            for (int f = 0; f < ToctaMeshData.SquareFaces.Length; f++)
            {
                var c = Centroid(ToctaMeshData.SquareFaces[f]);
                var n = ToctaMeshData.SquareFaceNormals[f];
                double expectedOffset = 0.5;
                double measured = c.X * n.X + c.Y * n.Y + c.Z * n.Z;
                Assert.InRange(measured, expectedOffset - 1e-5, expectedOffset + 1e-5);
            }
        }

        [Fact]
        public void HexFaceCentroids_AtBCCHexHalfDistance()
        {
            // Hex face centers at (±0.25, ±0.25, ±0.25); distance from origin
            // is sqrt(3)/4, matching half the BCC hex-neighbor center distance.
            double expected = Math.Sqrt(3) / 4;
            for (int f = 0; f < ToctaMeshData.HexFaces.Length; f++)
            {
                var c = Centroid(ToctaMeshData.HexFaces[f]);
                double d = Math.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z);
                Assert.InRange(d, expected - 1e-5, expected + 1e-5);
            }
        }

        [Fact]
        public void AllEdges_HaveUniformLength()
        {
            // Edge length of a truncated octahedron with vertices at
            // permutations of (0, ±1, ±2) is sqrt(2), times Scale.
            double expected = Math.Sqrt(2) * ToctaMeshData.Scale;
            var edges = CollectEdges();
            Assert.NotEmpty(edges);
            foreach (var (a, b) in edges)
            {
                var va = ToctaMeshData.Vertices[a];
                var vb = ToctaMeshData.Vertices[b];
                double dx = va.X - vb.X, dy = va.Y - vb.Y, dz = va.Z - vb.Z;
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                Assert.InRange(d, expected - 1e-5, expected + 1e-5);
            }
        }

        [Fact]
        public void TotalEdgeCount_Is36()
        {
            // Euler: V - E + F = 2 → 24 - E + 14 = 2 → E = 36.
            var edges = CollectEdges();
            Assert.Equal(36, edges.Count);
        }

        [Fact]
        public void HexFaceNormals_AreUnitLength()
        {
            foreach (var n in ToctaMeshData.HexFaceNormals)
            {
                double m = Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
                Assert.InRange(m, 1 - 1e-5, 1 + 1e-5);
            }
        }

        private static (double X, double Y, double Z) ComputeNormal(int[] face)
        {
            var v0 = ToctaMeshData.Vertices[face[0]];
            var v1 = ToctaMeshData.Vertices[face[1]];
            var v2 = ToctaMeshData.Vertices[face[2]];
            double ax = v1.X - v0.X, ay = v1.Y - v0.Y, az = v1.Z - v0.Z;
            double bx = v2.X - v0.X, by = v2.Y - v0.Y, bz = v2.Z - v0.Z;
            return (ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx);
        }

        private static (double X, double Y, double Z) Centroid(int[] face)
        {
            double sx = 0, sy = 0, sz = 0;
            foreach (var i in face)
            {
                var v = ToctaMeshData.Vertices[i];
                sx += v.X; sy += v.Y; sz += v.Z;
            }
            return (sx / face.Length, sy / face.Length, sz / face.Length);
        }

        private static HashSet<(int, int)> CollectEdges()
        {
            var edges = new HashSet<(int, int)>();
            foreach (var face in ToctaMeshData.SquareFaces)
                AddFaceEdges(face, edges);
            foreach (var face in ToctaMeshData.HexFaces)
                AddFaceEdges(face, edges);
            return edges;
        }

        private static void AddFaceEdges(int[] face, HashSet<(int, int)> edges)
        {
            for (int i = 0; i < face.Length; i++)
            {
                int a = face[i];
                int b = face[(i + 1) % face.Length];
                var key = a < b ? (a, b) : (b, a);
                edges.Add(key);
            }
        }
    }
}
