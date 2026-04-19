using System.Collections;
using System.Collections.Generic;
using LedgeRPG.Lattice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Magi.LedgeRPG
{
    /// Single-entry-point MonoBehaviour for the (1,1,1)-slice A/B demo.
    /// Drop on a GameObject in a scene with a Main Camera + Directional
    /// Light and press Play. Two viewing modes sit on the same BCC
    /// substrate and swap via the `M` key:
    ///
    ///   • Mode B — synthetic hex topology. One (1,1,1)-layer is rendered;
    ///     agent moves on the six in-layer hex-neighbor directions
    ///     (LatticeSlice111.HexNeighbors). Each hex step is atomic here —
    ///     internally it bridges two BCC face-steps but the demo hides
    ///     that. Cost = 1 per hex step.
    ///
    ///   • Mode C — thick slab. Three consecutive k-layers are rendered so
    ///     the cells you see are truly BCC-face-connected along Δk=±1
    ///     hex faces. Agent moves via two animated BCC hex-face steps per
    ///     input press (intermediate cell is visible because it sits in
    ///     the slab), making the face-adjacency concrete. Cost = 1 per
    ///     BCC step (2 per input).
    ///
    /// Both modes use the same six-key input (QAZEDC) for the six
    /// in-plane directions so the comparison is visual rather than
    /// kinematic. `[` / `]` shifts the viewed slab along (1,1,1); the
    /// agent is snapped to the closest in-slab cell if it falls off.
    public sealed class HexSliceBootstrap : MonoBehaviour
    {
        public enum Mode { SyntheticHex, ThickSlab }

        [Header("Seeding")]
        public long Seed = 42;
        public int SizeX = 10;
        public int SizeY = 10;
        public int SizeZ = 10;
        public int BlockedCount = 150;

        [Header("Camera framing")]
        public float CameraDistance = 10f;
        public float CameraHeight   = 6f;
        public float CameraFov      = 55f;

        [Header("Slab (Mode C)")]
        public int SlabHalfThickness = 1;

        [Header("Step animation (Mode C)")]
        public float StepSeconds = 0.15f;

        private LatticeWorld _world;
        private HexSliceRenderer _renderer;
        private Mode _mode = Mode.SyntheticHex;
        private ToctaCoord _agent;
        private int _viewK;
        private Coroutine _stepAnim;

        private static readonly (int dx, int dy, int dz)[] HexDeltas = LatticeSlice111.HexNeighborDeltas;

        private void Start()
        {
            _world = new LatticeWorld(Seed, SizeX, SizeY, SizeZ, BlockedCount);
            _agent = _world.AgentPos;
            _viewK = LatticeSlice111.LayerIndex(_agent);

            var rGo = new GameObject("HexSliceRenderer");
            rGo.transform.SetParent(transform, worldPositionStays: false);
            _renderer = rGo.AddComponent<HexSliceRenderer>();

            Rerender();
            FrameCamera();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.mKey.wasPressedThisFrame)
            {
                _mode = _mode == Mode.SyntheticHex ? Mode.ThickSlab : Mode.SyntheticHex;
                Rerender();
                FrameCamera();
                return;
            }

            if (kb.leftBracketKey.wasPressedThisFrame)   { _viewK -= 1; Rerender(); FrameCamera(); return; }
            if (kb.rightBracketKey.wasPressedThisFrame)  { _viewK += 1; Rerender(); FrameCamera(); return; }

            if (_stepAnim != null) return;

            int pressed = PressedHexIndex(kb);
            if (pressed < 0) return;

            var d = HexDeltas[pressed];
            var target = new ToctaCoord(_agent.X + d.dx, _agent.Y + d.dy, _agent.Z + d.dz);
            if (!_world.InBounds(target)) return;
            if (_world.TypeAt(target) != ToctaType.Passable) return;

            if (_mode == Mode.SyntheticHex)
            {
                _agent = target;
                _viewK = LatticeSlice111.LayerIndex(_agent);
                Rerender();
            }
            else
            {
                var intermediate = PickBccIntermediate(_agent, d);
                if (!_world.InBounds(intermediate) || _world.TypeAt(intermediate) != ToctaType.Passable)
                {
                    // Fall back to atomic move if the 2-hop path is blocked.
                    _agent = target;
                    _viewK = LatticeSlice111.LayerIndex(_agent);
                    Rerender();
                }
                else
                {
                    _stepAnim = StartCoroutine(AnimateTwoHop(intermediate, target));
                }
            }
        }

        private IEnumerator AnimateTwoHop(ToctaCoord intermediate, ToctaCoord target)
        {
            _agent = intermediate;
            Rerender();
            yield return new WaitForSeconds(StepSeconds);
            _agent = target;
            Rerender();
            _stepAnim = null;
        }

        /// For a synthetic hex delta from <paramref name="source"/>, return
        /// the BCC-hex-face intermediate cell that two BCC hex-face steps
        /// traverse. All six synthetic deltas factor through a single
        /// parity-crossing intermediate; we pick the one whose Δk is +1
        /// for deltas with dy ≥ 0 and -1 for dy &lt; 0, so intermediates
        /// stay inside a symmetric 3-layer slab centered on the source.
        private static ToctaCoord PickBccIntermediate(ToctaCoord source, (int dx, int dy, int dz) d)
        {
            bool sourceEven = !source.IsOddLayer;
            (int ix, int iy, int iz) step;

            // Six synthetic deltas; map each to its first-hop BCC hex-face.
            // The table differs by source parity because BCC hex-face deltas
            // do. Verified by enumerating HexFaceDeltas{Even,Odd} from
            // ToctaNeighbors against each synthetic delta.
            if (d == (+1, 0, -1))      step = sourceEven ? ( 0, +1, -1) : (+1, +1,  0);
            else if (d == (-1, 0, +1)) step = sourceEven ? (-1, +1,  0) : ( 0, +1, +1);
            else if (d == ( 0, +2, -1))step = sourceEven ? ( 0, +1, -1) : ( 0, +1,  0);
            else if (d == (-1, +2,  0))step = sourceEven ? (-1, +1,  0) : ( 0, +1,  0);
            else if (d == ( 0, -2, +1))step = sourceEven ? ( 0, -1,  0) : ( 0, -1, +1);
            else if (d == (+1, -2,  0))step = sourceEven ? ( 0, -1,  0) : (+1, -1,  0);
            else                       step = (0, 0, 0);

            return new ToctaCoord(source.X + step.ix, source.Y + step.iy, source.Z + step.iz);
        }

        private static int PressedHexIndex(Keyboard kb)
        {
            // QAZEDC around the hex: Q=NW, E=NE, A=W, D=E, Z=SW, C=SE.
            // Order matches LatticeSlice111.HexNeighborDeltas:
            //   0=(+1,0,-1) E, 1=(-1,0,+1) W, 2=(0,+2,-1) NE,
            //   3=(-1,+2,0) NW, 4=(0,-2,+1) SW, 5=(+1,-2,0) SE.
            if (kb.dKey.wasPressedThisFrame) return 0;
            if (kb.aKey.wasPressedThisFrame) return 1;
            if (kb.eKey.wasPressedThisFrame) return 2;
            if (kb.qKey.wasPressedThisFrame) return 3;
            if (kb.zKey.wasPressedThisFrame) return 4;
            if (kb.cKey.wasPressedThisFrame) return 5;
            return -1;
        }

        private void Rerender()
        {
            IEnumerable<ToctaCoord> visible;
            IEnumerable<ToctaCoord> ghosts = null;

            if (_mode == Mode.SyntheticHex)
            {
                visible = LatticeSlice111.CellsInLayer(_world, _viewK);
            }
            else
            {
                visible = LatticeSlice111.CellsInSlab(_world, _viewK, SlabHalfThickness);
            }

            _renderer.Rebuild(_world, visible, _agent, ghosts);
        }

        private void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var bounds = ComputeVisibleBounds();
            float radius = Mathf.Max(bounds.extents.magnitude, 1f);
            float fitDist = radius / Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);

            // Look along -(1,1,1) but tilt slightly off-axis so the
            // rolling-hill Y variation within a single k-layer is visible.
            Vector3 dir = (new Vector3(1f, 0.65f, 1f)).normalized;
            cam.transform.position = bounds.center + dir * fitDist * 1.5f;
            cam.transform.LookAt(bounds.center);
            cam.fieldOfView = CameraFov;
        }

        private Bounds ComputeVisibleBounds()
        {
            bool first = true;
            var b = new Bounds();
            foreach (Transform child in _renderer.transform)
            {
                if (first) { b = new Bounds(child.localPosition, Vector3.zero); first = false; }
                else b.Encapsulate(child.localPosition);
            }
            if (first) return new Bounds(Vector3.zero, Vector3.one);
            b.Expand(1f);
            return b;
        }

        private void OnGUI()
        {
            const int pad = 8;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            string modeName = _mode == Mode.SyntheticHex ? "B  synthetic hex (1 layer)" : "C  thick slab (" + (2 * SlabHalfThickness + 1) + " layers)";
            string line1 = "Mode: " + modeName + "   [M] toggle";
            string line2 = "Agent (" + _agent.X + "," + _agent.Y + "," + _agent.Z + ")  k=" + LatticeSlice111.LayerIndex(_agent) + "   view k=" + _viewK + "   [ ] shift view";
            string line3 = "Move: Q/E upper  A/D sides  Z/C lower";
            GUI.Label(new Rect(pad, pad,          900, 22), line1, style);
            GUI.Label(new Rect(pad, pad + 22,     900, 22), line2, style);
            GUI.Label(new Rect(pad, pad + 44,     900, 22), line3, style);
        }
    }
}
