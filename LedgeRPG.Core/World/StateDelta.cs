namespace LedgeRPG.Core.World
{
    /// Discriminated union of state-change events emitted by World.ApplyAction.
    /// The adapter (M2) translates these into the wire JSON shape that matches
    /// the HERMES MVP spec; Core stays wire-agnostic so the same record types
    /// can serve alternative serializers later without rewriting rules.
    public abstract record StateDelta;

    /// Emitted when a move would leave the grid or step onto an obstacle.
    /// Always paired with a same-step Position delta whose From equals To,
    /// matching the Python server's blocked-move semantics.
    public sealed record MovementBlockedDelta(string Direction, HexCoord At) : StateDelta;

    /// Agent position change. For a blocked move, From == To — clients that
    /// ignore MovementBlocked still see a no-op position update on that step.
    public sealed record PositionDelta(HexCoord From, HexCoord To) : StateDelta;

    /// Energy change. Delta is signed — move cost is negative, food consumption
    /// is positive. From and To let a client reconstruct the trajectory without
    /// tracking running totals of deltas.
    public sealed record EnergyDelta(double Delta, double From, double To) : StateDelta;

    /// First-time visit to a tile; drives EXPLORATION_INCENTIVE progress.
    public sealed record TileDiscoveredDelta(HexCoord At) : StateDelta;

    /// Food tile consumed via Examine; the tile flips to Empty and FoodRemaining
    /// decrements. Always paired with an EnergyDelta that restores energy to 1.0.
    public sealed record FoodConsumedDelta(HexCoord At) : StateDelta;
}
