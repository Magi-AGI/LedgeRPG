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

        /// Test-only constructor for building a world with an explicit terrain
        /// layout and agent placement. Used by movement tests that need a
        /// known obstacle configuration rather than seeded-random terrain.
        /// Not public — the seeded constructor is the production path.
        internal LatticeWorld(int sizeX, int sizeY, int sizeZ, ToctaCoord agentPos, System.Collections.Generic.IEnumerable<ToctaCoord> blockedCoords)
        {
            if (sizeX <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
            if (sizeY <= 0) throw new ArgumentOutOfRangeException(nameof(sizeY));
            if (sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeZ));

            Seed = 0;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;

            int total = sizeX * sizeY * sizeZ;
            _cells = new Dictionary<ToctaCoord, ToctaType>(total);
            for (int y = 0; y < sizeY; y++)
                for (int x = 0; x < sizeX; x++)
                    for (int z = 0; z < sizeZ; z++)
                        _cells[new ToctaCoord(x, y, z)] = ToctaType.Passable;

            int blockedCount = 0;
            if (blockedCoords != null)
            {
                foreach (var b in blockedCoords)
                {
                    if (!_cells.ContainsKey(b))
                        throw new ArgumentException($"Blocked coord {b} is out of bounds.", nameof(blockedCoords));
                    if (_cells[b] == ToctaType.Blocked) continue;
                    _cells[b] = ToctaType.Blocked;
                    blockedCount++;
                }
            }
            BlockedCount = blockedCount;

            if (!_cells.ContainsKey(agentPos))
                throw new ArgumentException($"Agent pos {agentPos} is out of bounds.", nameof(agentPos));
            if (_cells[agentPos] != ToctaType.Passable)
                throw new ArgumentException($"Agent pos {agentPos} must be passable.", nameof(agentPos));
            AgentPos = agentPos;
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

        /// Scale-0 primitive step: move the agent to a face-adjacent target.
        /// Returns AgentMovedDelta on success, MovementBlockedDelta otherwise.
        /// The three rejection reasons (OutOfBounds, BlockedTerrain,
        /// NotFaceAdjacent) are surfaced separately so UI / AI layers can give
        /// the player different feedback without sniffing state.
        ///
        /// Higher-scale moves refine down to sequences of these primitives
        /// through ScaledLattice.Apply — LatticeWorld doesn't know about scales.
        public LatticeDelta TryStep(ToctaCoord target)
        {
            var from = AgentPos;
            if (!IsFaceAdjacent(from, target))
                return new MovementBlockedDelta(from, target, BlockReason.NotFaceAdjacent);
            if (!InBounds(target))
                return new MovementBlockedDelta(from, target, BlockReason.OutOfBounds);
            if (TypeAt(target) != ToctaType.Passable)
                return new MovementBlockedDelta(from, target, BlockReason.BlockedTerrain);

            AgentPos = target;
            return new AgentMovedDelta(from, target);
        }

        private static bool IsFaceAdjacent(ToctaCoord a, ToctaCoord b)
        {
            foreach (var n in ToctaNeighbors.FaceNeighbors(a))
                if (n.Equals(b)) return true;
            return false;
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
