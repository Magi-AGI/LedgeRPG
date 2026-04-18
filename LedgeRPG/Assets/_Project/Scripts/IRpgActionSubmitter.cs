using System.Collections.Generic;
using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;

namespace Magi.LedgeRPG
{
    /// Seam between scene input and the authoritative rules evaluator.
    /// V0 resolves actions locally against an in-process World; M4 will
    /// swap in a networked implementation that routes through MagiGameServer.
    public interface IRpgActionSubmitter
    {
        /// Authoritative world state visible to the scene. For the local
        /// implementation this is the owned World; for the networked one it
        /// is the client-reconciled copy.
        World World { get; }

        /// Submit an action and return the resulting deltas. Bootstrap
        /// treats the call as synchronous; a transport implementation can
        /// block on a round-trip or queue/drain against a predicted World.
        IReadOnlyList<StateDelta> Submit(RPGActionKind action);
    }
}
