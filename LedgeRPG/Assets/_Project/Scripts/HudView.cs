using LedgeRPG.Core.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Magi.LedgeRPG
{
    public sealed class HudView : MonoBehaviour
    {
        private TMP_Text _text;

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

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            // Outline keeps glyph edges readable over the hex grid; underlay
            // replaces the old duplicate-GameObject drop shadow with a single
            // shader pass. Both ride on the default LiberationSans SDF
            // material that TMP Essentials ships.
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.outlineWidth = 0.2f;

            var mat = tmp.fontMaterial;
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.8f));
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 1f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1f);

            _text = tmp;
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
