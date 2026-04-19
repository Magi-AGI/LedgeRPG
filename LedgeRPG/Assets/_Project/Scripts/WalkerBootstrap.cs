using LedgeRPG.Lattice;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Entry-point MonoBehaviour for the "walk on the lattice" demo.
    /// Spawns a LatticeWorld, renders every cell with a MeshCollider, and
    /// drops a CharacterController capsule above the lattice so gravity
    /// lands it on top. WASD + mouse-look via WalkerCharacter; current
    /// occupied ToctaCoord is projected from the capsule body center each
    /// frame via ToctaCoord.FromWorldPosition.
    ///
    /// Phase 1 (continuous movement on collision) + Phase 2 (cell
    /// occupancy readout) + Phase 3 (cell transitions → LatticeWorld.TryStep).
    /// When the walker crosses into a new cell that's face-adjacent to the
    /// current agent, TryStep succeeds and the agent highlight updates.
    /// When it isn't (player jumped, ran fast, clipped a corner), TryStep
    /// rejects and the HUD shows the BlockReason — the visual position and
    /// game-rule position are allowed to drift so the mismatch is visible.
    public sealed class WalkerBootstrap : MonoBehaviour
    {
        [Header("World")]
        public long Seed = 42;
        public int SizeX = 12;
        public int SizeY = 4;
        public int SizeZ = 12;
        public int BlockedCount = 180;

        [Header("Character")]
        public float MoveSpeed = 4f;
        public float MouseSensitivity = 0.12f;
        public float JumpSpeed = 5f;

        [Header("Camera (third-person)")]
        public float CameraDistance = 4f;
        public float CameraHeight = 0.2f;
        public float CameraFov = 60f;

        private LatticeWorld _world;
        private HexSliceRenderer _renderer;
        private WalkerCharacter _character;

        private ToctaCoord _walkerCell;
        private bool _walkerCellKnown;
        private string _lastStepResult = "-";

        private void Start()
        {
            _world = new LatticeWorld(Seed, SizeX, SizeY, SizeZ, BlockedCount);

            var rGo = new GameObject("LatticeTerrain");
            rGo.transform.SetParent(transform, worldPositionStays: false);
            _renderer = rGo.AddComponent<HexSliceRenderer>();
            _renderer.AddColliders = true;
            _renderer.Rebuild(_world, _world.AllCoords(), _world.AgentPos, null);

            SpawnCharacter();
            AttachCamera();
        }

        private void SpawnCharacter()
        {
            var go = new GameObject("Walker");
            go.transform.SetParent(transform, worldPositionStays: false);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.4f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0f, 0.7f, 0f);
            cc.slopeLimit = 60f;
            cc.stepOffset = 0.3f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "WalkerVisual";
            var vcol = visual.GetComponent<Collider>();
            if (vcol != null) Destroy(vcol);
            visual.transform.SetParent(go.transform, worldPositionStays: false);
            visual.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            visual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            var camTarget = new GameObject("CameraTarget").transform;
            camTarget.SetParent(go.transform, worldPositionStays: false);
            camTarget.localPosition = new Vector3(0f, 1.3f, 0f);

            _character = go.AddComponent<WalkerCharacter>();
            _character.MoveSpeed = MoveSpeed;
            _character.MouseSensitivity = MouseSensitivity;
            _character.JumpSpeed = JumpSpeed;
            _character.CameraTarget = camTarget;

            var (wx, _, wz) = _world.AgentPos.WorldPosition;
            go.transform.position = new Vector3(
                (float)wx,
                SizeY * 0.5f + 3f,
                (float)wz);
        }

        private void AttachCamera()
        {
            var cam = Camera.main;
            if (cam == null || _character == null || _character.CameraTarget == null) return;

            cam.transform.SetParent(_character.CameraTarget, worldPositionStays: false);
            cam.transform.localPosition = new Vector3(0f, CameraHeight, -CameraDistance);
            cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.05f;
        }

        private void Update()
        {
            if (_character == null || _world == null) return;

            // Sample body center — 0.7 above feet — so the reported cell is
            // the one containing the character's bulk, not the floor below.
            var body = _character.transform.position + Vector3.up * 0.7f;
            var cell = ToctaCoord.FromWorldPosition(body.x, body.y, body.z);

            if (_walkerCellKnown && cell.Equals(_walkerCell)) return;

            _walkerCell = cell;
            _walkerCellKnown = true;

            if (cell.Equals(_world.AgentPos))
            {
                _lastStepResult = "on agent cell";
                return;
            }

            var delta = _world.TryStep(cell);
            switch (delta)
            {
                case AgentMovedDelta moved:
                    _renderer.SetAgent(moved.To);
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

        private void OnGUI()
        {
            const int pad = 8;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Label(new Rect(pad, pad,       700, 22), "Walker demo — WASD move, mouse look, Space jump, Esc unlock cursor", style);
            if (_character == null || _world == null) return;

            var p = _character.transform.position;
            string cellStatus = !_walkerCellKnown
                ? "(pending)"
                : !_world.InBounds(_walkerCell) ? "out of bounds"
                    : _world.TypeAt(_walkerCell) == ToctaType.Passable ? "passable" : "blocked";

            var agent = _world.AgentPos;
            GUI.Label(new Rect(pad, pad + 22, 700, 22),
                $"Pos ({p.x:F2}, {p.y:F2}, {p.z:F2})", style);
            GUI.Label(new Rect(pad, pad + 44, 700, 22),
                $"Walker cell ({_walkerCell.X},{_walkerCell.Y},{_walkerCell.Z}) {cellStatus}", style);
            GUI.Label(new Rect(pad, pad + 66, 700, 22),
                $"Agent cell  ({agent.X},{agent.Y},{agent.Z})", style);
            GUI.Label(new Rect(pad, pad + 88, 700, 22),
                $"Last step: {_lastStepResult}", style);
        }
    }
}
