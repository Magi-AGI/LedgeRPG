using System.Collections.Generic;
using LedgeRPG.Adapter;
using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;
using MagiGameServer.Contracts.Core;
using MagiGameServer.Contracts.Rules;
using Xunit;

namespace LedgeRPG.Adapter.Tests
{
    public class AdapterTests
    {
        private static readonly GameConfig DefaultConfig = new GameConfig
        {
            Seed = 42,
            SeatCount = 1,
            Options = new Dictionary<string, string>()
        };

        [Fact]
        public void ApplyLeavesInputStateUntouched()
        {
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var initial = (RPGState)module.CreateInitialState(DefaultConfig);

            int stepBefore = initial.Step;
            double energyBefore = initial.Energy;
            var posBefore = initial.AgentPos;

            adapter.Apply(initial, new RPGAction(RPGActionKind.Rest), out var advanced);

            Assert.Equal(stepBefore, initial.Step);
            Assert.Equal(energyBefore, initial.Energy);
            Assert.Equal(posBefore, initial.AgentPos);

            Assert.Equal(stepBefore + 1, advanced.Step);
        }

        [Fact]
        public void ApplyReturnsAppliedOnValidAction()
        {
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var initial = (RPGState)module.CreateInitialState(DefaultConfig);

            var outcome = adapter.Apply(initial, new RPGAction(RPGActionKind.Rest), out _);

            Assert.Equal(ApplyOutcome.Applied, outcome);
        }

        [Fact]
        public void ApplyReturnsRejectedOnUnknownEnumValue()
        {
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var initial = (RPGState)module.CreateInitialState(DefaultConfig);

            var outcome = adapter.Apply(
                initial,
                new RPGAction((RPGActionKind)999),
                out var unchanged);

            Assert.Equal(ApplyOutcome.Rejected, outcome);
            Assert.Same(initial, unchanged);
        }

        [Fact]
        public void ApplyReturnsRejectedOnNullAction()
        {
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var initial = (RPGState)module.CreateInitialState(DefaultConfig);

            var outcome = adapter.Apply(initial, null, out var unchanged);

            Assert.Equal(ApplyOutcome.Rejected, outcome);
            Assert.Same(initial, unchanged);
        }

        [Fact]
        public void GetStateHashIsStableAcrossCalls()
        {
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var state = (RPGState)module.CreateInitialState(DefaultConfig);

            long h1 = adapter.GetStateHash(state);
            long h2 = adapter.GetStateHash(state);
            Assert.Equal(h1, h2);
        }

        [Fact]
        public void GetStateHashDiffersAfterApply()
        {
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var initial = (RPGState)module.CreateInitialState(DefaultConfig);

            long initialHash = adapter.GetStateHash(initial);
            adapter.Apply(initial, new RPGAction(RPGActionKind.Rest), out var advanced);
            long advancedHash = adapter.GetStateHash(advanced);

            Assert.NotEqual(initialHash, advancedHash);
        }

        [Fact]
        public void GetStateHashMatchesAcrossTwoIdenticallySeededInitials()
        {
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var a = (RPGState)module.CreateInitialState(DefaultConfig);
            var b = (RPGState)module.CreateInitialState(DefaultConfig);

            Assert.Equal(adapter.GetStateHash(a), adapter.GetStateHash(b));
        }

        [Fact]
        public void ProjectStateForReturnsIdentityInV0()
        {
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var state = (RPGState)module.CreateInitialState(DefaultConfig);

            var projected = adapter.ProjectStateFor(state, new SeatId(0));

            Assert.Same(state, projected);
        }

        [Fact]
        public void GetStateHashForSeatEqualsGetStateHashInV0()
        {
            // Default RulesAdapterBase.GetStateHashForSeat implementation is
            // GetStateHash(ProjectStateFor(state, seat)) — in V0 projection is
            // identity so the two should agree exactly.
            var module = new LedgeRPGGameModule();
            var adapter = (IRulesAdapter<RPGState, RPGAction>)module.Rules;
            var state = (RPGState)module.CreateInitialState(DefaultConfig);

            long canonical = adapter.GetStateHash(state);
            long seatView = adapter.GetStateHashForSeat(state, new SeatId(0));

            Assert.Equal(canonical, seatView);
        }

        [Fact]
        public void NonGenericApplyDelegatesToTypedApply()
        {
            // Sanity-check the object-shape bridge in RulesAdapterBase: the
            // server registry holds IRulesAdapter (non-generic) and we need
            // that path to round-trip correctly.
            var module = new LedgeRPGGameModule();
            IRulesAdapter nonGeneric = module.Rules;
            object state = module.CreateInitialState(DefaultConfig);
            object action = new RPGAction(RPGActionKind.Rest);

            var outcome = nonGeneric.Apply(state, action, out var advanced);

            Assert.Equal(ApplyOutcome.Applied, outcome);
            Assert.IsType<RPGState>(advanced);
            Assert.Equal(1, ((RPGState)advanced).Step);
        }
    }
}
