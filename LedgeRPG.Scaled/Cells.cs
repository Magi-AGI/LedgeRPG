using System;

namespace LedgeRPG.Scaled
{
    /// Aggregated view of all scale-0 tiles inside one RegionCoord. Counts are
    /// invariant under reordering (that's the point of aggregation) and sum
    /// conservation is part of the projection contract: the sum of Passable /
    /// Food / Obstacle across every region equals the corresponding count on
    /// the source World.
    ///
    /// HasAgent is a degenerate aggregate — agent position is a single point,
    /// not a count — but surfaces here because higher-scale UIs need to know
    /// which region contains the agent without drilling back to scale 0.
    ///
    /// Visited-tile counts are intentionally out of scope for the spike: Core.World
    /// keeps the visited set private and the spike declines to widen that surface.
    /// Visited aggregation lands when we decide how the visited concept itself
    /// generalizes across scales (is "visited" even meaningful at scale 2?).
    public readonly struct RegionCell : IEquatable<RegionCell>
    {
        public RegionCoord Coord { get; }
        public int TileCount { get; }
        public int PassableCount { get; }
        public int FoodCount { get; }
        public int ObstacleCount { get; }
        public bool HasAgent { get; }

        public RegionCell(
            RegionCoord coord,
            int tileCount,
            int passableCount,
            int foodCount,
            int obstacleCount,
            bool hasAgent)
        {
            Coord = coord;
            TileCount = tileCount;
            PassableCount = passableCount;
            FoodCount = foodCount;
            ObstacleCount = obstacleCount;
            HasAgent = hasAgent;
        }

        public bool Equals(RegionCell other)
            => Coord.Equals(other.Coord)
               && TileCount == other.TileCount
               && PassableCount == other.PassableCount
               && FoodCount == other.FoodCount
               && ObstacleCount == other.ObstacleCount
               && HasAgent == other.HasAgent;

        public override bool Equals(object obj) => obj is RegionCell o && Equals(o);
        public override int GetHashCode() => Coord.GetHashCode();
        public override string ToString()
            => $"RegionCell({Coord}, tiles={TileCount}, food={FoodCount}, obs={ObstacleCount}{(HasAgent ? ", agent" : "")})";
    }

    /// Aggregated view of all scale-1 regions inside one ZoneCoord. Counts are
    /// sums across the contained regions — sum conservation across scale
    /// transitions is tested explicitly.
    public readonly struct ZoneCell : IEquatable<ZoneCell>
    {
        public ZoneCoord Coord { get; }
        public int RegionCount { get; }
        public int TotalTiles { get; }
        public int TotalPassable { get; }
        public int TotalFood { get; }
        public int TotalObstacle { get; }
        public bool HasAgent { get; }

        public ZoneCell(
            ZoneCoord coord,
            int regionCount,
            int totalTiles,
            int totalPassable,
            int totalFood,
            int totalObstacle,
            bool hasAgent)
        {
            Coord = coord;
            RegionCount = regionCount;
            TotalTiles = totalTiles;
            TotalPassable = totalPassable;
            TotalFood = totalFood;
            TotalObstacle = totalObstacle;
            HasAgent = hasAgent;
        }

        public bool Equals(ZoneCell other)
            => Coord.Equals(other.Coord)
               && RegionCount == other.RegionCount
               && TotalTiles == other.TotalTiles
               && TotalPassable == other.TotalPassable
               && TotalFood == other.TotalFood
               && TotalObstacle == other.TotalObstacle
               && HasAgent == other.HasAgent;

        public override bool Equals(object obj) => obj is ZoneCell o && Equals(o);
        public override int GetHashCode() => Coord.GetHashCode();
        public override string ToString()
            => $"ZoneCell({Coord}, regions={RegionCount}, food={TotalFood}, obs={TotalObstacle}{(HasAgent ? ", agent" : "")})";
    }
}
