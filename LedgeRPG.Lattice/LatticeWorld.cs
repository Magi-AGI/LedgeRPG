using System;
using System.Collections.Generic;

namespace LedgeRPG.Lattice
{
    public enum ToctaType
    {
        Passable = 0,
        Blocked = 1,
    }

    /// Synthetic scale-0 source-of-truth for the Lattice spike. A bounded
    /// 3D block of toctas with seeded random terrain (Passable / Blocked)
    /// and a single agent placed on a passable cell.
    ///
    /// Explicitly NOT coupled to Core.World — the 2D paper-mirror keeps
    /// running on its hex substrate; this is a parallel architecture-only
    /// experiment. When/if the volumetric substrate promotes out of spike,
    /// that's when we revisit whether it replaces or composes with Core.
    ///
    /// PRNG note: System.Random is used for the spike's simplicity. If the
    /// Lattice work graduates, swap to the SplitMix64 in Core.Determinism
    /// so seeds stay stable across platforms and runtimes.
    public sealed class LatticeWorld
    {
        public long Seed { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }
        public int BlockedCount { get; }

        public ToctaCoord AgentPos { get; private set; }

        private readonly Dictionary<ToctaCoord, ToctaType> _cells;

        public LatticeWorld(long seed, int sizeX, int sizeY, int sizeZ, int blockedCount)
        {
            if (sizeX <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
            if (sizeY <= 0) throw new ArgumentOutOfRangeException(nameof(sizeY));
            if (sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeZ));
            if (blockedCount < 0) throw new ArgumentOutOfRangeException(nameof(blockedCount));
            int total = sizeX * sizeY * sizeZ;
            if (blockedCount >= total)
                throw new ArgumentOutOfRangeException(nameof(blockedCount),
                    "Must leave at least one passable tocta for the agent.");

            Seed = seed;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            BlockedCount = blockedCount;

            _cells = new Dictionary<ToctaCoord, ToctaType>(total);
            for (int y = 0; y < sizeY; y++)
                for (int x = 0; x < sizeX; x++)
                    for (int z = 0; z < sizeZ; z++)
                        _cells[new ToctaCoord(x, y, z)] = ToctaType.Passable;

            var rng = new Random(unchecked((int)seed));
            var allCoords = new List<ToctaCoord>(_cells.Keys);
            Shuffle(allCoords, rng);

            for (int i = 0; i < blockedCount; i++)
                _cells[allCoords[i]] = ToctaType.Blocked;

            // Place agent on the next passable coord after the blocked prefix.
            AgentPos = allCoords[blockedCount];
        }

        public bool InBounds(ToctaCoord c) =>
            c.X >= 0 && c.X < SizeX &&
            c.Y >= 0 && c.Y < SizeY &&
            c.Z >= 0 && c.Z < SizeZ;

        public ToctaType TypeAt(ToctaCoord c) =>
            _cells.TryGetValue(c, out var t) ? t : ToctaType.Blocked;

        public int TotalToctas => _cells.Count;
        public int PassableCount => _cells.Count - BlockedCount;

        /// All in-bounds coords in deterministic enumeration order
        /// (Y-major, then X, then Z). Exposed so callers that need to scan
        /// the whole world (projections, rendering prep) can iterate without
        /// touching the private dictionary.
        public IEnumerable<ToctaCoord> AllCoords()
        {
            for (int y = 0; y < SizeY; y++)
                for (int x = 0; x < SizeX; x++)
                    for (int z = 0; z < SizeZ; z++)
                        yield return new ToctaCoord(x, y, z);
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
