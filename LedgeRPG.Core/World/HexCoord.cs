using System;

namespace LedgeRPG.Core.World
{
    /// Axial hex coordinate. Q is the "column" axis, R is the "row" axis;
    /// together with the implicit s = -q-r they form the three-axis cube
    /// coordinate system, but we only carry two since the third is derivable.
    /// Matches the Python server's (q, r) tuples exactly.
    public readonly struct HexCoord : IEquatable<HexCoord>, IComparable<HexCoord>
    {
        public int Q { get; }
        public int R { get; }

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public HexCoord Translate(HexCoord offset) => new HexCoord(Q + offset.Q, R + offset.R);

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);

        public override int GetHashCode()
        {
            // Stable hash that doesn't depend on framework-provided string/tuple
            // hashing (which .NET randomizes by default). Keeps world-gen
            // deterministic if we ever iterate a Dictionary<HexCoord, T> in a
            // hash-order-sensitive context — though rules paths avoid that.
            unchecked { return (Q * 397) ^ R; }
        }

        public int CompareTo(HexCoord other)
        {
            // Lexicographic (Q, R) order — matches the Python sorted((q, r) for ...)
            // world-gen sort so the C# shuffle has the same input ordering.
            int qCmp = Q.CompareTo(other.Q);
            return qCmp != 0 ? qCmp : R.CompareTo(other.R);
        }

        public override string ToString() => $"({Q},{R})";

        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
    }
}
