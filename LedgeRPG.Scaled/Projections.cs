using System;
using System.Collections.Generic;
using LedgeRPG.Core.World;

namespace LedgeRPG.Scaled
{
    /// Pure projection functions — one World plus a region/zone size in, a
    /// sorted list of aggregate cells out. Deterministic: same inputs yield
    /// byte-equivalent outputs. No caching here; ScaledWorld is where caching
    /// lives if it's needed.
    ///
    /// Iteration order is canonical (Q, R)-sorted so aggregate outputs are
    /// stable for hashing and for tests that compare slices. This matches
    /// Core.World.GridSnapshot()'s sorted ordering.
    public static class Projections
    {
        /// Aggregate every scale-0 tile in <paramref name="world"/> into its
        /// containing RegionCoord, returned in canonical (Q, R) order. Regions
        /// that would be empty (no member tiles) are omitted. Partial regions
        /// at the grid edge — when GridSize is not divisible by regionSize —
        /// are returned with fewer tiles; TileCount tells you so.
        public static IReadOnlyList<RegionCell> Scale0To1(World world, int regionSize)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (regionSize <= 0) throw new ArgumentOutOfRangeException(nameof(regionSize));

            var acc = new Dictionary<RegionCoord, Accumulator>();
            var agentRegion = RegionCoord.ForTile(world.AgentPos, regionSize);

            foreach (var cell in world.GridSnapshot())
            {
                var rc = RegionCoord.ForTile(cell.Coord, regionSize);
                if (!acc.TryGetValue(rc, out var a))
                {
                    a = new Accumulator();
                    acc[rc] = a;
                }
                a.TileCount++;
                switch (cell.Type)
                {
                    case TileType.Food:
                        a.FoodCount++;
                        a.PassableCount++;
                        break;
                    case TileType.Empty:
                        a.PassableCount++;
                        break;
                    case TileType.Obstacle:
                        a.ObstacleCount++;
                        break;
                }
            }

            var keys = new RegionCoord[acc.Count];
            acc.Keys.CopyTo(keys, 0);
            Array.Sort(keys, CompareRegion);

            var result = new List<RegionCell>(keys.Length);
            foreach (var k in keys)
            {
                var a = acc[k];
                result.Add(new RegionCell(
                    coord: k,
                    tileCount: a.TileCount,
                    passableCount: a.PassableCount,
                    foodCount: a.FoodCount,
                    obstacleCount: a.ObstacleCount,
                    hasAgent: k.Equals(agentRegion)));
            }
            return result;
        }

        /// Aggregate scale-1 region cells into scale-2 zone cells. Takes the
        /// projected regions rather than the raw World so callers can compose
        /// — or inject a different scale-1 source — without this method having
        /// to know the scale-0 details.
        public static IReadOnlyList<ZoneCell> Scale1To2(
            IReadOnlyList<RegionCell> regions,
            int zoneSize)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            if (zoneSize <= 0) throw new ArgumentOutOfRangeException(nameof(zoneSize));

            var acc = new Dictionary<ZoneCoord, ZoneAccumulator>();
            foreach (var r in regions)
            {
                var zc = ZoneCoord.ForRegion(r.Coord, zoneSize);
                if (!acc.TryGetValue(zc, out var a))
                {
                    a = new ZoneAccumulator();
                    acc[zc] = a;
                }
                a.RegionCount++;
                a.TotalTiles += r.TileCount;
                a.TotalPassable += r.PassableCount;
                a.TotalFood += r.FoodCount;
                a.TotalObstacle += r.ObstacleCount;
                if (r.HasAgent) a.HasAgent = true;
            }

            var keys = new ZoneCoord[acc.Count];
            acc.Keys.CopyTo(keys, 0);
            Array.Sort(keys, CompareZone);

            var result = new List<ZoneCell>(keys.Length);
            foreach (var k in keys)
            {
                var a = acc[k];
                result.Add(new ZoneCell(
                    coord: k,
                    regionCount: a.RegionCount,
                    totalTiles: a.TotalTiles,
                    totalPassable: a.TotalPassable,
                    totalFood: a.TotalFood,
                    totalObstacle: a.TotalObstacle,
                    hasAgent: a.HasAgent));
            }
            return result;
        }

        private static int CompareRegion(RegionCoord a, RegionCoord b)
        {
            int qc = a.Q.CompareTo(b.Q);
            return qc != 0 ? qc : a.R.CompareTo(b.R);
        }

        private static int CompareZone(ZoneCoord a, ZoneCoord b)
        {
            int qc = a.Q.CompareTo(b.Q);
            return qc != 0 ? qc : a.R.CompareTo(b.R);
        }

        // Mutable accumulators — local to projection, never escape this file.
        // Classes (not structs) because Dictionary-value mutation via out / TryGetValue
        // requires reference semantics to avoid the "modifying a copy" trap.
        private sealed class Accumulator
        {
            public int TileCount;
            public int PassableCount;
            public int FoodCount;
            public int ObstacleCount;
        }

        private sealed class ZoneAccumulator
        {
            public int RegionCount;
            public int TotalTiles;
            public int TotalPassable;
            public int TotalFood;
            public int TotalObstacle;
            public bool HasAgent;
        }
    }
}
