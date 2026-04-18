using System;
using System.Collections.Generic;
using LedgeRPG.Core.Determinism;

namespace LedgeRPG.Scaled
{
    /// Pure syntactic refinement: Scale1Action → sequence of scale-0 primitives.
    /// No world state is consulted — refinement is deterministic from the action
    /// alone. Runtime outcomes (blocked moves, energy depletion, termination)
    /// are Core.World's business, surfaced through deltas when ScaledWorld
    /// applies the sequence.
    ///
    /// This separation is the load-bearing architectural move of the spike:
    /// "what the player intends" (semantic action) is defined independently of
    /// "what the world does" (primitive outcomes). Higher-scale actions can
    /// compose without knowing how the world will respond; the world stays the
    /// single source of truth for resolution.
    public static class ActionRefinement
    {
        public static IReadOnlyList<RPGActionKind> Refine(Scale1Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            switch (action)
            {
                case MovementBurst burst:
                    return RepeatPrimitive(burst.Direction, burst.Count);

                case DriftAndRest drift:
                    var result = new List<RPGActionKind>(drift.MoveCount + drift.RestCount);
                    for (int i = 0; i < drift.MoveCount; i++) result.Add(drift.Direction);
                    for (int i = 0; i < drift.RestCount; i++) result.Add(RPGActionKind.Rest);
                    return result;

                default:
                    // Scale1Action is abstract with a closed concrete set —
                    // unknown subclasses are a programming error, not a user
                    // mistake, so throw rather than swallow. If this fires,
                    // someone added a new Scale1Action subtype without adding
                    // a refinement case.
                    throw new ArgumentException(
                        $"Unknown Scale1Action subtype: {action.GetType().FullName}",
                        nameof(action));
            }
        }

        private static IReadOnlyList<RPGActionKind> RepeatPrimitive(RPGActionKind kind, int count)
        {
            var list = new List<RPGActionKind>(count);
            for (int i = 0; i < count; i++) list.Add(kind);
            return list;
        }
    }
}
