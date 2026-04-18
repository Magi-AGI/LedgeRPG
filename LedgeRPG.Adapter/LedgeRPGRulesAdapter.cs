using System;
using LedgeRPG.Core;
using LedgeRPG.Core.World;
using MagiGameServer.Contracts.Core;
using MagiGameServer.Contracts.Rules;

namespace LedgeRPG.Adapter
{
    /// Binds LedgeRPG.Core to the shared MagiGameServer framework. Stateless
    /// and thread-safe per the IGameModule contract — per-session state lives
    /// entirely inside RPGState's wrapped World, never on the adapter.
    public sealed class LedgeRPGRulesAdapter : RulesAdapterBase<RPGState, RPGAction>
    {
        public override ApplyOutcome Apply(RPGState state, RPGAction action, out RPGState newState)
        {
            // Defensive against null — the framework's non-generic bridge casts
            // object→RPGAction and could land here with a null if a client
            // submitted an unrecognized wire name that FromWireName couldn't
            // parse. Surface as Rejected rather than NRE-ing inside the rules.
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (action == null)
            {
                newState = state;
                return ApplyOutcome.Rejected;
            }

            // Copy-on-apply: Core stays mutate-in-place (faithful Python port),
            // the adapter clones so the caller's snapshot is untouched. Matches
            // the IRulesAdapter contract's "produced only from (state, action)"
            // purity mandate without forcing immutability into Core.
            World advanced = state.World.Clone();
            try
            {
                advanced.ApplyAction(action.Kind);
            }
            catch (InvalidActionException)
            {
                // Rules refused (bogus enum, or action after termination).
                // Return the unchanged snapshot so the client can reconcile
                // against an authoritative no-op.
                newState = state;
                return ApplyOutcome.Rejected;
            }

            newState = new RPGState(advanced);
            return ApplyOutcome.Applied;
        }

        public override long GetStateHash(RPGState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            // FNV-1a 64-bit over canonical-ordered fields. Goals / derived
            // counts (VisitedCount, TotalPassable) are NOT hashed — they're
            // functions of the visited set and grid which are hashed. Energy
            // is quantized to 1e-9 to avoid float-representation drift across
            // runs; 1e-9 is far below the 0.05 per-move step size so no
            // legitimate state pair collides under quantization.
            ulong h = 14695981039346656037UL;
            h = FnvMix(h, unchecked((ulong)state.Seed));
            h = FnvMix(h, (ulong)state.GridSize);
            h = FnvMix(h, (ulong)state.FoodCount);
            h = FnvMix(h, (ulong)state.ObstacleCount);
            h = FnvMix(h, (ulong)state.StepLimit);
            h = FnvMix(h, (ulong)state.Step);
            h = FnvMix(h, QuantizeEnergy(state.Energy));
            h = FnvMix(h, state.Done ? 1UL : 0UL);
            h = FnvMix(h, state.Success ? 1UL : 0UL);
            h = FnvMix(h, (ulong)state.TerminalReason);
            h = FnvMix(h, unchecked((ulong)(long)state.AgentPos.Q));
            h = FnvMix(h, unchecked((ulong)(long)state.AgentPos.R));
            h = FnvMix(h, (ulong)state.FoodRemaining);

            // Grid iterated in canonical sorted order (GridSnapshot sorts by Q, R).
            foreach (var cell in state.Grid)
            {
                h = FnvMix(h, unchecked((ulong)(long)cell.Coord.Q));
                h = FnvMix(h, unchecked((ulong)(long)cell.Coord.R));
                h = FnvMix(h, (ulong)cell.Type);
            }
            return unchecked((long)h);
        }

        public override RPGState SnapshotState(RPGState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            // Identity snapshot is safe here: Apply clones state.World before
            // mutating and wraps the clone in a new RPGState, so any RPGState
            // Session stashes in its takeback log is never mutated after the
            // fact. If RPGState ever exposes a mutable collection directly,
            // switch this to a deep clone.
            return state;
        }

        public override RPGState ProjectStateFor(RPGState state, SeatId seat)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            // V0 is single-seat, no hidden info — LedgeRPG's paper rules have
            // a fully observable world for the one agent playing. Identity
            // projection is correct here.
            //
            // V1+ will need per-seat filtering for:
            //   - private inventory (each character sees own bag only)
            //   - private stats (HP/MP/status derived from inventory + class)
            //   - quest state (per-character quest log entries)
            // When that lands, this method materializes a new RPGState whose
            // World is built from a projected grid/inventory snapshot rather
            // than handed through unchanged. Hide behind a seat-projection
            // helper on RPGState when the time comes.
            _ = seat;
            return state;
        }

        private static ulong FnvMix(ulong hash, ulong value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 1099511628211UL;
            }
        }

        private static ulong QuantizeEnergy(double energy)
        {
            // Multiply-and-round to a fixed 1e-9 grid; avoids hash churn from
            // low-bit float noise without collapsing the 0.05 step resolution.
            double scaled = energy * 1_000_000_000.0;
            long rounded = (long)System.Math.Round(scaled);
            return unchecked((ulong)rounded);
        }
    }
}
