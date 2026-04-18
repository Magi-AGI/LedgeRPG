using System.Collections.Generic;
using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Spawns one GameObject per cell sharing a single ToctaMesh and material
    /// across every instance. Two build paths:
    ///   • Build(LatticeWorld)                    — render the scale-0 source.
    ///   • BuildScale(aggregates, scaleMultiplier) — render a projected layer
    ///     at scale N, with each parent-tocta positioned at its coord scaled by
    ///     scaleMultiplier = factor^N and sized to match.
    ///
    /// Rebuild-on-change (no partial refresh): zoom swaps the full set cheaply,
    /// and the spike has no in-place mutation yet. Once movement wires up at
    /// scale 0 we can bring back a Refresh path to avoid respawning 216 cells
    /// on every step.
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

        public void Build(LatticeWorld world)
        {
            Clear();
            EnsureSharedAssets();

            foreach (var c in world.AllCoords())
            {
                var (wx, wy, wz) = c.WorldPosition;
                bool passable = world.TypeAt(c) == ToctaType.Passable;
                Color color = c.Equals(world.AgentPos)
                    ? AgentColor
                    : (passable ? PassableColor : BlockedColor);
                SpawnTocta(
                    new Vector3((float)wx, (float)wy, (float)wz),
                    scale: 1f,
                    passable: passable,
                    color: color,
                    name: $"Tocta {c.X},{c.Y},{c.Z}");
            }
        }

        public void BuildScale(IReadOnlyDictionary<ToctaCoord, ToctaAggregate> aggregates,
                               double scaleMultiplier)
        {
            Clear();
            EnsureSharedAssets();

            float sm = (float)scaleMultiplier;
            foreach (var kv in aggregates)
            {
                var parent = kv.Key;
                var agg = kv.Value;
                var (wx, wy, wz) = parent.WorldPosition;
                bool passable = agg.DominantType == ToctaType.Passable;
                Color color = agg.HasAgent
                    ? AgentColor
                    : (passable ? PassableColor : BlockedColor);
                SpawnTocta(
                    new Vector3((float)(wx * sm), (float)(wy * sm), (float)(wz * sm)),
                    scale: sm,
                    passable: passable,
                    color: color,
                    name: $"Agg {parent.X},{parent.Y},{parent.Z}");
            }
        }

        private void SpawnTocta(Vector3 position, float scale, bool passable, Color color, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = position;
            go.transform.localScale    = Vector3.one * scale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = passable ? _transparentMaterial : _opaqueMaterial;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            mr.SetPropertyBlock(block);
        }

        private void EnsureSharedAssets()
        {
            if (_sharedMesh          == null) _sharedMesh          = ToctaMeshFactory.Build();
            if (_opaqueMaterial      == null) _opaqueMaterial      = CreateMaterial(transparent: false);
            if (_transparentMaterial == null) _transparentMaterial = CreateMaterial(transparent: true);
        }

        private static Material CreateMaterial(bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { enableInstancing = true };
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend",   0f);
                mat.SetFloat("_SrcBlend",     (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend",     (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite",       0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return mat;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; --i)
                Destroy(transform.GetChild(i).gameObject);
        }

        private void OnDestroy()
        {
            Clear();
            if (_sharedMesh          != null) Destroy(_sharedMesh);
            if (_opaqueMaterial      != null) Destroy(_opaqueMaterial);
            if (_transparentMaterial != null) Destroy(_transparentMaterial);
        }
    }
}
