using System.Linq;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class LatticeWorldMovementTests
    {
        [Fact]
        public void TryStep_ToPassableFaceNeighbor_MovesAgent()
        {
            var agent = new ToctaCoord(2, 2, 2);
            var w = new LatticeWorld(sizeX: 5, sizeY: 5, sizeZ: 5, agentPos: agent, blockedCoords: null);

            var target = ToctaNeighbors.FaceNeighbors(agent).First();
            var delta = w.TryStep(target);

            var moved = Assert.IsType<AgentMovedDelta>(delta);
            Assert.Equal(agent, moved.From);
            Assert.Equal(target, moved.To);
            Assert.Equal(target, w.AgentPos);
        }

        [Fact]
        public void TryStep_ToBlockedTerrain_ReturnsBlocked_AndDoesNotMove()
        {
            var agent = new ToctaCoord(2, 2, 2);
            var blocked = ToctaNeighbors.FaceNeighbors(agent).First();
            var w = new LatticeWorld(5, 5, 5, agent, new[] { blocked });

            var delta = w.TryStep(blocked);

            var b = Assert.IsType<MovementBlockedDelta>(delta);
            Assert.Equal(BlockReason.BlockedTerrain, b.Reason);
            Assert.Equal(agent, w.AgentPos);
        }

        [Fact]
        public void TryStep_ToOutOfBoundsFaceNeighbor_ReturnsOutOfBounds()
        {
            // Agent in corner (0,0,0) — even layer, face-neighbor (-1,0,0)
            // is face-adjacent (via square-face delta) but out of bounds.
            var agent = new ToctaCoord(0, 0, 0);
            var w = new LatticeWorld(5, 5, 5, agent, null);
            var target = new ToctaCoord(-1, 0, 0);
            Assert.Contains(target, ToctaNeighbors.FaceNeighbors(agent));

            var delta = w.TryStep(target);
            var b = Assert.IsType<MovementBlockedDelta>(delta);
            Assert.Equal(BlockReason.OutOfBounds, b.Reason);
            Assert.Equal(agent, w.AgentPos);
        }

        [Fact]
        public void TryStep_ToNonFaceAdjacent_ReturnsNotFaceAdjacent()
        {
            var agent = new ToctaCoord(2, 2, 2);
            var w = new LatticeWorld(5, 5, 5, agent, null);
            // Two steps away — not face-adjacent.
            var target = new ToctaCoord(4, 2, 2);
            Assert.DoesNotContain(target, ToctaNeighbors.FaceNeighbors(agent));

            var delta = w.TryStep(target);
            var b = Assert.IsType<MovementBlockedDelta>(delta);
            Assert.Equal(BlockReason.NotFaceAdjacent, b.Reason);
            Assert.Equal(agent, w.AgentPos);
        }

        [Fact]
        public void TryStep_ToSelf_IsNotFaceAdjacent()
        {
            var agent = new ToctaCoord(2, 2, 2);
            var w = new LatticeWorld(5, 5, 5, agent, null);
            var delta = w.TryStep(agent);
            var b = Assert.IsType<MovementBlockedDelta>(delta);
            Assert.Equal(BlockReason.NotFaceAdjacent, b.Reason);
        }

        [Fact]
        public void TryStep_All14FaceNeighbors_WorkIndividually_FromEvenLayer()
        {
            // Verify the 14-face rule: from an even-layer agent with empty
            // terrain, each of the 14 face-neighbors accepts a primitive move.
            for (int i = 0; i < 14; i++)
            {
                var agent = new ToctaCoord(3, 2, 3);
                var w = new LatticeWorld(7, 7, 7, agent, null);
                var target = ToctaNeighbors.FaceNeighbors(agent).ElementAt(i);
                var delta = w.TryStep(target);
                Assert.IsType<AgentMovedDelta>(delta);
                Assert.Equal(target, w.AgentPos);
            }
        }

        [Fact]
        public void TryStep_All14FaceNeighbors_WorkIndividually_FromOddLayer()
        {
            // Mirror test for odd-Y-parity start — the hex-face deltas branch
            // on parity, so this proves the parity branch doesn't skip any face.
            for (int i = 0; i < 14; i++)
            {
                var agent = new ToctaCoord(3, 3, 3);
                var w = new LatticeWorld(7, 7, 7, agent, null);
                var target = ToctaNeighbors.FaceNeighbors(agent).ElementAt(i);
                var delta = w.TryStep(target);
                Assert.IsType<AgentMovedDelta>(delta);
                Assert.Equal(target, w.AgentPos);
            }
        }
    }
}
