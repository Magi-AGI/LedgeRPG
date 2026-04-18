using System;
using Xunit;

namespace LedgeRPG.Lattice.Tests
{
    public class LatticeActionTests
    {
        [Fact]
        public void Equality_SameComponents_AreEqual()
        {
            var a = new LatticeAction(1, 7);
            var b = new LatticeAction(1, 7);
            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equality_DifferentScale_AreNotEqual()
        {
            Assert.NotEqual(new LatticeAction(0, 3), new LatticeAction(1, 3));
            Assert.True(new LatticeAction(0, 3) != new LatticeAction(1, 3));
        }

        [Fact]
        public void Constructor_RejectsNegativeScale()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeAction(-1, 0));
        }

        [Fact]
        public void Constructor_RejectsOutOfRangeFaceIndex()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeAction(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeAction(0, ToctaNeighbors.FaceCount));
        }

        [Fact]
        public void Constructor_AcceptsAllValidFaceIndices()
        {
            for (int i = 0; i < ToctaNeighbors.FaceCount; i++)
            {
                var a = new LatticeAction(0, i);
                Assert.Equal(i, a.FaceIndex);
            }
        }
    }
}
