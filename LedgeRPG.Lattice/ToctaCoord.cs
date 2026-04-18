using System;
using System.Collections.Generic;

namespace LedgeRPG.Lattice
{
    /// Integer coordinate for a single tocta (truncated-octahedron cell) in
    /// the body-centered-cubic lattice. Y parity drives an X/Z half-offset
    /// at the world-position level — even-Y layers align to integer world
    /// positions, odd-Y layers are offset by (0.5, 0, 0.5).
    ///
    /// This keeps neighbor arithmetic in integer space: the 14 face-
    /// neighbors are tabulated as integer deltas with a parity branch, no
    /// floating-point required for same-scale adjacency.
    public readonly struct ToctaCoord : IEquatable<ToctaCoord>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public ToctaCoord(int x, int y, int z) { X = x; Y = y; Z = z; }

        public bool IsOddLayer => ((Y % 2) + 2) % 2 == 1;

        /// World position in units of tocta-edge-length. Even layers align
        /// to integer world positions; odd layers offset by (0.5, 0, 0.5)
        /// in X/Z. Y is half-stepped (each integer Y is half a layer spacing)
        /// so that parity-flipping neighbors land at consistent world offsets.
        public (double x, double y, double z) WorldPosition
        {
            get
            {
                double off = IsOddLayer ? 0.5 : 0.0;
                return (X + off, 0.5 * Y, Z + off);
            }
        }

        public bool Equals(ToctaCoord other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is ToctaCoord c && Equals(c);
        public override int GetHashCode()
        {
            unchecked
            {
                int h = X;
                h = (h * 397) ^ Y;
                h = (h * 397) ^ Z;
                return h;
            }
        }
        public override string ToString() => $"({X},{Y},{Z})";

        public static bool operator ==(ToctaCoord a, ToctaCoord b) => a.Equals(b);
        public static bool operator !=(ToctaCoord a, ToctaCoord b) => !a.Equals(b);
    }

    /// Face-neighbor enumeration for toctas. A truncated octahedron has 14
    /// faces (6 square + 8 hexagonal); in the BCC lattice each face is
    /// shared with exactly one neighbor, giving 14 face-adjacent toctas per
    /// cell regardless of scale. The 6 square-face offsets are parity-
    /// independent; the 8 hex-face offsets branch on Y parity.
    public static class ToctaNeighbors
    {
        private static readonly (int dx, int dy, int dz)[] SquareFaceDeltas =
        {
            (+1, 0, 0), (-1, 0, 0),
            (0, +2, 0), (0, -2, 0),
            (0, 0, +1), (0, 0, -1),
        };

        private static readonly (int dx, int dy, int dz)[] HexFaceDeltasEven =
        {
            ( 0, +1,  0), (-1, +1,  0), ( 0, +1, -1), (-1, +1, -1),
            ( 0, -1,  0), (-1, -1,  0), ( 0, -1, -1), (-1, -1, -1),
        };

        private static readonly (int dx, int dy, int dz)[] HexFaceDeltasOdd =
        {
            ( 0, +1,  0), (+1, +1,  0), ( 0, +1, +1), (+1, +1, +1),
            ( 0, -1,  0), (+1, -1,  0), ( 0, -1, +1), (+1, -1, +1),
        };

        /// Enumerate the 14 face-neighbors of a given tocta in a stable,
        /// documented order: 6 square-face neighbors first, then 8 hex-face
        /// neighbors (4 in layer above, 4 in layer below).
        public static IEnumerable<ToctaCoord> FaceNeighbors(ToctaCoord center)
        {
            foreach (var d in SquareFaceDeltas)
                yield return new ToctaCoord(center.X + d.dx, center.Y + d.dy, center.Z + d.dz);

            var hex = center.IsOddLayer ? HexFaceDeltasOdd : HexFaceDeltasEven;
            foreach (var d in hex)
                yield return new ToctaCoord(center.X + d.dx, center.Y + d.dy, center.Z + d.dz);
        }

        public const int FaceCount = 14;
    }
}
