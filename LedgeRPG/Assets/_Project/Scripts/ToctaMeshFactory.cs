using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Builds a faceted UnityEngine.Mesh from ToctaMeshData. Each of the 14
    /// faces gets its own copy of its vertices so per-face flat normals
    /// render the polyhedron crisply instead of Gouraud-smoothing across
    /// the whole hull. Fan-triangulated from face[0]: squares → 2 tris,
    /// hexes → 4 tris.
    ///
    /// Winding: ToctaMeshData emits vertices CCW-as-seen-from-outside, and
    /// Unity's default front-face rule is also CCW-from-camera. So the
    /// natural fan (base, k, k+1) is outward-facing — backface culling
    /// then hides the interior, which is what you want for opaque cells.
    public static class ToctaMeshFactory
    {
        /// Faceted triangle mesh, one submesh. Use for opaque or
        /// back-face-culled transparent rendering. For a combined mesh
        /// that also carries edge-line geometry for an outline effect,
        /// see <see cref="BuildWithEdges"/>.
        public static Mesh Build()
        {
            var (verts, normals, tris, _) = BuildVertexData();
            var mesh = new Mesh { name = "Tocta" };
            mesh.vertices  = verts;
            mesh.normals   = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// Same faceted triangle geometry as <see cref="Build"/>, plus a
        /// second submesh of MeshTopology.Lines carrying every face's
        /// edges. Each edge appears twice (once per adjacent face) because
        /// we reuse the per-face duplicated vertex positions — the cost
        /// is trivial and avoids a parallel de-duplicated vertex table.
        ///
        /// Render with two materials: material[0] for the solid faces,
        /// material[1] (unlit dark) for the outline lines.
        public static Mesh BuildWithEdges()
        {
            var (verts, normals, tris, lines) = BuildVertexData();
            var mesh = new Mesh { name = "Tocta+Edges", subMeshCount = 2 };
            mesh.vertices = verts;
            mesh.normals  = normals;
            mesh.SetTriangles(tris, submesh: 0);
            mesh.SetIndices(lines, MeshTopology.Lines, submesh: 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static (Vector3[] verts, Vector3[] normals, int[] tris, int[] lines) BuildVertexData()
        {
            int vertCount = ToctaMeshData.SquareFaceCount * 4 + ToctaMeshData.HexFaceCount * 6;
            int triCount  = ToctaMeshData.SquareFaceCount * 2 + ToctaMeshData.HexFaceCount * 4;
            int lineCount = ToctaMeshData.SquareFaceCount * 4 + ToctaMeshData.HexFaceCount * 6;

            var verts   = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var tris    = new int[triCount * 3];
            var lines   = new int[lineCount * 2];

            int vi = 0, ti = 0, li = 0;

            for (int f = 0; f < ToctaMeshData.SquareFaces.Length; f++)
                AppendFace(ToctaMeshData.SquareFaces[f], ToctaMeshData.SquareFaceNormals[f],
                           verts, normals, tris, lines, ref vi, ref ti, ref li);

            for (int f = 0; f < ToctaMeshData.HexFaces.Length; f++)
                AppendFace(ToctaMeshData.HexFaces[f], ToctaMeshData.HexFaceNormals[f],
                           verts, normals, tris, lines, ref vi, ref ti, ref li);

            return (verts, normals, tris, lines);
        }

        private static void AppendFace(int[] face,
                                       (float X, float Y, float Z) faceNormal,
                                       Vector3[] verts, Vector3[] normals,
                                       int[] tris, int[] lines,
                                       ref int vi, ref int ti, ref int li)
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
            for (int k = 1; k < face.Length - 1; k++)
            {
                tris[ti++] = baseVi;
                tris[ti++] = baseVi + k;
                tris[ti++] = baseVi + k + 1;
            }
            for (int k = 0; k < face.Length; k++)
            {
                lines[li++] = baseVi + k;
                lines[li++] = baseVi + ((k + 1) % face.Length);
            }
        }
    }
}
