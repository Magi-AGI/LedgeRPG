using System;
using System.Collections.Generic;
using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;

namespace LedgeRPG.Scaled
{
    /// Wraps a Core.World as the scale-0 source of truth and exposes projected
    /// views at scale 1 and 2. The spike's load-bearing claim: one authoritative
    /// state with N views, where actions at any scale ultimately project down
    /// to scale-0 mutations.
    ///
    /// View caching: simple version-counter invalidation. <see cref="Apply"/>
    /// and <see cref="ApplyScale1"/> bump the version; cached projections rebuild
    /// on next access. Good enough for a correctness spike; a production design
    /// would want delta-driven incremental projection instead of full rebuilds
    /// once grids get large (the user flagged scale-induced size as a real
    /// perf concern).
    public sealed class ScaledWorld
    {
        /// How many scale-0 hex tiles along each axis make up one region.
        /// Default 4 yields a 2-region-wide scale-1 view for the paper-mirror's
        /// 8×8 grid — giving us two cross-region rows to exercise aggregation.
        public const int DefaultRegionSize = 4;

        /// How many scale-1 regions along each axis make up one zone.
        /// Default 2 yields one zone covering the whole paper-mirror grid —
        /// trivial but exercises the scale-1→scale-2 composition cleanly.
        public const int DefaultZoneSize = 2;

        public World Source { get; }
        public int RegionSize { get; }
        public int ZoneSize { get; }

        private long _version;
        private long _cachedRegionsVersion = -1;
        private IReadOnlyList<RegionCell> _cachedRegions;
        private long _cachedZonesVersion = -1;
        private IReadOnlyList<ZoneCell> _cachedZones;

        public ScaledWorld(World source, int regionSize = DefaultRegionSize, int zoneSize = DefaultZoneSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (regionSize <= 0) throw new ArgumentOutOfRangeException(nameof(regionSize));
            if (zoneSize <= 0) throw new ArgumentOutOfRangeException(nameof(zoneSize));

            Source = source;
            RegionSize = regionSize;
            ZoneSize = zoneSize;
        }

        /// Monotonically increasing version counter. Every mutating call bumps
        /// this; projections keyed to the current version are guaranteed fresh.
        /// Exposed for tests that want to assert "views rebuilt after apply".
        public long Version => _version;

        public IReadOnlyList<RegionCell> GetRegions()
        {
            if (_cachedRegionsVersion != _version)
            {
                _cachedRegions = Projections.Scale0To1(Source, RegionSize);
                _cachedRegionsVersion = _version;
            }
            return _cachedRegions;
        }

        public IReadOnlyList<ZoneCell> GetZones()
        {
            if (_cachedZonesVersion != _version)
            {
                _cachedZones = Projections.Scale1To2(GetRegions(), ZoneSize);
                _cachedZonesVersion = _version;
            }
            return _cachedZones;
        }

        /// Apply a scale-0 primitive action to the source World and invalidate
        /// projected views. Returns the deltas Core emitted so callers can wire
        /// UI updates without re-snapshotting.
        public IReadOnlyList<StateDelta> Apply(RPGActionKind action)
        {
            var deltas = Source.ApplyAction(action);
            _version++;
            return deltas;
        }

        /// Apply a scale-1 action by refining it into a scale-0 sequence and
        /// running each primitive through the source. All emitted deltas are
        /// returned in order; one version bump per call (not per primitive)
        /// because the scale-1 action is the semantic unit.
        ///
        /// Rejections from Core surface as empty deltas in the returned list —
        /// the refinement model doesn't short-circuit on invalid primitives
        /// because "obstacle blocked me mid-sweep" is a legitimate outcome,
        /// not an error. Callers inspect deltas to see what actually happened.
        public IReadOnlyList<StateDelta> ApplyScale1(Scale1Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var refined = ActionRefinement.Refine(action);
            var all = new List<StateDelta>();
            foreach (var primitive in refined)
            {
                var deltas = Source.ApplyAction(primitive);
                all.AddRange(deltas);
                // Core returns empty delta list for rejected actions (it throws
                // InvalidActionException for unknown enums, but valid-enum-but-
                // blocked-at-runtime returns MovementBlockedDelta at most).
                // No short-circuit.
            }
            _version++;
            return all;
        }
    }
}
