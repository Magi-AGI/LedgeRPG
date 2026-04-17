using System.Collections.Generic;
using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;
using Xunit;

namespace LedgeRPG.Core.Tests
{
    public class DeterminismTests
    {
        // Mirrors the Python server's seed-stability gate: two World instances
        // seeded identically must produce identical state deltas for identical
        // action sequences. C#-local determinism only — cross-language parity
        // with the Python server is intentionally not asserted (see SplitMix64Rng).
        [Fact]
        public void TwoWorldsSameSeedProduceIdenticalTraces()
        {
            var actions = new[]
            {
                RPGActionKind.MoveN, RPGActionKind.MoveS, RPGActionKind.Examine,
                RPGActionKind.Rest, RPGActionKind.MoveNE, RPGActionKind.MoveSW,
                RPGActionKind.MoveSE, RPGActionKind.MoveNW, RPGActionKind.Rest,
                RPGActionKind.Examine
            };

            var traceA = RunTrace(seed: 42, actions);
            var traceB = RunTrace(seed: 42, actions);

            Assert.Equal(traceA.Count, traceB.Count);
            for (int i = 0; i < traceA.Count; i++)
                Assert.Equal(traceA[i], traceB[i]);
        }

        [Fact]
        public void DifferentSeedsProduceDifferentWorlds()
        {
            var w1 = new World.World(seed: 42);
            var w2 = new World.World(seed: 43);
            // Seed 42 and 43 should land the agent in different starting positions
            // almost certainly — if this ever fires, we've picked unlucky seeds, not
            // a bug, and the test can move to different inputs.
            Assert.NotEqual(w1.AgentPos, w2.AgentPos);
        }

        [Fact]
        public void ActionsListCoversAllEightKinds()
        {
            // Sanity: the canonical list hasn't accidentally dropped an action.
            Assert.Equal(8, RPGActions.All.Count);
            Assert.Contains(RPGActionKind.MoveN, RPGActions.All);
            Assert.Contains(RPGActionKind.MoveNW, RPGActions.All);
            Assert.Contains(RPGActionKind.Examine, RPGActions.All);
            Assert.Contains(RPGActionKind.Rest, RPGActions.All);
        }

        [Fact]
        public void WireNameRoundTripsForAllActions()
        {
            foreach (var kind in RPGActions.All)
            {
                var wire = RPGActions.ToWireName(kind);
                Assert.True(RPGActions.TryParse(wire, out var parsed));
                Assert.Equal(kind, parsed);
            }
        }

        private static List<StateDelta> RunTrace(int seed, RPGActionKind[] actions)
        {
            var world = new World.World(seed);
            var trace = new List<StateDelta>();
            foreach (var action in actions)
            {
                if (world.Done) break;
                foreach (var delta in world.ApplyAction(action))
                    trace.Add(delta);
            }
            return trace;
        }
    }
}
