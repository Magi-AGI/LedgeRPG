namespace LedgeRPG.Scaled
{
    /// Which coarseness level a view, coord, or action lives at. Scale0 is
    /// the finest resolution (individual hex tiles, matches Core.World); each
    /// subsequent scale aggregates <c>ScaledWorld.RegionSize</c> units along
    /// each axis into one coarser unit.
    ///
    /// Spike scope (2026-04-18): only Scale0..Scale2 populated. The full
    /// Gods-Game vision targets 10 discrete scales; we're validating that
    /// the projection + refinement shape holds across two aggregation steps
    /// before committing to the full ladder.
    public enum ScaleLevel
    {
        Scale0 = 0,
        Scale1 = 1,
        Scale2 = 2,
    }
}
