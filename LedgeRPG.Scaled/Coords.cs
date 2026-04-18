using System;
using LedgeRPG.Core.World;

namespace LedgeRPG.Scaled
{
    /// Scale-1 coordinate. Addresses a rectangular group of scale-0 hex tiles.
    /// Rectangular grouping is a spike simplification — the eventual truncated-
    /// octahedron substrate will make higher-scale coords tessellate as slices
    /// of the 3D mesh rather than rectangles of flat axial hexes. The shape of
    /// this struct will change when that substrate lands; consumers should treat
    /// it as opaque rather than doing arithmetic on the component ints.
    public readonly struct RegionCoord : IEquatable<RegionCoord>
    {
        public int Q { get; }
        public int R { get; }

        public RegionCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        /// Which region a given scale-0 hex tile belongs to, at the given region
        /// size. Integer division is safe here because Core.World's grid is
        /// always the non-negative quadrant [0, GridSize) × [0, GridSize).
        public static RegionCoord ForTile(HexCoord tile, int regionSize)
        {
            if (regionSize <= 0) throw new ArgumentOutOfRangeException(nameof(regionSize));
            return new RegionCoord(tile.Q / regionSize, tile.R / regionSize);
        }

        public bool Equals(RegionCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is RegionCoord o && Equals(o);
        public override int GetHashCode() { unchecked { return (Q * 397) ^ R; } }
        public override string ToString() => $"Region({Q},{R})";
        public static bool operator ==(RegionCoord a, RegionCoord b) => a.Equals(b);
        public static bool operator !=(RegionCoord a, RegionCoord b) => !a.Equals(b);
    }

    /// Scale-2 coordinate. Addresses a rectangular group of scale-1 regions.
    /// Same spike caveat as RegionCoord — shape is provisional pending the
    /// truncated-octahedron substrate decision.
    public readonly struct ZoneCoord : IEquatable<ZoneCoord>
    {
        public int Q { get; }
        public int R { get; }

        public ZoneCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static ZoneCoord ForRegion(RegionCoord region, int zoneSize)
        {
            if (zoneSize <= 0) throw new ArgumentOutOfRangeException(nameof(zoneSize));
            return new ZoneCoord(region.Q / zoneSize, region.R / zoneSize);
        }

        public bool Equals(ZoneCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is ZoneCoord o && Equals(o);
        public override int GetHashCode() { unchecked { return (Q * 397) ^ R; } }
        public override string ToString() => $"Zone({Q},{R})";
        public static bool operator ==(ZoneCoord a, ZoneCoord b) => a.Equals(b);
        public static bool operator !=(ZoneCoord a, ZoneCoord b) => !a.Equals(b);
    }
}
