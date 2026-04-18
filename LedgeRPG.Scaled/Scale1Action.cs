using System;
using LedgeRPG.Core.Determinism;

namespace LedgeRPG.Scaled
{
    /// A semantic action issued at scale 1, refined to a sequence of scale-0
    /// primitives before it hits Core.World. Abstract base — concrete actions
    /// are the discriminated set below.
    ///
    /// Spike rationale: we need to prove that scale-N actions can be expressed
    /// as compositions of scale-0 actions without losing coherence. The action
    /// set here is intentionally minimal (just enough to exercise refinement);
    /// it is NOT the eventual game surface. Real Gods-Game scale-1 actions will
    /// come from the card/RPG mechanical spine, not from hand-authored enums.
    public abstract class Scale1Action
    {
    }

    /// Move in one direction up to <see cref="Count"/> times. Refinement emits
    /// Count copies of <see cref="Direction"/>; Core.World handles any per-step
    /// blocking (movement-blocked deltas, staying put) without this class
    /// knowing or caring about the source world state.
    ///
    /// Direction-aware validation is deliberately out of scope for the spike:
    /// refinement is a pure syntactic transform, not a planner. Callers decide
    /// what "count" means at their layer. A real planner would live in a
    /// higher-scale action — e.g., a scale-2 PathToTarget that decomposes into
    /// a sequence of MovementBursts.
    public sealed class MovementBurst : Scale1Action
    {
        public RPGActionKind Direction { get; }
        public int Count { get; }

        public MovementBurst(RPGActionKind direction, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (!RPGActions.IsMove(direction))
                throw new ArgumentException(
                    $"MovementBurst direction must be a move action, got {direction}",
                    nameof(direction));
            Direction = direction;
            Count = count;
        }
    }

    /// Move in one direction, then rest in place for a recovery window. Useful
    /// for exercising that refinement can mix action kinds — it's not purely
    /// "repeat the same primitive."
    public sealed class DriftAndRest : Scale1Action
    {
        public RPGActionKind Direction { get; }
        public int MoveCount { get; }
        public int RestCount { get; }

        public DriftAndRest(RPGActionKind direction, int moveCount, int restCount)
        {
            if (moveCount < 0) throw new ArgumentOutOfRangeException(nameof(moveCount));
            if (restCount < 0) throw new ArgumentOutOfRangeException(nameof(restCount));
            if (!RPGActions.IsMove(direction))
                throw new ArgumentException(
                    $"DriftAndRest direction must be a move action, got {direction}",
                    nameof(direction));
            Direction = direction;
            MoveCount = moveCount;
            RestCount = restCount;
        }
    }
}
