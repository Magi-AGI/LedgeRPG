namespace LedgeRPG.Lattice
{
    /// Aggregate of child toctas rolled up into a parent tocta at scale N+1.
    /// Conservation rule: sum of ChildCount across all aggregates at scale k+1
    /// equals TotalToctas at scale k. Passable/Blocked counts sum similarly.
    ///
    /// "Dominant type" is a display choice, not a truth claim — the authoritative
    /// terrain still lives at scale 0. Higher-scale views are projections.
    public sealed class ToctaAggregate
    {
        public ToctaCoord ParentCoord { get; }
        public int ChildCount { get; internal set; }
        public int PassableCount { get; internal set; }
        public int BlockedCount { get; internal set; }
        public bool HasAgent { get; internal set; }

        public ToctaAggregate(ToctaCoord parentCoord)
        {
            ParentCoord = parentCoord;
        }

        public ToctaType DominantType =>
            PassableCount >= BlockedCount ? ToctaType.Passable : ToctaType.Blocked;
    }
}
