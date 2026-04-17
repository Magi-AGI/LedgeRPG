using LedgeRPG.Core.Determinism;

namespace LedgeRPG.Adapter
{
    /// Wire-boundary action type. Wraps the Core's value-typed RPGActionKind
    /// enum in a reference type because RulesAdapterBase<TState, TAction>
    /// constrains TAction : class — the server registry boxes actions into
    /// `object` and a struct would incur a box-per-apply. Construction is
    /// cheap and pooled-allocatable; the action carries no state beyond the
    /// kind discriminator in V0.
    public sealed class RPGAction
    {
        public RPGActionKind Kind { get; }

        public RPGAction(RPGActionKind kind)
        {
            Kind = kind;
        }

        /// Convenience factory for wire payloads: parse the "move-N" /
        /// "examine" / "rest" string form and produce an action, or return
        /// null if the wire name doesn't match the canonical action set.
        /// Callers that receive null should surface a Rejected ApplyOutcome
        /// rather than crashing.
        public static RPGAction FromWireName(string wireName)
        {
            if (RPGActions.TryParse(wireName, out var kind))
                return new RPGAction(kind);
            return null;
        }

        public string WireName => RPGActions.ToWireName(Kind);

        public override string ToString() => $"RPGAction({WireName})";
    }
}
