using System.Collections;
using System.Collections.Generic;
using LedgeRPG.Lattice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Magi.LedgeRPG
{
    /// Discrete 14-direction stepper on a BCC lattice. Replaces the walker's
    /// continuous CharacterController with explicit cell-by-cell hopping —
    /// the agent always sits at a cell center, never drifts off.
    ///
    /// Input: WASD generates a camera-relative direction vector
    /// (W = camera-forward including pitch, A/D = camera-right strafe).
    /// Holding Space adds a +Y bias to the intent vector. The face-neighbor
    /// of the agent's current cell whose normalized world delta best matches
    /// the normalized intent vector wins. No modifier keys, no per-direction
    /// hotkeys — camera pitch and the jump-bias collectively address all 14
    /// face-neighbors:
    ///
    ///   look level + WASD    → 4 horizontal square faces
    ///   look up   + Space    → +Y square face
    ///   look up   + W        → hex-up forward
    ///   look up   + W + D    → hex-up forward-right corner
    ///   look down + W        → hex-down forward
    ///
    /// While a step is animating (lerp ~0.15s between cell centers) input is
    /// ignored. The 14 candidate cells are color-classed (horizontal square
    /// / vertical square / hex-up / hex-down) and the currently winning
    /// candidate is brightened to give the player live feedback.
    public sealed class HexFaceNavBootstrap : MonoBehaviour
    {
        [Header("World")]
        public long Seed = 42;
        public int SizeX = 12;
        public int SizeY = 6;
        public int SizeZ = 12;
        public int BlockedCount = 120;

        [Header("Look")]
        public float MouseSensitivity = 0.12f;

        [Header("Step")]
        public float StepSeconds = 0.15f;
        public float JumpYBias = 0.6f;
        public float MinIntentMagnitude = 0.05f;

        [Header("Camera (third-person pivot)")]
        public float CameraDistance = 3f;
        public float CameraHeight = 1.2f;
        public float CameraFov = 60f;

        // Candidate highlight colors, one per face-neighbor class.
        private static readonly Color SquareHColor = new Color(0.55f, 0.75f, 1.00f, 1.00f);
        private static readonly Color SquareVColor = new Color(0.65f, 1.00f, 0.55f, 1.00f);
        private static readonly Color HexUpColor   = new Color(1.00f, 0.95f, 0.55f, 1.00f);
        private static readonly Color HexDownColor = new Color(1.00f, 0.65f, 0.40f, 1.00f);
        private static readonly Color WinnerColor  = new Color(1.00f, 1.00f, 1.00f, 1.00f);

        private LatticeWorld _world;
        private HexSliceRenderer _renderer;

        private Transform _agentRoot;
        private Transform _cameraTarget;
        private float _yaw;
        private float _pitch;

        private bool _animating;
        private string _lastStepResult = "-";

        private void Start()
        {
            _world = new LatticeWorld(Seed, SizeX, SizeY, SizeZ, BlockedCount);

            var rGo = new GameObject("LatticeTerrain");
            rGo.transform.SetParent(transform, worldPositionStays: false);
            _renderer = rGo.AddComponent<HexSliceRenderer>();
            _renderer.AddColliders = false;
            _renderer.Rebuild(_world, _world.AllCoords(), _world.AgentPos, null);

            SpawnAgentPivot();
            AttachCamera();
            UpdateCandidatePreview();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void SpawnAgentPivot()
        {
            var go = new GameObject("AgentPivot");
            go.transform.SetParent(transform, worldPositionStays: false);
            _agentRoot = go.transform;

            var (wx, wy, wz) = _world.AgentPos.WorldPosition;
            _agentRoot.position = new Vector3((float)wx, (float)wy, (float)wz);

            var ct = new GameObject("CameraTarget");
            ct.transform.SetParent(_agentRoot, worldPositionStays: false);
            ct.transform.localPosition = Vector3.zero;
            _cameraTarget = ct.transform;
        }

        private void AttachCamera()
        {
            var cam = Camera.main;
            if (cam == null || _cameraTarget == null) return;
            cam.transform.SetParent(_cameraTarget, worldPositionStays: false);
            cam.transform.localPosition = new Vector3(0f, CameraHeight, -CameraDistance);
            cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.05f;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            HandleCursor(kb, mouse);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue();
                _yaw += d.x * MouseSensitivity;
                _pitch -= d.y * MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
                if (_agentRoot != null)
                    _agentRoot.rotation = Quaternion.Euler(0f, _yaw, 0f);
                if (_cameraTarget != null)
                    _cameraTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            UpdateCandidatePreview();

            if (_animating) return;

            Vector3 intent = ComputeIntent(kb);
            if (intent.sqrMagnitude < MinIntentMagnitude * MinIntentMagnitude) return;

            var winner = PickFace(intent);
            if (!winner.HasValue) return;

            var delta = _world.TryStep(winner.Value);
            switch (delta)
            {
                case AgentMovedDelta moved:
                    _renderer.SetAgent(moved.To);
                    StartCoroutine(AnimateStep(moved.From, moved.To));
                    _lastStepResult = $"moved {moved.From}→{moved.To}";
                    break;
                case MovementBlockedDelta blocked:
                    _lastStepResult = $"blocked → {blocked.AttemptedTo} ({blocked.Reason})";
                    break;
                default:
                    _lastStepResult = "?";
                    break;
            }
        }

        private static void HandleCursor(Keyboard kb, Mouse mouse)
        {
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private Vector3 ComputeIntent(Keyboard kb)
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            Vector3 fwd = cam.transform.forward;
            Vector3 right = cam.transform.right;
            Vector3 v = Vector3.zero;
            if (kb.wKey.isPressed) v += fwd;
            if (kb.sKey.isPressed) v -= fwd;
            if (kb.dKey.isPressed) v += right;
            if (kb.aKey.isPressed) v -= right;
            if (kb.spaceKey.isPressed) v.y += JumpYBias;
            return v;
        }

        private ToctaCoord? PickFace(Vector3 intent)
        {
            Vector3 norm = intent.normalized;
            ToctaCoord src = _world.AgentPos;
            var (sx, sy, sz) = src.WorldPosition;

            float bestDot = float.NegativeInfinity;
            ToctaCoord? best = null;
            foreach (var n in ToctaNeighbors.FaceNeighbors(src))
            {
                var (nx, ny, nz) = n.WorldPosition;
                Vector3 d = new Vector3((float)(nx - sx), (float)(ny - sy), (float)(nz - sz));
                float mag = d.magnitude;
                if (mag <= 0f) continue;
                float dot = Vector3.Dot(d / mag, norm);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = n;
                }
            }
            return best;
        }

        private void UpdateCandidatePreview()
        {
            if (_renderer == null || _world == null) return;

            var kb = Keyboard.current;
            ToctaCoord? winner = null;
            if (kb != null)
            {
                Vector3 intent = ComputeIntent(kb);
                if (intent.sqrMagnitude >= MinIntentMagnitude * MinIntentMagnitude)
                    winner = PickFace(intent);
            }

            var src = _world.AgentPos;
            var highlights = new Dictionary<ToctaCoord, Color>(ToctaNeighbors.FaceCount);
            foreach (var n in ToctaNeighbors.FaceNeighbors(src))
            {
                Color c = winner.HasValue && n.Equals(winner.Value)
                    ? WinnerColor
                    : ColorForClass(src, n);
                highlights[n] = c;
            }
            _renderer.SetCandidates(highlights);
        }

        private static Color ColorForClass(ToctaCoord src, ToctaCoord nbr)
        {
            int dy = nbr.Y - src.Y;
            if (dy == 0) return SquareHColor;
            if (Mathf.Abs(dy) == 2) return SquareVColor;
            if (dy == +1) return HexUpColor;
            return HexDownColor;
        }

        private IEnumerator AnimateStep(ToctaCoord from, ToctaCoord to)
        {
            _animating = true;
            var (fx, fy, fz) = from.WorldPosition;
            var (tx, ty, tz) = to.WorldPosition;
            Vector3 a = new Vector3((float)fx, (float)fy, (float)fz);
            Vector3 b = new Vector3((float)tx, (float)ty, (float)tz);
            float t0 = Time.time;
            float dur = Mathf.Max(StepSeconds, 1e-4f);
            while (true)
            {
                float u = (Time.time - t0) / dur;
                if (u >= 1f) break;
                if (_agentRoot != null) _agentRoot.position = Vector3.Lerp(a, b, u);
                yield return null;
            }
            if (_agentRoot != null) _agentRoot.position = b;
            _animating = false;
        }

        private void OnGUI()
        {
            const int pad = 8;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Label(new Rect(pad, pad,      900, 22),
                "Hex-face nav — WASD step, Space biases up, mouse look, Esc unlock cursor", style);
            if (_world == null) return;

            var a = _world.AgentPos;
            GUI.Label(new Rect(pad, pad + 22, 900, 22), $"Agent ({a.X},{a.Y},{a.Z})", style);
            GUI.Label(new Rect(pad, pad + 44, 900, 22), $"Last step: {_lastStepResult}", style);
        }
    }
}
