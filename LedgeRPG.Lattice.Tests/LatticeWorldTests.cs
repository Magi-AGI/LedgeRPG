using System;
using System.Linq;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class LatticeWorldTests
    {
        [Fact]
        public void Construction_SameSeed_ProducesSameWorld()
        {
            var a = new LatticeWorld(seed: 42, sizeX: 6, sizeY: 4, sizeZ: 6, blockedCount: 20);
            var b = new LatticeWorld(seed: 42, sizeX: 6, sizeY: 4, sizeZ: 6, blockedCount: 20);

            Assert.Equal(a.AgentPos, b.AgentPos);
            foreach (var c in a.AllCoords())
                Assert.Equal(a.TypeAt(c), b.TypeAt(c));
        }

        [Fact]
        public void Construction_DifferentSeed_DivergesSomewhere()
        {
            var a = new LatticeWorld(seed: 42, sizeX: 6, sizeY: 4, sizeZ: 6, blockedCount: 20);
            var b = new LatticeWorld(seed: 43, sizeX: 6, sizeY: 4, sizeZ: 6, blockedCount: 20);

            bool diverged = a.AgentPos != b.AgentPos
                || a.AllCoords().Any(c => a.TypeAt(c) != b.TypeAt(c));
            Assert.True(diverged);
        }

        [Fact]
        public void Counts_AddUpToTotal()
        {
            var w = new LatticeWorld(seed: 1, sizeX: 5, sizeY: 3, sizeZ: 5, blockedCount: 10);
            Assert.Equal(75, w.TotalToctas);
            Assert.Equal(10, w.BlockedCount);
            Assert.Equal(65, w.PassableCount);

            int passable = 0, blocked = 0;
            foreach (var c in w.AllCoords())
            {
                if (w.TypeAt(c) == ToctaType.Passable) passable++;
                else blocked++;
            }
            Assert.Equal(65, passable);
            Assert.Equal(10, blocked);
        }

        [Fact]
        public void AgentPos_IsPassable_AndInBounds()
        {
            var w = new LatticeWorld(seed: 7, sizeX: 4, sizeY: 4, sizeZ: 4, blockedCount: 30);
            Assert.True(w.InBounds(w.AgentPos));
            Assert.Equal(ToctaType.Passable, w.TypeAt(w.AgentPos));
        }

        [Fact]
        public void TypeAt_OutOfBounds_ReturnsBlocked()
        {
            var w = new LatticeWorld(seed: 1, sizeX: 3, sizeY: 3, sizeZ: 3, blockedCount: 5);
            Assert.Equal(ToctaType.Blocked, w.TypeAt(new ToctaCoord(-1, 0, 0)));
            Assert.Equal(ToctaType.Blocked, w.TypeAt(new ToctaCoord(3, 0, 0)));
        }

        [Fact]
        public void Constructor_RejectsInvalidSizes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeWorld(1, 0, 2, 2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeWorld(1, 2, 0, 2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeWorld(1, 2, 2, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeWorld(1, 2, 2, 2, -1));
            // blockedCount == total leaves no room for agent
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeWorld(1, 2, 2, 2, 8));
        }

        [Fact]
        public void AllCoords_EnumeratesEveryCell_Exactly_Once()
        {
            var w = new LatticeWorld(seed: 1, sizeX: 4, sizeY: 3, sizeZ: 5, blockedCount: 1);
            var coords = w.AllCoords().ToList();
            Assert.Equal(60, coords.Count);
            Assert.Equal(60, coords.Distinct().Count());
        }
    }
}
