using System;
using System.Collections.Generic;

namespace LedgeRPG.Core.Determinism
{
    /// The 8-action set matching the Python paper server's ACTION_NAMES:
    /// six move-{direction} variants plus Examine and Rest. Kept as an enum
    /// so the adapter (M2) can switch on it exhaustively and so the type
    /// system prevents a stringly-typed "moveN" vs "move-N" drift bug.
    public enum RPGActionKind
    {
        MoveN,
        MoveNE,
        MoveSE,
        MoveS,
        MoveSW,
        MoveNW,
        Examine,
        Rest
    }

    public static class RPGActions
    {
        // Canonical ordering mirrors the Python tuple: moves-in-direction-order,
        // then examine, then rest. Consumers that need a stable enumeration
        // (trace building, tests) iterate this list rather than Enum.GetValues.
        public static readonly IReadOnlyList<RPGActionKind> All = new[]
        {
            RPGActionKind.MoveN,
            RPGActionKind.MoveNE,
            RPGActionKind.MoveSE,
            RPGActionKind.MoveS,
            RPGActionKind.MoveSW,
            RPGActionKind.MoveNW,
            RPGActionKind.Examine,
            RPGActionKind.Rest
        };

        public static bool IsMove(RPGActionKind kind)
            => kind >= RPGActionKind.MoveN && kind <= RPGActionKind.MoveNW;

        public static Direction ToDirection(RPGActionKind kind)
        {
            switch (kind)
            {
                case RPGActionKind.MoveN:  return Direction.N;
                case RPGActionKind.MoveNE: return Direction.NE;
                case RPGActionKind.MoveSE: return Direction.SE;
                case RPGActionKind.MoveS:  return Direction.S;
                case RPGActionKind.MoveSW: return Direction.SW;
                case RPGActionKind.MoveNW: return Direction.NW;
                default: throw new ArgumentException(
                    $"Action {kind} is not a move", nameof(kind));
            }
        }

        public static string ToWireName(RPGActionKind kind)
        {
            switch (kind)
            {
                case RPGActionKind.MoveN:  return "move-N";
                case RPGActionKind.MoveNE: return "move-NE";
                case RPGActionKind.MoveSE: return "move-SE";
                case RPGActionKind.MoveS:  return "move-S";
                case RPGActionKind.MoveSW: return "move-SW";
                case RPGActionKind.MoveNW: return "move-NW";
                case RPGActionKind.Examine: return "examine";
                case RPGActionKind.Rest:    return "rest";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static bool TryParse(string wireName, out RPGActionKind kind)
        {
            switch (wireName)
            {
                case "move-N":  kind = RPGActionKind.MoveN;  return true;
                case "move-NE": kind = RPGActionKind.MoveNE; return true;
                case "move-SE": kind = RPGActionKind.MoveSE; return true;
                case "move-S":  kind = RPGActionKind.MoveS;  return true;
                case "move-SW": kind = RPGActionKind.MoveSW; return true;
                case "move-NW": kind = RPGActionKind.MoveNW; return true;
                case "examine": kind = RPGActionKind.Examine; return true;
                case "rest":    kind = RPGActionKind.Rest;    return true;
                default:        kind = default;              return false;
            }
        }
    }
}
