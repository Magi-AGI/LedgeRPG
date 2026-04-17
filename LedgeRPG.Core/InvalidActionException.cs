using System;

namespace LedgeRPG.Core
{
    /// Thrown by World.ApplyAction when an action cannot be applied: either the
    /// action is not in the canonical 8-action set, or the episode has already
    /// terminated and further actions are refused. The adapter (M2) maps this
    /// to ApplyOutcome.Rejected on the wire surface.
    public sealed class InvalidActionException : Exception
    {
        public InvalidActionException(string message) : base(message) { }
    }
}
