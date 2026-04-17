using LedgeRPG.Core;
using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Single-entry-point MonoBehaviour for the paper-mirror scene.
    /// Attach to any GameObject in an otherwise-empty scene and press Play:
    /// the bootstrap builds the hex grid, agent, and HUD at runtime, positions
    /// the main camera, and routes keyboard input through IRpgActionSubmitter.
    public sealed class LedgeRPGBootstrap : MonoBehaviour
    {
        [Header("Seeding (mirrors paper-server defaults)")]
        public long Seed = 42;
        public int GridSize = 8;
        public int FoodCount = 5;
        public int ObstacleCount = 8;
        public int StepLimit = 100;

        [Header("Rendering")]
        public float TileSize = 0.55f;

        // Auto-spawn in any play-mode scene so the first draft works without a
        // hand-wired scene. Remove this once a proper Main scene lands (M3+).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindFirstObjectByType<LedgeRPGBootstrap>() != null) return;
            var go = new GameObject("LedgeRPG");
            go.AddComponent<LedgeRPGBootstrap>();
        }

        private IRpgActionSubmitter _submitter;
        private HexGridRenderer _renderer;
        private AgentView _agent;
        private HudView _hud;

        private void Start()
        {
            BuildChildren();
            NewRun();
        }

        private void BuildChildren()
        {
            var rGo = new GameObject("HexGrid");
            rGo.transform.SetParent(transform, worldPositionStays: false);
            _renderer = rGo.AddComponent<HexGridRenderer>();
            _renderer.TileSize = TileSize;

            var aGo = new GameObject("Agent");
            aGo.transform.SetParent(transform, worldPositionStays: false);
            _agent = aGo.AddComponent<AgentView>();
            _agent.TileSize = TileSize;

            var hGo = new GameObject("Hud");
            hGo.transform.SetParent(transform, worldPositionStays: false);
            _hud = hGo.AddComponent<HudView>();
        }

        private void NewRun()
        {
            _submitter = new LocalRpgActionSubmitter(
                new World(Seed, GridSize, FoodCount, ObstacleCount, StepLimit));
            var world = _submitter.World;
            _renderer.Build(world);
            _renderer.MarkVisited(world.AgentPos);
            _agent.SetPosition(world.AgentPos);
            _hud.Refresh(world);
            PositionCamera();
        }

        private void PositionCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var centroid = _renderer.GridCentroid(_submitter.World.GridSize);
            float span = GridSize * TileSize;
            cam.transform.SetPositionAndRotation(
                centroid + new Vector3(0f, span * 1.5f, -span * 0.85f),
                Quaternion.Euler(60f, 0f, 0f));
            cam.fieldOfView = 60f;
        }

        private void Update()
        {
            if (_submitter == null) return;

            if (!_submitter.World.Done)
            {
                var action = KeyboardInputHandler.ReadActionThisFrame();
                if (action.HasValue) TryApply(action.Value);
                return;
            }

            if (KeyboardInputHandler.ResetPressedThisFrame())
            {
                Seed++;
                NewRun();
            }
        }

        private void TryApply(RPGActionKind action)
        {
            try
            {
                var deltas = _submitter.Submit(action);
                var world = _submitter.World;
                foreach (var d in deltas)
                    if (d is TileDiscoveredDelta td) _renderer.MarkVisited(td.At);
                _renderer.Refresh(world);
                _agent.SetPosition(world.AgentPos);
                _hud.Refresh(world);
            }
            catch (InvalidActionException)
            {
                // Unknown enum or post-terminal — surface nothing, the HUD already reports state.
            }
        }
    }
}
