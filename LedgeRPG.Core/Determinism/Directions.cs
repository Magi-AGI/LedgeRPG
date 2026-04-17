using System;
using System.Collections.Generic;
using LedgeRPG.Core.World;

namespace LedgeRPG.Core.Determinism
{
    public enum Direction
    {
        N,
        NE,
        SE,
        S,
        SW,
        NW
    }

    public static class Directions
    {
        // Order mirrors the Python DIRECTIONS dict insertion order so the move-action
        // enumeration is identical: move-N, move-NE, move-SE, move-S, move-SW, move-NW.
        public static readonly IReadOnlyList<Direction> Ordered = new[]
        {
            Direction.N,
            Direction.NE,
            Direction.SE,
            Direction.S,
            Direction.SW,
            Direction.NW
        };

        public static HexCoord Offset(Direction direction)
        {
            switch (direction)
            {
                case Direction.N:  return new HexCoord(0, -1);
                case Direction.NE: return new HexCoord(1, -1);
                case Direction.SE: return new HexCoord(1, 0);
                case Direction.S:  return new HexCoord(0, 1);
                case Direction.SW: return new HexCoord(-1, 1);
                case Direction.NW: return new HexCoord(-1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static string ToWireName(Direction direction)
        {
            switch (direction)
            {
                case Direction.N:  return "N";
                case Direction.NE: return "NE";
                case Direction.SE: return "SE";
                case Direction.S:  return "S";
                case Direction.SW: return "SW";
                case Direction.NW: return "NW";
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static bool TryParse(string wireName, out Direction direction)
        {
            switch (wireName)
            {
                case "N":  direction = Direction.N;  return true;
                case "NE": direction = Direction.NE; return true;
                case "SE": direction = Direction.SE; return true;
                case "S":  direction = Direction.S;  return true;
                case "SW": direction = Direction.SW; return true;
                case "NW": direction = Direction.NW; return true;
                default:   direction = default;     return false;
            }
        }
    }
}
