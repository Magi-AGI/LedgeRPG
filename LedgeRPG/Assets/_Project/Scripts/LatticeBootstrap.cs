using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Single-entry-point MonoBehaviour for the lattice spike scene. Drop onto
    /// a GameObject in LedgeRPGLattice.unity (Main Camera + Directional Light
    /// expected) and press Play: builds a seeded LatticeWorld, spawns one
    /// tocta per cell via LatticeRenderer, and frames the grid with the main
    /// camera.
    ///
    /// Deliberately no input yet — movement hookup waits on the pending input
    /// rework. The purpose of this scene is to confirm the tocta mesh
    /// geometry and BCC tiling read correctly when rendered.
    public sealed class LatticeBootstrap : MonoBehaviour
    {
        [Header("Seeding")]
        public long Seed = 42;
        public int SizeX = 6;
        public int SizeY = 6;
        public int SizeZ = 6;
        public int BlockedCount = 40;

        [Header("Camera framing")]
        public float CameraDistance = 10f;
        public float CameraHeight   = 6f;
        public float CameraFov      = 50f;

        private LatticeWorld _world;
        private LatticeRenderer _renderer;

        private void Start()
        {
            _world = new LatticeWorld(Seed, SizeX, SizeY, SizeZ, BlockedCount);

            var rGo = new GameObject("LatticeRenderer");
            rGo.transform.SetParent(transform, worldPositionStays: false);
            _renderer = rGo.AddComponent<LatticeRenderer>();
            _renderer.Build(_world);

            ConfigureCamera();
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
            // ToctaCoord.WorldPosition: X+odd-offset, 0.5*Y, Z+odd-offset.
            // Midpoint roughly (X/2, Y/4, Z/2) plus ~0.25 for the odd-layer half-offset.
            float cx = (SizeX - 1) * 0.5f + 0.25f;
            float cy = 0.25f * (SizeY - 1);
            float cz = (SizeZ - 1) * 0.5f + 0.25f;
            return new Vector3(cx, cy, cz);
        }
    }
}
