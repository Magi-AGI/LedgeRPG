using System.Collections.Generic;
using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Spawns one GameObject per tocta in a LatticeWorld, sharing a single
    /// ToctaMesh and material across every cell. State is conveyed per-instance
    /// via MaterialPropertyBlocks — agent highlight and passable/blocked recolor
    /// don't need to touch geometry or allocate materials.
    ///
    /// Rendering choice: every cell is drawn, not just blocked ones. Passable
    /// cells are semi-transparent so the lattice structure reads; blocked cells
    /// are opaque so walls punch through. This is the cheapest way to confirm
    /// the tocta geometry tiles correctly — a production renderer would either
    /// hide passable cells entirely or use GPU instancing with a DrawMeshInstanced
    /// batch instead of one GameObject per cell.
    public sealed class LatticeRenderer : MonoBehaviour
    {
        private static readonly Color PassableColor = new Color(0.90f, 0.85f, 0.70f, 0.25f);
        private static readonly Color BlockedColor  = new Color(0.35f, 0.35f, 0.35f, 1.00f);
        private static readonly Color AgentColor    = new Color(0.30f, 0.85f, 1.00f, 1.00f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private Mesh _sharedMesh;
        private Material _opaqueMaterial;
        private Material _transparentMaterial;
        private readonly Dictionary<ToctaCoord, Renderer> _cells =
            new Dictionary<ToctaCoord, Renderer>();

        public void Build(LatticeWorld world)
        {
            Clear();
            _sharedMesh          = ToctaMeshFactory.Build();
            _opaqueMaterial      = CreateMaterial(transparent: false);
            _transparentMaterial = CreateMaterial(transparent: true);

            foreach (var c in world.AllCoords())
            {
                var (wx, wy, wz) = c.WorldPosition;
                var go = new GameObject($"Tocta {c.X},{c.Y},{c.Z}");
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = new Vector3((float)wx, (float)wy, (float)wz);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = _sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial =
                    world.TypeAt(c) == ToctaType.Passable ? _transparentMaterial : _opaqueMaterial;
                _cells[c] = mr;
            }
            Refresh(world);
        }

        public void Refresh(LatticeWorld world)
        {
            var block = new MaterialPropertyBlock();
            foreach (var kv in _cells)
            {
                var coord = kv.Key;
                Color color;
                if (coord.Equals(world.AgentPos))
                    color = AgentColor;
                else if (world.TypeAt(coord) == ToctaType.Passable)
                    color = PassableColor;
                else
                    color = BlockedColor;
                block.Clear();
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                kv.Value.SetPropertyBlock(block);
            }
        }

        private static Material CreateMaterial(bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { enableInstancing = true };
            if (transparent)
            {
                // URP Lit transparent setup: _Surface=1, blend src/dst, disable ZWrite, move to transparent queue.
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend",   0f); // alpha blend
                mat.SetFloat("_SrcBlend",     (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend",     (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite",       0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return mat;
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; --i)
                Destroy(transform.GetChild(i).gameObject);
            _cells.Clear();
            if (_sharedMesh          != null) Destroy(_sharedMesh);
            if (_opaqueMaterial      != null) Destroy(_opaqueMaterial);
            if (_transparentMaterial != null) Destroy(_transparentMaterial);
        }

        private void OnDestroy() => Clear();
    }
}
