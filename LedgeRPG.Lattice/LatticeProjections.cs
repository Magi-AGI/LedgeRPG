using System;
using System.Collections.Generic;

namespace LedgeRPG.Lattice
{
    /// Voronoi-to-parent aggregation across scales. Each scale-N tocta maps
    /// to the nearest scale-(N+1) BCC lattice point in world space, and
    /// aggregates accumulate child counts / passable / blocked / agent flags
    /// along the chain.
    ///
    /// BCC "nearest point" is resolved by checking a small candidate set of
    /// parent coords around the naive rounding — the lattice alternates
    /// parity between layers, so one nearest-even-Y and one nearest-odd-Y
    /// candidate must both be considered (plus a neighbor to cover edge
    /// cases where the child is nearly equidistant between Y layers).
    ///
    /// Truncated octahedra do NOT tile recursively — a scale-1 tocta is
    /// not an exact union of scale-0 toctas. We accept the small Voronoi
    /// discrepancy in exchange for a clean, deterministic rule that
    /// generalizes to any scale and any scale factor. This is the
    /// "approximate aggregation" the design deliberately chose.
    public static class LatticeProjections
    {
        /// Find the scale-(N+1) ToctaCoord whose world-position * parentScaleFactor
        /// is nearest to the given child coord's world position.
        public static ToctaCoord NearestParent(ToctaCoord child, int parentScaleFactor)
        {
            if (parentScaleFactor <= 1)
                throw new ArgumentOutOfRangeException(nameof(parentScaleFactor),
                    "Parent scale factor must be > 1.");

            var (wx, wy, wz) = child.WorldPosition;
            return NearestParent(wx, wy, wz, parentScaleFactor);
        }

        /// Nearest scale-(N+1) parent for an arbitrary world-space point.
        /// Used by both the scale-0 projection and the recursive higher-scale
        /// projection (where the "point" is a lower-scale aggregate's parent
        /// coord treated as a world position in the next-level-up's units).
        public static ToctaCoord NearestParent(double wx, double wy, double wz, int parentScaleFactor)
        {
            double invF = 1.0 / parentScaleFactor;

            // Parent Y expressed in half-layer units (Y axis is 0.5 per integer step).
            double yHalfLayers = 2.0 * wy * invF;
            int yCenter = (int)Math.Round(yHalfLayers);

            ToctaCoord best = default;
            double bestSq = double.PositiveInfinity;

            for (int dy = -1; dy <= 1; dy++)
            {
                int y = yCenter + dy;
                double off = (((y % 2) + 2) % 2 == 1) ? 0.5 : 0.0;
                int x = (int)Math.Round(wx * invF - off);
                int z = (int)Math.Round(wz * invF - off);

                var cand = new ToctaCoord(x, y, z);
                var (cwx, cwy, cwz) = cand.WorldPosition;
                double dwx = wx - parentScaleFactor * cwx;
                double dwy = wy - parentScaleFactor * cwy;
                double dwz = wz - parentScaleFactor * cwz;
                double sq = dwx * dwx + dwy * dwy + dwz * dwz;
                if (sq < bestSq) { bestSq = sq; best = cand; }
            }
            return best;
        }

        /// Walk the nearest-parent chain <paramref name="scale"/> times to find
        /// the scale-N parent coord of a given scale-0 coord. Used by movement
        /// code to resolve "which scale-N parent is the agent currently in?"
        /// without scanning the whole aggregate dict.
        ///
        /// Scale 0 returns the input unchanged. Each step applies the same
        /// parentScaleFactor — the per-level factor is uniform in this spike.
        public static ToctaCoord ParentAt(ToctaCoord scale0Coord, int scale, int parentScaleFactor)
        {
            if (scale < 0) throw new ArgumentOutOfRangeException(nameof(scale));
            var c = scale0Coord;
            for (int i = 0; i < scale; i++)
                c = NearestParent(c, parentScaleFactor);
            return c;
        }

        /// Project a scale-0 LatticeWorld up one scale. Each scale-0 cell
        /// contributes to exactly one scale-1 aggregate.
        public static IReadOnlyDictionary<ToctaCoord, ToctaAggregate> Project(
            LatticeWorld source, int parentScaleFactor)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = new Dictionary<ToctaCoord, ToctaAggregate>();
            foreach (var c in source.AllCoords())
            {
                var parent = NearestParent(c, parentScaleFactor);
                if (!result.TryGetValue(parent, out var agg))
                {
                    agg = new ToctaAggregate(parent);
                    result[parent] = agg;
                }
                agg.ChildCount++;
                if (source.TypeAt(c) == ToctaType.Passable) agg.PassableCount++;
                else agg.BlockedCount++;
                if (source.AgentPos.Equals(c)) agg.HasAgent = true;
            }
            return result;
        }

        /// Project an existing aggregate layer up one more scale. Child counts,
        /// passable/blocked counts, and agent flags chain through additively,
        /// so conservation is preserved across arbitrary scale chains.
        public static IReadOnlyDictionary<ToctaCoord, ToctaAggregate> Project(
            IReadOnlyDictionary<ToctaCoord, ToctaAggregate> lower, int parentScaleFactor)
        {
            if (lower == null) throw new ArgumentNullException(nameof(lower));

            var result = new Dictionary<ToctaCoord, ToctaAggregate>();
            foreach (var kv in lower)
            {
                var parent = NearestParent(kv.Key, parentScaleFactor);
                if (!result.TryGetValue(parent, out var agg))
                {
                    agg = new ToctaAggregate(parent);
                    result[parent] = agg;
                }
                agg.ChildCount += kv.Value.ChildCount;
                agg.PassableCount += kv.Value.PassableCount;
                agg.BlockedCount += kv.Value.BlockedCount;
                if (kv.Value.HasAgent) agg.HasAgent = true;
            }
            return result;
        }
    }
}
