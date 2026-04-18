using System.Text;
using LedgeRPG.Core.World;
using LedgeRPG.Scaled;
using UnityEngine;
using UnityEngine.UI;

namespace Magi.LedgeRPG
{
    public sealed class HudView : MonoBehaviour
    {
        private Text _text;

        private void Awake()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("HudText");
            textGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(16, -16);
            rt.sizeDelta = new Vector2(560, 380);

            _text = textGo.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 16;
            _text.color = Color.white;
            _text.alignment = TextAnchor.UpperLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);
        }

        public void Refresh(ScaledWorld scaled, ScaleLevel level)
        {
            var world = scaled.Source;
            string status = world.Done
                ? $"TERMINAL: {world.TerminalReason} (success={world.Success}) — press R to reseed"
                : "Playing";

            var sb = new StringBuilder();
            sb.Append("Seed: ").Append(world.Seed).Append('\n');
            sb.Append("Step: ").Append(world.Step).Append('/').Append(world.StepLimit).Append('\n');
            sb.Append("Energy: ").Append(world.Energy.ToString("F3")).Append('\n');
            sb.Append("Agent: (").Append(world.AgentPos.Q).Append(", ").Append(world.AgentPos.R).Append(")\n");
            sb.Append("Food: ").Append(world.FoodRemaining).Append('/').Append(world.FoodCount).Append(" remaining\n");
            sb.Append("Visited: ").Append(world.VisitedCount).Append('/').Append(world.TotalPassable).Append('\n');
            sb.Append('\n');
            sb.Append("Scale: ").Append(level).Append("  (scroll to change)\n");
            AppendScaleReadout(sb, scaled, level);
            sb.Append('\n');
            sb.Append("Controls: Q/W/E/A/S/D move • X examine • Space rest\n");
            sb.Append("Status: ").Append(status);

            _text.text = sb.ToString();
        }

        private static void AppendScaleReadout(StringBuilder sb, ScaledWorld scaled, ScaleLevel level)
        {
            switch (level)
            {
                case ScaleLevel.Scale0:
                    sb.Append("Tiles: ").Append(scaled.Source.TotalPassable).Append(" passable, ");
                    sb.Append(scaled.Source.FoodRemaining).Append(" food, ");
                    sb.Append(CountObstacles(scaled.Source)).Append(" obstacles\n");
                    break;
                case ScaleLevel.Scale1:
                    var regions = scaled.GetRegions();
                    sb.Append("Regions: ").Append(regions.Count).Append(" (size ").Append(scaled.RegionSize).Append(")\n");
                    foreach (var r in regions)
                    {
                        sb.Append("  (").Append(r.Coord.Q).Append(',').Append(r.Coord.R).Append("): ");
                        sb.Append(r.TileCount).Append(" tiles, ");
                        sb.Append(r.FoodCount).Append(" food, ");
                        sb.Append(r.ObstacleCount).Append(" obs");
                        if (r.HasAgent) sb.Append(" [AGENT]");
                        sb.Append('\n');
                    }
                    break;
                case ScaleLevel.Scale2:
                    var zones = scaled.GetZones();
                    sb.Append("Zones: ").Append(zones.Count).Append(" (size ").Append(scaled.ZoneSize).Append(")\n");
                    foreach (var z in zones)
                    {
                        sb.Append("  (").Append(z.Coord.Q).Append(',').Append(z.Coord.R).Append("): ");
                        sb.Append(z.RegionCount).Append(" regions, ");
                        sb.Append(z.TotalTiles).Append(" tiles, ");
                        sb.Append(z.TotalFood).Append(" food");
                        if (z.HasAgent) sb.Append(" [AGENT]");
                        sb.Append('\n');
                    }
                    break;
            }
        }

        private static int CountObstacles(World world)
        {
            int n = 0;
            foreach (var cell in world.GridSnapshot())
                if (cell.Type == TileType.Obstacle) n++;
            return n;
        }
    }
}
