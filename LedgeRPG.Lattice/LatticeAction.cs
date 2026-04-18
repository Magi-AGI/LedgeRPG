using System;

namespace LedgeRPG.Lattice
{
    /// Agent traverses face <see cref="FaceIndex"/> of its scale-<see cref="Scale"/>
    /// parent tocta. FaceIndex is the position in <see cref="ToctaNeighbors.FaceNeighbors"/>
    /// enumeration order — 0..5 are the 6 square faces, 6..13 are the 8 hex faces.
    ///
    /// The whole architectural claim being tested: a single (scale, faceIndex)
    /// pair is a sufficient description of movement at every scale, because the
    /// 14-face adjacency rule applies identically from scale 0 to scale 9.
    /// The scale-N action refines into a sequence of scale-0 primitive steps
    /// that end with the agent's scale-N parent having changed to the target
    /// face-neighbor.
    public readonly struct LatticeAction : IEquatable<LatticeAction>
    {
        public int Scale { get; }
        public int FaceIndex { get; }

        public LatticeAction(int scale, int faceIndex)
        {
            if (scale < 0) throw new ArgumentOutOfRangeException(nameof(scale));
            if (faceIndex < 0 || faceIndex >= ToctaNeighbors.FaceCount)
                throw new ArgumentOutOfRangeException(nameof(faceIndex),
                    $"FaceIndex must be in [0, {ToctaNeighbors.FaceCount - 1}].");
            Scale = scale;
            FaceIndex = faceIndex;
        }

        public bool Equals(LatticeAction other) => Scale == other.Scale && FaceIndex == other.FaceIndex;
        public override bool Equals(object obj) => obj is LatticeAction a && Equals(a);
        public override int GetHashCode() => unchecked(Scale * 397 ^ FaceIndex);
        public override string ToString() => $"Face@s{Scale}:{FaceIndex}";

        public static bool operator ==(LatticeAction a, LatticeAction b) => a.Equals(b);
        public static bool operator !=(LatticeAction a, LatticeAction b) => !a.Equals(b);
    }
}
