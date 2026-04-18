using LedgeRPG.Core;
using LedgeRPG.Core.Determinism;
using LedgeRPG.Core.World;
using LedgeRPG.Scaled;
using UnityEngine;

namespace Magi.LedgeRPG
{
    /// Single-entry-point MonoBehaviour for the paper-mirror scene.
    /// Drop onto a GameObject in LedgeRPGMain.unity (Main Camera + Directional
    /// Light expected alongside) and press Play: the bootstrap builds the hex
    /// grid, agent, and HUD at runtime, positions the main camera, and routes
    /// keyboard input through IRpgActionSubmitter. Mouse-wheel zoom snaps the
    /// camera between three scales exposed by ScaledWorld.
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

        private IRpgActionSubmitter _submitter;
        private ScaledWorld _scaled;
        private HexGridRenderer _renderer;
        private AgentView _agent;
        private HudView _hud;
        private ScaleZoomController _zoom;

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

            var zGo = new GameObject("ZoomController");
            zGo.transform.SetParent(transform, worldPositionStays: false);
            _zoom = zGo.AddComponent<ScaleZoomController>();
            _zoom.OnScaleChanged += OnScaleChanged;
        }

        private void NewRun()
        {
            var world = new World(Seed, GridSize, FoodCount, ObstacleCount, StepLimit);
            _submitter = new LocalRpgActionSubmitter(world);
            _scaled = new ScaledWorld(world);
            _renderer.Build(world);
            _renderer.MarkVisited(world.AgentPos);
            _agent.SetPosition(world.AgentPos);
            _hud.Refresh(_scaled, _zoom.Current);
            ConfigureZoom();
        }

        private void ConfigureZoom()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.fieldOfView = 60f;
            var centroid = _renderer.GridCentroid(_submitter.World.GridSize);
            float span = GridSize * TileSize;
            _zoom.Configure(cam, centroid, span);
        }

        private void OnScaleChanged(ScaleLevel level)
        {
            if (_scaled != null) _hud.Refresh(_scaled, level);
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
                _scaled.Invalidate();
                var world = _submitter.World;
                foreach (var d in deltas)
                    if (d is TileDiscoveredDelta td) _renderer.MarkVisited(td.At);
                _renderer.Refresh(world);
                _agent.SetPosition(world.AgentPos);
                _hud.Refresh(_scaled, _zoom.Current);
            }
            catch (InvalidActionException)
            {
                // Unknown enum or post-terminal — surface nothing, the HUD already reports state.
            }
        }
    }
}
