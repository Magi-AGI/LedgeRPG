using System.Linq;
using LedgeRPG.Core.Determinism;
using Xunit;

namespace LedgeRPG.Scaled.Tests
{
    public class ActionRefinementTests
    {
        [Fact]
        public void MovementBurst_RefinesToNCopiesOfDirection()
        {
            var burst = new MovementBurst(RPGActionKind.MoveNE, count: 3);
            var refined = ActionRefinement.Refine(burst);

            Assert.Equal(3, refined.Count);
            Assert.All(refined, a => Assert.Equal(RPGActionKind.MoveNE, a));
        }

        [Fact]
        public void MovementBurst_WithZeroCount_RefinesToEmpty()
        {
            var burst = new MovementBurst(RPGActionKind.MoveN, count: 0);
            var refined = ActionRefinement.Refine(burst);
            Assert.Empty(refined);
        }

        [Fact]
        public void DriftAndRest_RefinesToMovesThenRests()
        {
            // Ordering is part of the refinement contract — moves come first,
            // rests after. If we ever flip this, callers that rely on "moved
            // before resting" semantics silently break.
            var drift = new DriftAndRest(RPGActionKind.MoveS, moveCount: 2, restCount: 3);
            var refined = ActionRefinement.Refine(drift);

            var expected = new[]
            {
                RPGActionKind.MoveS,
                RPGActionKind.MoveS,
                RPGActionKind.Rest,
                RPGActionKind.Rest,
                RPGActionKind.Rest,
            };
            Assert.Equal(expected, refined.ToArray());
        }

        [Fact]
        public void Refinement_IsPure_NoWorldStateConsulted()
        {
            // Same action → same sequence, regardless of context. Refinement
            // is a syntactic transform; world-state-dependent planning lives
            // elsewhere (not implemented in the spike). Proving this keeps the
            // scale-N action layer testable without World fixtures.
            var burst = new MovementBurst(RPGActionKind.MoveSW, count: 5);
            var a = ActionRefinement.Refine(burst);
            var b = ActionRefinement.Refine(burst);

            Assert.Equal(a.ToArray(), b.ToArray());
        }
    }
}
