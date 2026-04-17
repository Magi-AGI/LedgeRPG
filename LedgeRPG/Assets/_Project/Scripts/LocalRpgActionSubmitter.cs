using System.Collections.Generic;
using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;

namespace Magi.LedgeRPG
{
    /// V0 submitter: applies actions directly against an in-process World.
    /// No network, no reconciliation, no prediction.
    public sealed class LocalRpgActionSubmitter : IRpgActionSubmitter
    {
        public World World { get; }

        public LocalRpgActionSubmitter(World world)
        {
            World = world;
        }

        public IReadOnlyList<StateDelta> Submit(RPGActionKind action)
            => World.ApplyAction(action);
    }
}
