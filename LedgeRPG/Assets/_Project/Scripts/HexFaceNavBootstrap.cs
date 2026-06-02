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
    /// Input:
    ///   • WASD generates a camera-relative direction vector (W = camera-
    ///     forward including pitch, A/D = camera-right strafe). Holding Space
    ///     adds a +Y bias. The face-neighbor whose normalized world delta
    ///     best matches the normalized intent vector wins. No modifier keys,
    ///     no per-direction hotkeys — camera pitch + jump-bias collectively
    ///     address all 14 face-neighbors:
    ///
    ///       look level + WASD    → 4 horizontal square faces
    ///       look up   + Space    → +Y square face
    ///       look up   + W        → hex-up forward
    ///       look up   + W + D    → hex-up forward-right corner
    ///       look down + W        → hex-down forward
    ///
    ///   • Mouse-pick waypoint nav: the cell under the screen-center reticle
    ///     is hovered; BFS over face-adjacency finds the shortest path from
    ///     the agent. Left-click commits and the agent auto-steps along the
    ///     path, tween-per-cell. Right-click cancels mid-path.
    ///
    /// Walker-drift policy does NOT apply here — the discrete-step mode is
    /// exactly the UI-tight context flagged by the input-rework resolution
    /// as appropriate for snap-to-cell traversal and auto-pathing.
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

        [Header("Mouse-pick")]
        public float PickMaxDistance = 30f;
        public int PathMaxCells = 50;
        public int BfsVisitedCap = 600;

        [Header("Camera (third-person pivot)")]
        public float CameraDistance = 3f;
        public float CameraHeight = 1.2f;
        public float CameraFov = 60f;

        // Face-neighbor class colors.
        private static readonly Color SquareHColor = new Color(0.55f, 0.75f, 1.00f, 1.00f);
        private static readonly Color SquareVColor = new Color(0.65f, 1.00f, 0.55f, 1.00f);
        private static readonly Color HexUpColor   = new Color(1.00f, 0.95f, 0.55f, 1.00f);
        private static readonly Color HexDownColor = new Color(1.00f, 0.65f, 0.40f, 1.00f);
        private static readonly Color WinnerColor  = new Color(1.00f, 1.00f, 1.00f, 1.00f);
        // Path preview colors (intermediate cells and hover target).
        private static readonly Color PathColor    = new Color(0.80f, 0.55f, 1.00f, 1.00f);
        private static readonly Color PathEndColor = new Color(1.00f, 0.45f, 1.00f, 1.00f);

        private LatticeWorld _world;
        private HexSliceRenderer _renderer;

        private Transform _agentRoot;
        private Transform _cameraTarget;
        private float _yaw;
        private float _pitch;

        private bool _animating;
        private string _lastStepResult = "-";

        // Mouse-pick state.
        private ToctaCoord? _hoverTarget;
        private List<ToctaCoord> _hoverPath;          // null if no path
        private List<ToctaCoord> _activePath;         // currently auto-stepping
        private bool _cancelAutoStep;

        private void Start()
        {
            _world = new LatticeWorld(Seed, SizeX, SizeY, SizeZ, BlockedCount);

            var rGo = new GameObject("LatticeTerrain");
            rGo.transform.SetParent(transform, worldPositionStays: false);
            _renderer = rGo.AddComponent<HexSliceRenderer>();
            _renderer.AddColliders = true;           // needed for mouse-pick raycasting
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

            UpdateHover();
            HandleMouseButtons(mouse);
            UpdateCandidatePreview();

            if (_animating) return;
            if (_activePath != null) return;   // auto-step running via coroutine

            Vector3 intent = ComputeIntent(kb);
            if (intent.sqrMagnitude < MinIntentMagnitude * MinIntentMagnitude) return;

            var winner = PickFace(intent);
            if (!winner.HasValue) return;
            TryStepAndAnimate(winner.Value);
        }

        private void TryStepAndAnimate(ToctaCoord target)
        {
            var delta = _world.TryStep(target);
            switch (delta)
            {
                case AgentMovedDelta moved:
                    _renderer.SetAgent(moved.To);
                    StartCoroutine(AnimateStepOnce(moved.From, moved.To));
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
            // Left-click re-locks the cursor after Esc. Path-commit is also on
            // left-click, but only while locked — see HandleMouseButtons.
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void HandleMouseButtons(Mouse mouse)
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (_activePath != null)
                {
                    _cancelAutoStep = true;
                    _lastStepResult = "path cancelled";
                }
            }

            if (mouse.leftButton.wasPressedThisFrame && _activePath == null)
            {
                if (_hoverPath != null && _hoverPath.Count >= 2)
                {
                    _activePath = _hoverPath;
                    _cancelAutoStep = false;
                    StartCoroutine(AutoStepAlongPath(_activePath));
                }
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

        private void UpdateHover()
        {
            var cam = Camera.main;
            if (cam == null || _renderer == null || _world == null)
            {
                _hoverTarget = null;
                _hoverPath = null;
                return;
            }

            var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            if (!Physics.Raycast(ray, out var hit, PickMaxDistance))
            {
                _hoverTarget = null;
                _hoverPath = null;
                return;
            }
            if (!_renderer.TryGetCoord(hit.collider.gameObject, out var coord))
            {
                _hoverTarget = null;
                _hoverPath = null;
                return;
            }
            if (_hoverTarget.HasValue && _hoverTarget.Value.Equals(coord)) return;

            _hoverTarget = coord;
            _hoverPath = (coord.Equals(_world.AgentPos) || _world.TypeAt(coord) != ToctaType.Passable)
                ? null
                : BfsPath(_world.AgentPos, coord);
        }

        /// Breadth-first shortest path over passable face-neighbors. Returns
        /// null for blocked / out-of-bounds / unreachable / too-long targets.
        /// Face-adjacency cost is uniform (1 per step) so BFS = Dijkstra here.
        private List<ToctaCoord> BfsPath(ToctaCoord src, ToctaCoord dst)
        {
            if (!_world.InBounds(dst)) return null;
            if (_world.TypeAt(dst) != ToctaType.Passable) return null;
            if (src.Equals(dst)) return new List<ToctaCoord> { src };

            var parents = new Dictionary<ToctaCoord, ToctaCoord>(64);
            var visited = new HashSet<ToctaCoord> { src };
            var queue = new Queue<ToctaCoord>(64);
            queue.Enqueue(src);

            while (queue.Count > 0)
            {
                if (visited.Count > BfsVisitedCap) return null;
                var cur = queue.Dequeue();
                foreach (var n in ToctaNeighbors.FaceNeighbors(cur))
                {
                    if (!visited.Add(n)) continue;
                    if (!_world.InBounds(n)) continue;
                    if (_world.TypeAt(n) != ToctaType.Passable) continue;
                    parents[n] = cur;
                    if (n.Equals(dst))
                    {
                        var path = new List<ToctaCoord>();
                        var c = dst;
                        while (!c.Equals(src))
                        {
                            path.Add(c);
                            c = parents[c];
                        }
                        path.Add(src);
                        path.Reverse();
                        return path.Count <= PathMaxCells ? path : null;
                    }
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        private void UpdateCandidatePreview()
        {
            if (_renderer == null || _world == null) return;

            var src = _world.AgentPos;
            var highlights = new Dictionary<ToctaCoord, Color>(ToctaNeighbors.FaceCount + PathMaxCells);

            // Layer 1: 14 face-neighbors by class.
            foreach (var n in ToctaNeighbors.FaceNeighbors(src))
                highlights[n] = ColorForClass(src, n);

            // Layer 2: auto-step remaining path (overrides face-neighbor color).
            if (_activePath != null)
            {
                for (int i = 1; i < _activePath.Count; i++)
                    highlights[_activePath[i]] = PathColor;
                if (_activePath.Count >= 2)
                    highlights[_activePath[_activePath.Count - 1]] = PathEndColor;
            }
            // Layer 2 alt: hover path preview (if no active path).
            else if (_hoverPath != null)
            {
                for (int i = 1; i < _hoverPath.Count; i++)
                    highlights[_hoverPath[i]] = PathColor;
                if (_hoverPath.Count >= 2)
                    highlights[_hoverPath[_hoverPath.Count - 1]] = PathEndColor;
            }

            // Layer 3: WASD winner (brightest; only when no path is active/hovered).
            if (_activePath == null && _hoverPath == null)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    Vector3 intent = ComputeIntent(kb);
                    if (intent.sqrMagnitude >= MinIntentMagnitude * MinIntentMagnitude)
                    {
                        var winner = PickFace(intent);
                        if (winner.HasValue) highlights[winner.Value] = WinnerColor;
                    }
                }
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

        private IEnumerator AnimateStepOnce(ToctaCoord from, ToctaCoord to)
        {
            _animating = true;
            yield return TweenPosition(from, to);
            _animating = false;
        }

        private IEnumerator AutoStepAlongPath(List<ToctaCoord> path)
        {
            _animating = true;
            for (int i = 1; i < path.Count; i++)
            {
                if (_cancelAutoStep) break;
                var from = _world.AgentPos;
                var to = path[i];
                var delta = _world.TryStep(to);
                if (delta is MovementBlockedDelta blocked)
                {
                    _lastStepResult = $"auto-step blocked → {blocked.AttemptedTo} ({blocked.Reason})";
                    break;
                }
                _renderer.SetAgent(to);
                _lastStepResult = $"auto-step {from}→{to}  ({i}/{path.Count - 1})";
                yield return TweenPosition(from, to);
            }
            _activePath = null;
            _cancelAutoStep = false;
            _animating = false;
        }

        private IEnumerator TweenPosition(ToctaCoord from, ToctaCoord to)
        {
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
        }

        private void OnGUI()
        {
            const int pad = 8;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Label(new Rect(pad, pad,      900, 22),
                "Hex-face nav — WASD step, Space biases up, left-click paths, right-click cancels, Esc unlock", style);
            if (_world == null) return;

            var a = _world.AgentPos;
            GUI.Label(new Rect(pad, pad + 22, 900, 22), $"Agent ({a.X},{a.Y},{a.Z})", style);
            string hoverStr = _hoverTarget.HasValue
                ? (_hoverPath != null ? $"hover ({_hoverTarget.Value.X},{_hoverTarget.Value.Y},{_hoverTarget.Value.Z})  path {_hoverPath.Count - 1} steps"
                                      : $"hover ({_hoverTarget.Value.X},{_hoverTarget.Value.Y},{_hoverTarget.Value.Z})  unreachable")
                : "hover —";
            GUI.Label(new Rect(pad, pad + 44, 900, 22), hoverStr, style);
            GUI.Label(new Rect(pad, pad + 66, 900, 22), $"Last: {_lastStepResult}", style);

            // Reticle at screen center.
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float r = 6f;
            GUI.DrawTexture(new Rect(cx - r, cy - 1f, r * 2f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy - r, 2f, r * 2f), Texture2D.whiteTexture);
        }
    }
}
