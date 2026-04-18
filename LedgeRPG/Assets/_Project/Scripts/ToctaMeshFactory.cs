using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Builds a faceted UnityEngine.Mesh from ToctaMeshData. Each of the 14
    /// faces gets its own copy of its vertices so per-face flat normals render
    /// the polyhedron crisply instead of Gouraud-smoothing across the whole
    /// hull. Fan-triangulated from face[0]: squares → 2 tris, hexes → 4 tris.
    ///
    /// Winding: ToctaMeshData emits vertices CCW-as-seen-from-outside. Unity
    /// treats CW-from-camera as front-facing, so we reverse the fan to avoid
    /// having to disable backface culling or flip normals.
    public static class ToctaMeshFactory
    {
        public static Mesh Build()
        {
            int vertCount  = ToctaMeshData.SquareFaceCount * 4 + ToctaMeshData.HexFaceCount * 6;
            int triCount   = ToctaMeshData.SquareFaceCount * 2 + ToctaMeshData.HexFaceCount * 4;

            var verts   = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var tris    = new int[triCount * 3];

            int vi = 0;
            int ti = 0;

            for (int f = 0; f < ToctaMeshData.SquareFaces.Length; f++)
                AppendFace(ToctaMeshData.SquareFaces[f], ToctaMeshData.SquareFaceNormals[f],
                           verts, normals, tris, ref vi, ref ti);

            for (int f = 0; f < ToctaMeshData.HexFaces.Length; f++)
                AppendFace(ToctaMeshData.HexFaces[f], ToctaMeshData.HexFaceNormals[f],
                           verts, normals, tris, ref vi, ref ti);

            var mesh = new Mesh { name = "Tocta" };
            mesh.vertices  = verts;
            mesh.normals   = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendFace(int[] face,
                                       (float X, float Y, float Z) faceNormal,
                                       Vector3[] verts, Vector3[] normals, int[] tris,
                                       ref int vi, ref int ti)
        {
            var n = new Vector3(faceNormal.X, faceNormal.Y, faceNormal.Z);
            int baseVi = vi;
            for (int k = 0; k < face.Length; k++)
            {
                var v = ToctaMeshData.Vertices[face[k]];
                verts[vi]   = new Vector3(v.X, v.Y, v.Z);
                normals[vi] = n;
                vi++;
            }
            // Reverse fan: (base, k+1, k) instead of (base, k, k+1) to flip winding.
            for (int k = 1; k < face.Length - 1; k++)
            {
                tris[ti++] = baseVi;
                tris[ti++] = baseVi + k + 1;
                tris[ti++] = baseVi + k;
            }
        }
    }
}
