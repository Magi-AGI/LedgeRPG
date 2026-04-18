namespace LedgeRPG.Lattice
{
    /// Discriminated delta describing what happened when a LatticeAction applied.
    /// Mirrors the StateDelta pattern in LedgeRPG.Core — callers pattern-match to
    /// react (update UI, log, etc.) without re-snapshotting the whole world.
    public abstract class LatticeDelta { }

    /// Agent moved from <see cref="From"/> to <see cref="To"/> — both coords are
    /// face-adjacent scale-0 toctas. A scale-N action emits a sequence of these.
    public sealed class AgentMovedDelta : LatticeDelta
    {
        public ToctaCoord From { get; }
        public ToctaCoord To { get; }

        public AgentMovedDelta(ToctaCoord from, ToctaCoord to)
        {
            From = from;
            To = to;
        }

        public override string ToString() => $"Moved {From}->{To}";
    }

    /// Movement was rejected before applying. <see cref="Reason"/> discriminates
    /// between "out of bounds", "blocked terrain", and "target not face-adjacent"
    /// so callers can present different feedback without parsing strings.
    public sealed class MovementBlockedDelta : LatticeDelta
    {
        public ToctaCoord AttemptedFrom { get; }
        public ToctaCoord AttemptedTo { get; }
        public BlockReason Reason { get; }

        public MovementBlockedDelta(ToctaCoord attemptedFrom, ToctaCoord attemptedTo, BlockReason reason)
        {
            AttemptedFrom = attemptedFrom;
            AttemptedTo = attemptedTo;
            Reason = reason;
        }

        public override string ToString() => $"Blocked {AttemptedFrom}->{AttemptedTo} ({Reason})";
    }

    public enum BlockReason
    {
        OutOfBounds = 0,
        BlockedTerrain = 1,
        NotFaceAdjacent = 2,
    }
}
