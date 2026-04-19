using System.Collections.Generic;
using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Renders a set of toctas as opaque meshes at their true world
    /// positions. Used by the (1,1,1)-slice A/B demo where cells within a
    /// single k-layer sit at slightly different Y values, producing the
    /// "rolling hills" alternating-height pattern the mode is meant to
    /// showcase.
    ///
    /// Unlike LatticeRenderer (which renders the whole scale-0 grid or a
    /// scale-N aggregate layer with a transparent material for crossfade),
    /// this one takes an arbitrary cell set and an "agent" highlight coord;
    /// rebuilding is cheap because the selected set is small. The shared
    /// opaque material has instancing enabled but per-cell MPB color so the
    /// agent and any future type-based tinting stay per-cell.
    public sealed class HexSliceRenderer : MonoBehaviour
    {
        private static readonly Color PassableColor = new Color(0.90f, 0.85f, 0.70f, 1.00f);
        private static readonly Color BlockedColor  = new Color(0.35f, 0.35f, 0.35f, 1.00f);
        private static readonly Color AgentColor    = new Color(0.30f, 0.85f, 1.00f, 1.00f);
        private static readonly Color GhostColor    = new Color(0.70f, 0.65f, 0.55f, 0.35f);
        private static readonly Color EdgeColor     = new Color(0.05f, 0.05f, 0.05f, 1.00f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private Mesh _sharedMesh;
        private Material _opaqueMaterial;
        private Material _ghostMaterial;
        private Material _edgeMaterial;

        private readonly List<GameObject> _cells = new();

        public void Rebuild(LatticeWorld world,
                            IEnumerable<ToctaCoord> visibleCells,
                            ToctaCoord agent,
                            IEnumerable<ToctaCoord> ghostCells = null)
        {
            Clear();
            EnsureSharedAssets();

            foreach (var c in visibleCells)
            {
                var (wx, wy, wz) = c.WorldPosition;
                bool passable = world.TypeAt(c) == ToctaType.Passable;
                Color color = c.Equals(agent)
                    ? AgentColor
                    : (passable ? PassableColor : BlockedColor);
                Spawn(new Vector3((float)wx, (float)wy, (float)wz), color, _opaqueMaterial,
                      name: $"Cell {c.X},{c.Y},{c.Z}");
            }

            if (ghostCells != null)
            {
                foreach (var c in ghostCells)
                {
                    var (wx, wy, wz) = c.WorldPosition;
                    Spawn(new Vector3((float)wx, (float)wy, (float)wz), GhostColor, _ghostMaterial,
                          name: $"Ghost {c.X},{c.Y},{c.Z}");
                }
            }
        }

        private void Spawn(Vector3 position, Color color, Material faceMaterial, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = position;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { faceMaterial, _edgeMaterial };

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            mr.SetPropertyBlock(block, 0);

            _cells.Add(go);
        }

        private void EnsureSharedAssets()
        {
            if (_sharedMesh      == null) _sharedMesh      = ToctaMeshFactory.BuildWithEdges();
            if (_opaqueMaterial  == null) _opaqueMaterial  = CreateOpaqueMaterial();
            if (_ghostMaterial   == null) _ghostMaterial   = CreateTransparentMaterial();
            if (_edgeMaterial    == null) _edgeMaterial    = CreateEdgeMaterial();
        }

        private static Material CreateOpaqueMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { enableInstancing = true };
            return mat;
        }

        private static Material CreateTransparentMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { enableInstancing = true };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend",   0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        private static Material CreateEdgeMaterial()
        {
            // Unlit so outlines stay legible regardless of scene lighting.
            // URP "Unlit" falls back to the built-in Unlit/Color on legacy.
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { enableInstancing = true };
            mat.SetColor(BaseColorId, EdgeColor);
            mat.SetColor(ColorId,     EdgeColor);
            // Bias outline draw order to sit on top of the face submesh.
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 1;
            return mat;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; --i)
                Destroy(transform.GetChild(i).gameObject);
            _cells.Clear();
        }

        private void OnDestroy()
        {
            Clear();
            if (_sharedMesh     != null) Destroy(_sharedMesh);
            if (_opaqueMaterial != null) Destroy(_opaqueMaterial);
            if (_ghostMaterial  != null) Destroy(_ghostMaterial);
            if (_edgeMaterial   != null) Destroy(_edgeMaterial);
        }
    }
}
