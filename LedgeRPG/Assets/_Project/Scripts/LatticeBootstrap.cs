using System;
using System.Collections;
using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Single-entry-point MonoBehaviour for the lattice spike scene. Drop onto
    /// a GameObject in LedgeRPGLattice.unity (Main Camera + Directional Light
    /// expected) and press Play: builds a seeded LatticeWorld wrapped in a
    /// ScaledLattice, spawns one LatticeRenderer per scale (pre-built so zoom
    /// transitions don't pay rebuild cost), spawns a LatticeZoomController,
    /// and frames the main camera.
    ///
    /// Mouse wheel snaps between scales — scroll DOWN coarser, scroll UP finer.
    /// Each scale change kicks off a short crossfade coroutine that lerps
    /// alphas between the two scales' renderers and the camera between their
    /// framed poses, so the user perceives a smooth zoom rather than a snap.
    ///
    /// Defaults target the production factor-10 ladder; the demo region (15³
    /// children, ~25% blocked) is picked so scale 1 yields ~8 aggregates while
    /// keeping the scale-0 cell count manageable. Scale 2 collapses to ~1
    /// aggregate at factor 10, which is expected — the ladder system is
    /// general, the demo just doesn't have enough children to populate it
    /// densely past scale 1.
    public sealed class LatticeBootstrap : MonoBehaviour
    {
        [Header("Seeding")]
        public long Seed = 42;
        public int SizeX = 15;
        public int SizeY = 15;
        public int SizeZ = 15;
        public int BlockedCount = 800;

        [Header("Zoom")]
        public int ScaleFactor = 10;
        public int ScaleCount  = 3;

        [Header("Camera framing")]
        public float CameraDistance = 12f;
        public float CameraHeight   = 8f;
        public float CameraFov      = 50f;

        [Header("Transition")]
        public float TransitionSeconds = 0.5f;

        private LatticeWorld    _world;
        private ScaledLattice   _scaled;
        private LatticeRenderer[] _renderers;
        private Vector3[] _centroids;
        private Vector3[] _cameraPositions;
        private LatticeZoomController _zoom;
        private Coroutine _transition;
        private int _currentScale;
        private Vector3 _currentLookTarget;

        private void Start()
        {
            _world  = new LatticeWorld(Seed, SizeX, SizeY, SizeZ, BlockedCount);
            _scaled = new ScaledLattice(_world, ScaleFactor, ScaleCount);

            _renderers       = new LatticeRenderer[ScaleCount];
            _centroids       = new Vector3[ScaleCount];
            _cameraPositions = new Vector3[ScaleCount];
            for (int s = 0; s < ScaleCount; s++)
            {
                var rGo = new GameObject($"LatticeRenderer s{s}");
                rGo.transform.SetParent(transform, worldPositionStays: false);
                var r = rGo.AddComponent<LatticeRenderer>();
                if (s == 0)
                {
                    r.Build(_world);
                }
                else
                {
                    var aggregates = _scaled.GetScale(s);
                    double scaleMultiplier = Math.Pow(ScaleFactor, s);
                    r.BuildScale(aggregates, scaleMultiplier);
                }
                r.SetAlpha(s == 0 ? 1f : 0f);
                rGo.SetActive(s == 0);
                _renderers[s] = r;

                // Frame from actual rendered bounds — formula-based centroids
                // drift badly when factor compresses parent coords (e.g. factor
                // 10 on a 15³ source leaves only ~2 parent coords per axis).
                float cellExtent = (float)Math.Pow(ScaleFactor, s) * 0.5f;
                var bounds = ComputeChildBounds(rGo.transform, cellExtent);
                _centroids[s] = bounds.center;
                float radius = bounds.extents.magnitude;
                float fitDist = radius / Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);
                Vector3 dir = new Vector3(CameraDistance, CameraHeight, -CameraDistance).normalized;
                _cameraPositions[s] = _centroids[s] + dir * fitDist * 1.4f;
            }

            var zGo = new GameObject("ZoomController");
            zGo.transform.SetParent(transform, worldPositionStays: false);
            _zoom = zGo.AddComponent<LatticeZoomController>();
            _zoom.MaxScale = ScaleCount - 1;
            _zoom.OnScaleChanged += OnScaleChanged;

            _currentScale = 0;
            _currentLookTarget = _centroids[0];
            ConfigureCamera(_currentScale);
        }

        private static Bounds ComputeChildBounds(Transform parent, float cellExtent)
        {
            if (parent.childCount == 0) return new Bounds(Vector3.zero, Vector3.zero);
            var b = new Bounds(parent.GetChild(0).localPosition, Vector3.zero);
            for (int i = 1; i < parent.childCount; i++)
                b.Encapsulate(parent.GetChild(i).localPosition);
            b.Expand(cellExtent * 2f);
            return b;
        }

        private void OnScaleChanged(int scale)
        {
            if (scale == _currentScale) return;
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(CrossfadeTo(scale));
        }

        private IEnumerator CrossfadeTo(int toScale)
        {
            _currentScale = toScale;
            var toR = _renderers[toScale];
            toR.gameObject.SetActive(true);

            // Snapshot every renderer's starting alpha so an interrupt mid-fade
            // resumes from the actual visual state instead of snapping back.
            var startAlphas = new float[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                startAlphas[i] = _renderers[i].CurrentAlpha;

            var cam = Camera.main;
            Vector3 fromPos = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 toPos   = _cameraPositions[toScale];
            Vector3 fromLook = _currentLookTarget;
            Vector3 toLook   = _centroids[toScale];

            float t = 0f;
            while (t < TransitionSeconds)
            {
                t += Time.deltaTime;
                float u  = Mathf.Clamp01(t / TransitionSeconds);
                float us = Mathf.SmoothStep(0f, 1f, u);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (i != toScale && !_renderers[i].gameObject.activeSelf) continue;
                    float target = (i == toScale) ? 1f : 0f;
                    _renderers[i].SetAlpha(Mathf.Lerp(startAlphas[i], target, us));
                }
                if (cam != null)
                {
                    cam.transform.position = Vector3.Lerp(fromPos, toPos, us);
                    Vector3 lookNow = Vector3.Lerp(fromLook, toLook, us);
                    cam.transform.LookAt(lookNow);
                    _currentLookTarget = lookNow;
                }
                yield return null;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (i == toScale)
                {
                    _renderers[i].SetAlpha(1f);
                }
                else if (_renderers[i].gameObject.activeSelf)
                {
                    _renderers[i].SetAlpha(0f);
                    _renderers[i].gameObject.SetActive(false);
                }
            }
            if (cam != null)
            {
                cam.transform.position = toPos;
                cam.transform.LookAt(toLook);
                _currentLookTarget = toLook;
            }
            _transition = null;
        }

        private void ConfigureCamera(int scale)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = _cameraPositions[scale];
            cam.transform.LookAt(_centroids[scale]);
            cam.fieldOfView = CameraFov;
        }
    }
}
