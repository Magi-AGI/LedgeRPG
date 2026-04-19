using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Magi.LedgeRPG
{
    /// Mouse-wheel zoom between the lattice scales exposed by ScaledLattice.
    /// Scroll DOWN steps to a coarser (zoomed-out) scale, scroll UP steps
    /// finer (zoomed-in) — matches the intuition that scrolling away pulls
    /// the camera back. Fires <see cref="OnScaleChanged"/> so listeners (the
    /// bootstrap) can swap the renderer's content without this controller
    /// knowing anything about LatticeWorld or aggregates.
    ///
    /// Mirrors ScaleZoomController from the 2D paper-mirror, but uses an int
    /// scale index instead of LedgeRPG.Scaled.ScaleLevel — the 3D substrate
    /// is a parallel architecture and shouldn't depend on the 2D enum.
    public sealed class LatticeZoomController : MonoBehaviour
    {
        public event Action<int> OnScaleChanged;

        public int MaxScale = 2;
        public int Current { get; private set; }

        private float _scrollAccumulator;
        private const float ScrollThreshold = 0.1f;

        public void SetScale(int scale)
        {
            if (scale < 0 || scale > MaxScale) return;
            if (scale == Current) return;
            Current = scale;
            OnScaleChanged?.Invoke(Current);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float y = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(y, 0f))
            {
                _scrollAccumulator = 0f;
                return;
            }

            _scrollAccumulator += y;
            if (_scrollAccumulator >= ScrollThreshold)
            {
                _scrollAccumulator = 0f;
                SetScale(Current - 1); // scroll up → finer
            }
            else if (_scrollAccumulator <= -ScrollThreshold)
            {
                _scrollAccumulator = 0f;
                SetScale(Current + 1); // scroll down → coarser
            }
        }
    }
}
