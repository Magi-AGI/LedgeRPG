using System;
using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Single-entry-point MonoBehaviour for the lattice spike scene. Drop onto
    /// a GameObject in LedgeRPGLattice.unity (Main Camera + Directional Light
    /// expected) and press Play: builds a seeded LatticeWorld wrapped in a
    /// ScaledLattice, spawns a LatticeRenderer and LatticeZoomController, and
    /// frames the main camera.
    ///
    /// Scroll wheel snaps between scale 0 (the authoritative world) and the
    /// projected aggregate layers at scale 1 and 2. ScaleFactor defaults to 2
    /// (not the 10 that production intends) so three scales remain visually
    /// distinct in a 6^3 demo world — with factor 10 you'd get ~1 aggregate at
    /// scale 1, which tells you nothing.
    ///
    /// No input wiring yet — agent stays put. The zoom controller takes mouse
    /// wheel directly via the new Input System, independent of the pending
    /// keyboard-movement rework.
    public sealed class LatticeBootstrap : MonoBehaviour
    {
        [Header("Seeding")]
        public long Seed = 42;
        public int SizeX = 6;
        public int SizeY = 6;
        public int SizeZ = 6;
        public int BlockedCount = 40;

        [Header("Zoom")]
        public int ScaleFactor = 2;
        public int ScaleCount  = 3;

        [Header("Camera framing")]
        public float CameraDistance = 10f;
        public float CameraHeight   = 6f;
        public float CameraFov      = 50f;

        private LatticeWorld    _world;
        private ScaledLattice   _scaled;
        private LatticeRenderer _renderer;
        private LatticeZoomController _zoom;

        private void Start()
        {
            _world  = new LatticeWorld(Seed, SizeX, SizeY, SizeZ, BlockedCount);
            _scaled = new ScaledLattice(_world, ScaleFactor, ScaleCount);

            var rGo = new GameObject("LatticeRenderer");
            rGo.transform.SetParent(transform, worldPositionStays: false);
            _renderer = rGo.AddComponent<LatticeRenderer>();

            var zGo = new GameObject("ZoomController");
            zGo.transform.SetParent(transform, worldPositionStays: false);
            _zoom = zGo.AddComponent<LatticeZoomController>();
            _zoom.MaxScale = ScaleCount - 1;
            _zoom.OnScaleChanged += OnScaleChanged;

            RebuildAtScale(0);
            ConfigureCamera();
        }

        private void OnScaleChanged(int scale) => RebuildAtScale(scale);

        private void RebuildAtScale(int scale)
        {
            if (scale == 0)
            {
                _renderer.Build(_world);
            }
            else
            {
                var aggregates = _scaled.GetScale(scale);
                double scaleMultiplier = Math.Pow(ScaleFactor, scale);
                _renderer.BuildScale(aggregates, scaleMultiplier);
            }
        }

        private void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var centroid = ComputeCentroid();
            cam.transform.position = centroid + new Vector3(CameraDistance, CameraHeight, -CameraDistance);
            cam.transform.LookAt(centroid);
            cam.fieldOfView = CameraFov;
        }

        private Vector3 ComputeCentroid()
        {
            float cx = (SizeX - 1) * 0.5f + 0.25f;
            float cy = 0.25f * (SizeY - 1);
            float cz = (SizeZ - 1) * 0.5f + 0.25f;
            return new Vector3(cx, cy, cz);
        }
    }
}
