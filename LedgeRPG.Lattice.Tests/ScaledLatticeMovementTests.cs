using System;
using System.Linq;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class ScaledLatticeMovementTests
    {
        [Fact]
        public void Apply_Scale0_IsSinglePrimitiveStep()
        {
            var agent = new ToctaCoord(3, 2, 3);
            var w = new LatticeWorld(7, 7, 7, agent, null);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);

            var target = ToctaNeighbors.FaceNeighbors(agent).First();
            var deltas = s.Apply(new LatticeAction(scale: 0, faceIndex: 0));

            Assert.Single(deltas);
            var moved = Assert.IsType<AgentMovedDelta>(deltas[0]);
            Assert.Equal(agent, moved.From);
            Assert.Equal(target, moved.To);
        }

        [Fact]
        public void Apply_Scale1_ChangesAgentScale1Parent_ToFaceAdjacentParent()
        {
            // With factor 3 and empty terrain, the agent's scale-1 parent
            // after a scale-1 face-traversal must equal the face-adjacent
            // scale-1 parent of the original.
            var agent = new ToctaCoord(4, 4, 4);
            var w = new LatticeWorld(12, 12, 12, agent, null);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);

            var originalParent = LatticeProjections.ParentAt(agent, 1, 3);
            var expectedTarget = ToctaNeighbors.FaceNeighbors(originalParent).ElementAt(0);

            var deltas = s.Apply(new LatticeAction(scale: 1, faceIndex: 0));

            // Path must be nontrivial.
            Assert.True(deltas.Count >= 1);
            // All steps before the last should be AgentMovedDelta (unless blocked).
            Assert.All(deltas, d => Assert.IsType<AgentMovedDelta>(d));

            var finalParent = LatticeProjections.ParentAt(w.AgentPos, 1, 3);
            Assert.Equal(expectedTarget, finalParent);
        }

        [Fact]
        public void Apply_Scale2_ChangesAgentScale2Parent_ToFaceAdjacentParent()
        {
            // Same architectural claim, one scale deeper. Validates that the
            // (scale, faceIndex) action semantics translate identically to scale 2.
            var agent = new ToctaCoord(13, 13, 13);
            var w = new LatticeWorld(30, 30, 30, agent, null);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 4);

            var originalParent = LatticeProjections.ParentAt(agent, 2, 3);
            var expectedTarget = ToctaNeighbors.FaceNeighbors(originalParent).ElementAt(0);

            var deltas = s.Apply(new LatticeAction(scale: 2, faceIndex: 0));

            Assert.True(deltas.Count >= 1);
            var finalParent = LatticeProjections.ParentAt(w.AgentPos, 2, 3);
            Assert.Equal(expectedTarget, finalParent);
        }

        [Fact]
        public void Apply_ScaleN_AllStepsAreOnPassableCells()
        {
            // The agent must never traverse a blocked cell, even when the path
            // threads past obstacles. Seeded world with 15% blocked terrain.
            var w = new LatticeWorld(seed: 7, sizeX: 14, sizeY: 10, sizeZ: 14, blockedCount: 60);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);
            var deltas = s.Apply(new LatticeAction(1, faceIndex: 0));

            foreach (var d in deltas)
            {
                if (d is AgentMovedDelta moved)
                {
                    Assert.Equal(ToctaType.Passable, w.TypeAt(moved.To));
                    Assert.True(w.InBounds(moved.To));
                }
            }
        }

        [Fact]
        public void Apply_ScaleN_StepsAreFaceAdjacent()
        {
            // Every AgentMovedDelta must be a face-adjacent primitive step —
            // no teleports, no diagonal shortcuts.
            var agent = new ToctaCoord(4, 4, 4);
            var w = new LatticeWorld(12, 12, 12, agent, null);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);
            var deltas = s.Apply(new LatticeAction(1, faceIndex: 5));

            ToctaCoord prev = agent;
            foreach (var d in deltas)
            {
                if (d is AgentMovedDelta moved)
                {
                    Assert.Equal(prev, moved.From);
                    Assert.Contains(moved.To, ToctaNeighbors.FaceNeighbors(prev));
                    prev = moved.To;
                }
            }
        }

        [Fact]
        public void Apply_Determinism_SameStartingState_ProducesSamePath()
        {
            var agent = new ToctaCoord(4, 4, 4);
            var w1 = new LatticeWorld(12, 12, 12, agent, null);
            var w2 = new LatticeWorld(12, 12, 12, agent, null);
            var s1 = new ScaledLattice(w1, scaleFactor: 3, scaleCount: 3);
            var s2 = new ScaledLattice(w2, scaleFactor: 3, scaleCount: 3);

            var d1 = s1.Apply(new LatticeAction(1, 2));
            var d2 = s2.Apply(new LatticeAction(1, 2));

            Assert.Equal(d1.Count, d2.Count);
            for (int i = 0; i < d1.Count; i++)
            {
                Assert.Equal(d1[i].GetType(), d2[i].GetType());
                if (d1[i] is AgentMovedDelta m1 && d2[i] is AgentMovedDelta m2)
                {
                    Assert.Equal(m1.From, m2.From);
                    Assert.Equal(m1.To, m2.To);
                }
            }
            Assert.Equal(w1.AgentPos, w2.AgentPos);
        }

        [Fact]
        public void Apply_SurroundedAgent_ReturnsBlocked()
        {
            // Agent surrounded by blocked toctas on all 14 faces — scale-1
            // face-traversal should immediately return a blocked delta.
            var agent = new ToctaCoord(4, 4, 4);
            var blocked = ToctaNeighbors.FaceNeighbors(agent).ToArray();
            var w = new LatticeWorld(10, 10, 10, agent, blocked);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);

            var deltas = s.Apply(new LatticeAction(1, faceIndex: 0));

            // No primitive moves possible; exactly one blocked delta.
            Assert.Single(deltas);
            Assert.IsType<MovementBlockedDelta>(deltas[0]);
            Assert.Equal(agent, w.AgentPos);
        }

        [Fact]
        public void Apply_InvalidatesCache()
        {
            // After a scale-0 move, the scale-1 aggregate view must rebuild.
            var agent = new ToctaCoord(3, 2, 3);
            var w = new LatticeWorld(7, 7, 7, agent, null);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);

            var before = s.GetScale(1);
            long v0 = s.Version;
            s.Apply(new LatticeAction(0, 0));
            Assert.NotEqual(v0, s.Version);
            var after = s.GetScale(1);
            Assert.NotSame(before, after);
        }

        [Fact]
        public void Apply_RejectsScaleOutOfRange()
        {
            var w = new LatticeWorld(5, 5, 5, new ToctaCoord(2, 2, 2), null);
            var s = new ScaledLattice(w, scaleFactor: 3, scaleCount: 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Apply(new LatticeAction(3, 0)));
        }

        [Fact]
        public void Apply_SameFaceIndex_BehavesConsistently_AcrossScales()
        {
            // Architectural claim: (scale, faceIndex) has the same geometric
            // meaning at every scale. A scale-0 face-0 step offsets by a
            // specific delta; a scale-1 face-0 step offsets the scale-1 parent
            // by the same delta in the scale-1 coord system.
            var agent = new ToctaCoord(6, 6, 6);

            var w0 = new LatticeWorld(16, 16, 16, agent, null);
            var s0 = new ScaledLattice(w0, scaleFactor: 3, scaleCount: 3);
            s0.Apply(new LatticeAction(0, faceIndex: 3));

            var w1 = new LatticeWorld(16, 16, 16, agent, null);
            var s1 = new ScaledLattice(w1, scaleFactor: 3, scaleCount: 3);
            s1.Apply(new LatticeAction(1, faceIndex: 3));

            // For the scale-0 step, the offset between agent-before and
            // agent-after should equal the scale-0 face-neighbor delta.
            var scale0Delta = (w0.AgentPos.X - agent.X, w0.AgentPos.Y - agent.Y, w0.AgentPos.Z - agent.Z);
            var expectedScale0Neighbor = ToctaNeighbors.FaceNeighbors(agent).ElementAt(3);
            Assert.Equal((expectedScale0Neighbor.X - agent.X, expectedScale0Neighbor.Y - agent.Y, expectedScale0Neighbor.Z - agent.Z), scale0Delta);

            // For the scale-1 step, the offset in scale-1 parent coords should
            // equal the scale-1 face-neighbor delta.
            var scale1ParentBefore = LatticeProjections.ParentAt(agent, 1, 3);
            var scale1ParentAfter = LatticeProjections.ParentAt(w1.AgentPos, 1, 3);
            var scale1Delta = (scale1ParentAfter.X - scale1ParentBefore.X, scale1ParentAfter.Y - scale1ParentBefore.Y, scale1ParentAfter.Z - scale1ParentBefore.Z);
            var expectedScale1Neighbor = ToctaNeighbors.FaceNeighbors(scale1ParentBefore).ElementAt(3);
            Assert.Equal((expectedScale1Neighbor.X - scale1ParentBefore.X, expectedScale1Neighbor.Y - scale1ParentBefore.Y, expectedScale1Neighbor.Z - scale1ParentBefore.Z), scale1Delta);
        }
    }
}
