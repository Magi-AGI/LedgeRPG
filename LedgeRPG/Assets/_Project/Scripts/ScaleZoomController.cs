using System;
using LedgeRPG.Scaled;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Magi.LedgeRPG
{
    /// Mouse-wheel zoom between the three scales exposed by LedgeRPG.Scaled.
    /// Scroll up steps to a coarser scale; scroll down steps finer. The camera
    /// snaps to a precomputed pose per scale and fires <see cref="OnScaleChanged"/>
    /// so HUD and other listeners can re-project their readout.
    ///
    /// Positional math stays framework-agnostic: the bootstrap supplies a centroid
    /// and a base span, and the controller scales both camera height and back-off
    /// by a per-level multiplier. Good enough to feel the transition — a real
    /// multi-scale camera would do LOD swaps and smooth interpolation.
    public sealed class ScaleZoomController : MonoBehaviour
    {
        public event Action<ScaleLevel> OnScaleChanged;

        private Camera _cam;
        private Vector3 _centroid;
        private float _baseSpan;
        private ScaleLevel _current = ScaleLevel.Scale0;
        private float _scrollAccumulator;

        private const float ScrollThreshold = 0.1f;

        public ScaleLevel Current => _current;

        public void Configure(Camera cam, Vector3 centroid, float baseSpan)
        {
            _cam = cam;
            _centroid = centroid;
            _baseSpan = baseSpan;
            ApplyPose();
        }

        public void SetScale(ScaleLevel level)
        {
            if (level == _current) return;
            _current = level;
            ApplyPose();
            OnScaleChanged?.Invoke(_current);
        }

        private void Update()
        {
            if (_cam == null) return;
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
                StepCoarser();
            }
            else if (_scrollAccumulator <= -ScrollThreshold)
            {
                _scrollAccumulator = 0f;
                StepFiner();
            }
        }

        private void StepCoarser()
        {
            if (_current == ScaleLevel.Scale2) return;
            SetScale(_current + 1);
        }

        private void StepFiner()
        {
            if (_current == ScaleLevel.Scale0) return;
            SetScale(_current - 1);
        }

        private void ApplyPose()
        {
            if (_cam == null) return;
            float heightMul = _current switch
            {
                ScaleLevel.Scale0 => 1.5f,
                ScaleLevel.Scale1 => 3.0f,
                ScaleLevel.Scale2 => 5.5f,
                _ => 1.5f,
            };
            float backMul = _current switch
            {
                ScaleLevel.Scale0 => 0.85f,
                ScaleLevel.Scale1 => 1.4f,
                ScaleLevel.Scale2 => 2.2f,
                _ => 0.85f,
            };
            float tiltDeg = _current == ScaleLevel.Scale2 ? 80f : 60f;

            _cam.transform.SetPositionAndRotation(
                _centroid + new Vector3(0f, _baseSpan * heightMul, -_baseSpan * backMul),
                Quaternion.Euler(tiltDeg, 0f, 0f));
        }
    }
}
