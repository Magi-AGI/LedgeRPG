using System;

namespace LedgeRPG.Lattice
{
    /// Pure C# geometry for one truncated-octahedron mesh. Unity-free so it
    /// stays in LedgeRPG.Lattice and is unit-testable; the Unity-side factory
    /// (ToctaMeshFactory in the Unity scripts) triangulates this data into a
    /// UnityEngine.Mesh.
    ///
    /// Standard truncated octahedron: 24 vertices at permutations of
    /// (0, ±1, ±2). Scaled by <see cref="Scale"/> = 0.25 so the center-to-
    /// square-face distance is 0.5 and adjacent lattice cells' meshes tile
    /// seamlessly at unit BCC spacing. At this scale, center-to-hex-face is
    /// sqrt(3)/4 ≈ 0.433, which matches the BCC hex-neighbor half-distance.
    ///
    /// Face enumeration — 6 square + 8 hex, matching the <see cref="ToctaNeighbors"/>
    /// adjacency order so a face index here corresponds to the same neighbor
    /// the movement code targets. Vertex indices per face are ordered
    /// counterclockwise when viewed from outside (outward-pointing normal by
    /// the right-hand rule), so the factory can emit either a fan-triangulated
    /// mesh or pass vertices directly without reordering.
    public static class ToctaMeshData
    {
        public const float Scale = 0.25f;

        public const int VertexCount = 24;
        public const int SquareFaceCount = 6;
        public const int HexFaceCount = 8;

        private static readonly (int x, int y, int z)[] UnscaledVertices =
        {
            (0,  1,  2), (0, -1,  2), (0,  1, -2), (0, -1, -2),  // 0..3
            (0,  2,  1), (0, -2,  1), (0,  2, -1), (0, -2, -1),  // 4..7
            (1,  0,  2), (-1, 0,  2), (1,  0, -2), (-1, 0, -2),  // 8..11
            (2,  0,  1), (-2, 0,  1), (2,  0, -1), (-2, 0, -1),  // 12..15
            (1,  2,  0), (-1, 2,  0), (1, -2,  0), (-1, -2, 0),  // 16..19
            (2,  1,  0), (-2, 1,  0), (2, -1,  0), (-2, -1, 0),  // 20..23
        };

        public static readonly (float X, float Y, float Z)[] Vertices;

        /// Indices for each of the 6 square faces. Outer order matches the
        /// first 6 entries of <see cref="ToctaNeighbors.FaceNeighbors"/>:
        /// +X, -X, +Y, -Y, +Z, -Z. Each inner array is 4 vertex indices
        /// in counterclockwise order as viewed from outside.
        public static readonly int[][] SquareFaces =
        {
            new[] { 20, 12, 22, 14 }, // +X face (x = 2)
            new[] { 21, 15, 23, 13 }, // -X face (x = -2)
            new[] {  4, 16,  6, 17 }, // +Y face (y = 2)
            new[] {  5, 19,  7, 18 }, // -Y face (y = -2)
            new[] {  0,  9,  1,  8 }, // +Z face (z = 2)
            new[] {  2, 10,  3, 11 }, // -Z face (z = -2)
        };

        /// Indices for each of the 8 hex faces. Enumerated in octant order
        /// (sx, sy, sz ∈ {+, -}³) with outward-CCW winding. Each inner array
        /// is 6 vertex indices.
        public static readonly int[][] HexFaces =
        {
            new[] { 16,  4,  0,  8, 12, 20 }, // +++ (x+y+z=3)
            new[] { 20, 14, 10,  2,  6, 16 }, // ++- (x+y-z=3)
            new[] { 22, 12,  8,  1,  5, 18 }, // +-+ (x-y+z=3)
            new[] { 18,  7,  3, 10, 14, 22 }, // +-- (x-y-z=3)
            new[] { 21, 13,  9,  0,  4, 17 }, // -++ (-x+y+z=3)
            new[] { 17,  6,  2, 11, 15, 21 }, // -+- (-x+y-z=3)
            new[] { 19,  5,  1,  9, 13, 23 }, // --+ (-x-y+z=3)
            new[] { 23, 15, 11,  3,  7, 19 }, // --- (-x-y-z=3)
        };

        /// Outward unit normals for the 6 square faces, same order as
        /// <see cref="SquareFaces"/>.
        public static readonly (float X, float Y, float Z)[] SquareFaceNormals =
        {
            ( 1f,  0f,  0f), (-1f,  0f,  0f),
            ( 0f,  1f,  0f), ( 0f, -1f,  0f),
            ( 0f,  0f,  1f), ( 0f,  0f, -1f),
        };

        /// Outward unit normals for the 8 hex faces, same order as
        /// <see cref="HexFaces"/>. Each is (±1, ±1, ±1)/sqrt(3).
        public static readonly (float X, float Y, float Z)[] HexFaceNormals;

        static ToctaMeshData()
        {
            Vertices = new (float, float, float)[UnscaledVertices.Length];
            for (int i = 0; i < UnscaledVertices.Length; i++)
            {
                Vertices[i] = (UnscaledVertices[i].x * Scale,
                               UnscaledVertices[i].y * Scale,
                               UnscaledVertices[i].z * Scale);
            }

            HexFaceNormals = new (float, float, float)[HexFaceCount];
            float invSqrt3 = 1f / (float)Math.Sqrt(3);
            int k = 0;
            for (int sx = 1; sx >= -1; sx -= 2)
                for (int sy = 1; sy >= -1; sy -= 2)
                    for (int sz = 1; sz >= -1; sz -= 2)
                        HexFaceNormals[k++] = (sx * invSqrt3, sy * invSqrt3, sz * invSqrt3);
        }
    }
}
