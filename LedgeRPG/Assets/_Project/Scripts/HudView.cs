using LedgeRPG.Core.World;
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
            rt.sizeDelta = new Vector2(520, 220);

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

        public void Refresh(World world)
        {
            string status = world.Done
                ? $"TERMINAL: {world.TerminalReason} (success={world.Success}) — press R to reseed"
                : "Playing";
            _text.text =
                $"Seed: {world.Seed}\n" +
                $"Step: {world.Step}/{world.StepLimit}\n" +
                $"Energy: {world.Energy:F3}\n" +
                $"Agent: ({world.AgentPos.Q}, {world.AgentPos.R})\n" +
                $"Food: {world.FoodRemaining}/{world.FoodCount} remaining\n" +
                $"Visited: {world.VisitedCount}/{world.TotalPassable}\n" +
                $"\n" +
                $"Controls: Q/W/E/A/S/D move • X examine • Space rest\n" +
                $"\n" +
                $"Status: {status}";
        }
    }
}
