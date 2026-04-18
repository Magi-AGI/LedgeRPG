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

        /// Apply a scale-N face-traversal. At scale 0 this is a single primitive
        /// step via <see cref="LatticeWorld.TryStep"/>. At higher scales the
        /// agent walks scale-0-step by scale-0-step toward the target scale-N
        /// parent's center, using a greedy closest-neighbor rule: at each step,
        /// pick the passable face-neighbor whose world position is closest to
        /// the target center, terminating when the agent's scale-N parent
        /// equals the target (success) or no neighbor makes progress (blocked).
        ///
        /// The greedy walk is deliberately simple: the architectural claim under
        /// test is that a (scale, faceIndex) action at any scale decomposes into
        /// scale-0 primitives, not that the path is optimal. A-star or similar
        /// is a Phase 3 problem if we find the greedy rule gets stuck too often
        /// in realistic terrain.
        ///
        /// Invalidates the aggregate cache once at the end (not per primitive);
        /// the action is the semantic unit even when it fans out into many
        /// scale-0 steps, mirroring ScaledWorld.ApplyScale1's contract.
        public IReadOnlyList<LatticeDelta> Apply(LatticeAction action)
        {
            if (action.Scale >= ScaleCount)
                throw new ArgumentOutOfRangeException(nameof(action),
                    $"Action scale {action.Scale} exceeds ScaleCount {ScaleCount}.");

            var deltas = new List<LatticeDelta>();

            var agentParent = LatticeProjections.ParentAt(Source.AgentPos, action.Scale, ScaleFactor);
            ToctaCoord targetParent = default;
            int idx = 0;
            foreach (var n in ToctaNeighbors.FaceNeighbors(agentParent))
            {
                if (idx == action.FaceIndex) { targetParent = n; break; }
                idx++;
            }

            if (action.Scale == 0)
            {
                deltas.Add(Source.TryStep(targetParent));
                Invalidate();
                return deltas;
            }

            // Target's world position in scale-0 edge units.
            var (twx, twy, twz) = targetParent.WorldPosition;
            double scaleMult = Math.Pow(ScaleFactor, action.Scale);
            double targetWx = twx * scaleMult;
            double targetWy = twy * scaleMult;
            double targetWz = twz * scaleMult;

            // Generous cap: BCC nearest-neighbor path between parent centers
            // can take ~factor^scale steps; 4× that is enough slack for
            // obstacle avoidance while still catching runaway loops.
            int maxSteps = 4 * (int)scaleMult + 4;
            for (int step = 0; step < maxSteps; step++)
            {
                var currentParent = LatticeProjections.ParentAt(Source.AgentPos, action.Scale, ScaleFactor);
                if (currentParent.Equals(targetParent))
                {
                    Invalidate();
                    return deltas;
                }

                double currentDistSq = DistSq(Source.AgentPos, targetWx, targetWy, targetWz);
                ToctaCoord bestStep = default;
                double bestDistSq = double.PositiveInfinity;

                foreach (var candidate in ToctaNeighbors.FaceNeighbors(Source.AgentPos))
                {
                    if (!Source.InBounds(candidate)) continue;
                    if (Source.TypeAt(candidate) != ToctaType.Passable) continue;
                    double d = DistSq(candidate, targetWx, targetWy, targetWz);
                    if (d < bestDistSq) { bestDistSq = d; bestStep = candidate; }
                }

                if (bestDistSq >= currentDistSq)
                {
                    deltas.Add(new MovementBlockedDelta(Source.AgentPos, targetParent, BlockReason.BlockedTerrain));
                    Invalidate();
                    return deltas;
                }

                var stepDelta = Source.TryStep(bestStep);
                deltas.Add(stepDelta);
                if (!(stepDelta is AgentMovedDelta))
                {
                    Invalidate();
                    return deltas;
                }
            }

            deltas.Add(new MovementBlockedDelta(Source.AgentPos, targetParent, BlockReason.BlockedTerrain));
            Invalidate();
            return deltas;
        }

        private static double DistSq(ToctaCoord c, double tx, double ty, double tz)
        {
            var (wx, wy, wz) = c.WorldPosition;
            double dx = wx - tx, dy = wy - ty, dz = wz - tz;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
