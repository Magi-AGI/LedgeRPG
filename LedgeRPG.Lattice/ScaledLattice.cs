using System;
using System.Collections.Generic;

namespace LedgeRPG.Lattice
{
    /// 3D analogue of ScaledWorld: wraps a LatticeWorld as the scale-0 source
    /// of truth and caches projected aggregate layers at higher scales.
    ///
    /// Default scale factor is 10× linear per step — matches the Gods-Game
    /// 10-scale ladder's "room → solar system" intent (orders-of-magnitude
    /// per click of the zoom dial). 10× linear in BCC packing means ~680
    /// scale-0 toctas per scale-1 parent on average, not 15; 15 is the
    /// minimum-tessellation floor (1 center + 14 faces). The factor is
    /// parameterized so tests can exercise conservation at small factors.
    ///
    /// Cache strategy: single version counter, bumped by Invalidate().
    /// Scale layers are rebuilt lazily and fully on any mismatch. Same
    /// "good enough for a spike" tradeoff as ScaledWorld — a production
    /// implementation would want incremental delta projection once the
    /// scale-0 source gets large.
    public sealed class ScaledLattice
    {
        public const int DefaultScaleFactor = 10;
        public const int DefaultScaleCount = 3;

        public LatticeWorld Source { get; }
        public int ScaleFactor { get; }
        public int ScaleCount { get; }

        private long _version;
        private readonly long[] _cachedVersions;
        private readonly IReadOnlyDictionary<ToctaCoord, ToctaAggregate>[] _cachedLayers;

        public ScaledLattice(LatticeWorld source, int scaleFactor = DefaultScaleFactor, int scaleCount = DefaultScaleCount)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (scaleFactor <= 1) throw new ArgumentOutOfRangeException(nameof(scaleFactor),
                "Scale factor must be > 1.");
            if (scaleCount < 1) throw new ArgumentOutOfRangeException(nameof(scaleCount),
                "Scale count must be >= 1 (at least scale 0).");

            Source = source;
            ScaleFactor = scaleFactor;
            ScaleCount = scaleCount;

            // Indices 1..ScaleCount-1 hold aggregate layers; index 0 is unused
            // (scale 0 is the LatticeWorld itself, not an aggregate dictionary).
            _cachedVersions = new long[scaleCount];
            _cachedLayers = new IReadOnlyDictionary<ToctaCoord, ToctaAggregate>[scaleCount];
            for (int i = 0; i < scaleCount; i++) _cachedVersions[i] = -1;
        }

        public long Version => _version;

        /// Bump the version counter so all cached aggregate layers rebuild on
        /// next access. Call this after any mutation to the underlying
        /// LatticeWorld. The spike's LatticeWorld is currently immutable in
        /// its terrain, but AgentPos moves will eventually land here.
        public void Invalidate() => _version++;

        /// Get the aggregate layer at the given scale. Scale 0 returns null
        /// (use <see cref="Source"/> directly for scale-0 access). Scale 1+
        /// returns a dictionary keyed by parent ToctaCoord with rolled-up
        /// child/passable/blocked/agent data.
        public IReadOnlyDictionary<ToctaCoord, ToctaAggregate> GetScale(int scale)
        {
            if (scale < 0 || scale >= ScaleCount)
                throw new ArgumentOutOfRangeException(nameof(scale),
                    $"Scale must be in [0, {ScaleCount - 1}].");
            if (scale == 0) return null;

            if (_cachedVersions[scale] == _version && _cachedLayers[scale] != null)
                return _cachedLayers[scale];

            // Rebuild from the highest valid cached level we have, or from
            // scale 0 if nothing downstream is fresh. Simpler to just rebuild
            // the whole chain for the spike.
            IReadOnlyDictionary<ToctaCoord, ToctaAggregate> current =
                LatticeProjections.Project(Source, ScaleFactor);
            _cachedLayers[1] = current;
            _cachedVersions[1] = _version;

            for (int s = 2; s <= scale; s++)
            {
                current = LatticeProjections.Project(current, ScaleFactor);
                _cachedLayers[s] = current;
                _cachedVersions[s] = _version;
            }

            return _cachedLayers[scale];
        }
    }
}
