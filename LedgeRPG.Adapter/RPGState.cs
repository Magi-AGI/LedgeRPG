using System.Collections.Generic;
using LedgeRPG.Core.World;

namespace LedgeRPG.Adapter
{
    /// Immutable snapshot of an RPG episode's canonical state. Wraps a Core
    /// World without exposing its mutable surface — consumers can read
    /// everything they need, but cannot advance state without going through
    /// the rules adapter. The adapter's Apply path clones the underlying
    /// World, mutates the clone, and wraps the result in a new RPGState;
    /// the caller's original snapshot is never touched.
    ///
    /// Class (not struct / record struct) because RulesAdapterBase constrains
    /// TState : class. Sealed to keep the projection contract
    /// (ProjectStateFor returns a state of the same runtime shape) easy to
    /// reason about — no subclass-dependent projection behavior to chase.
    public sealed class RPGState
    {
        /// The Core world that backs this snapshot. Exposed at internal
        /// visibility so the adapter assembly can clone/advance it; external
        /// callers read state through the public accessors below. Not
        /// `readonly` because record-style copy-with would need a new field,
        /// and we haven't yet needed that — if that changes, flip it.
        internal World World { get; }

        internal RPGState(World world)
        {
            World = world;
        }

        // Public read-only surface. Mirrors World's properties exactly for
        // V0; when hidden-info projection arrives in V1+, these accessors
        // will return the seat-specific projected view rather than raw canon.

        public long Seed => World.Seed;
        public int GridSize => World.GridSize;
        public int FoodCount => World.FoodCount;
        public int ObstacleCount => World.ObstacleCount;
        public int StepLimit => World.StepLimit;

        public int Step => World.Step;
        public double Energy => World.Energy;
        public bool Done => World.Done;
        public bool Success => World.Success;
        public TerminalReason TerminalReason => World.TerminalReason;

        public HexCoord AgentPos => World.AgentPos;
        public int FoodRemaining => World.FoodRemaining;
        public int VisitedCount => World.VisitedCount;
        public int TotalPassable => World.TotalPassable;

        public TileType TileAt(HexCoord c) => World.TileAt(c);
        public IReadOnlyList<GridCell> Grid => World.GridSnapshot();
        public Goals Goals => World.Goals;
    }
}
