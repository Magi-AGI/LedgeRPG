using System.Collections.Generic;

namespace LedgeRPG.Lattice
{
    /// Slice and slab geometry for the (1,1,1)-perpendicular view mode.
    /// Companion to ToctaNeighbors — that class covers 14-direction BCC face
    /// adjacency; this one defines the (1,1,1)-planar hex topology that
    /// emerges when you project BCC cells along the (1,1,1) axis.
    ///
    /// The (1,1,1)-layer index k quantizes a cell's height along (1,1,1)
    /// into integers: k = 2(X+Z) + Y + 2*(Y mod 2). Spacing between
    /// consecutive k-layers is 0.5/√3 ≈ 0.289 world units.
    ///
    /// Cells in a single k-layer form a triangular lattice with edge √2
    /// (in world space). Each in-layer cell has exactly 6 in-layer
    /// neighbors at distance √2. Crucially these 6 neighbors are NOT
    /// BCC face-neighbors — every BCC face-step changes k by ±1, ±2, or
    /// ±3. One hex-neighbor step equals two BCC face-steps.
    ///
    /// The 6 hex-deltas are parity-independent (unlike the 8 hex-face
    /// deltas in ToctaNeighbors) because all six have dy ∈ {0, ±2},
    /// which cancels the Y-parity offset in WorldPosition.
    public static class LatticeSlice111
    {
        /// Six in-layer hex-neighbor deltas. Same for even-Y and odd-Y
        /// source cells. World-space directions (unit √2):
        ///   (+1, 0, -1), (-1, 0, +1),
        ///   ( 0, 1, -1), (-1, 1,  0),
        ///   ( 0,-1, +1), (+1,-1,  0)
        public static readonly (int dx, int dy, int dz)[] HexNeighborDeltas =
        {
            (+1, 0, -1),
            (-1, 0, +1),
            ( 0, +2, -1),
            (-1, +2,  0),
            ( 0, -2, +1),
            (+1, -2,  0),
        };

        /// Integer height of a cell along the (1,1,1) axis. Consecutive
        /// integers are 0.5/√3 world units apart. Even k ⇔ Y even; odd k
        /// ⇔ Y odd.
        public static int LayerIndex(ToctaCoord c)
        {
            int yMod2 = ((c.Y % 2) + 2) % 2;
            return 2 * (c.X + c.Z) + c.Y + 2 * yMod2;
        }

        /// Enumerate the six in-layer hex-neighbors of a cell. All six
        /// have the same LayerIndex as the input regardless of Y parity.
        public static IEnumerable<ToctaCoord> HexNeighbors(ToctaCoord center)
        {
            foreach (var d in HexNeighborDeltas)
                yield return new ToctaCoord(center.X + d.dx, center.Y + d.dy, center.Z + d.dz);
        }

        /// Enumerate every in-bounds cell in the given (1,1,1)-layer.
        /// Order matches LatticeWorld.AllCoords() (Y-major).
        public static IEnumerable<ToctaCoord> CellsInLayer(LatticeWorld world, int k)
        {
            foreach (var c in world.AllCoords())
                if (LayerIndex(c) == k) yield return c;
        }

        /// Enumerate every in-bounds cell whose layer index is within
        /// halfThickness of kCenter (inclusive on both sides). halfThickness
        /// 0 ≡ CellsInLayer; halfThickness 1 ≡ three consecutive layers.
        public static IEnumerable<ToctaCoord> CellsInSlab(LatticeWorld world, int kCenter, int halfThickness)
        {
            int lo = kCenter - halfThickness;
            int hi = kCenter + halfThickness;
            foreach (var c in world.AllCoords())
            {
                int k = LayerIndex(c);
                if (k >= lo && k <= hi) yield return c;
            }
        }
    }
}
