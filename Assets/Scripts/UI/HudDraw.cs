using System.Text.RegularExpressions;
using UnityEngine;

namespace Nuclear.UI
{
    /// <summary>
    /// Рисование HUD на IMGUI в мультяшном стиле: скруглённые панели с тёмной обводкой,
    /// толстые полоски с бликом, текст с тенью. Размеры в «пикселях при 1080p», умножаются на Scale.
    /// </summary>
    public static class HudDraw
    {
        public static readonly Color Ink = new(0.07f, 0.06f, 0.06f, 0.95f);
        public static readonly Color PanelColor = new(0.1f, 0.1f, 0.09f, 0.55f);
        public static readonly Color Cream = new(0.96f, 0.92f, 0.8f);

        private static GUIStyle text;
        private static readonly Regex ColorTag = new("<color=[^>]*>");

        /// <summary>Масштаб интерфейса под высоту экрана.</summary>
        public static float Scale { get; private set; } = 1f;

        public static void Begin()
        {
            Scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2.5f);
            text ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false };
        }

        public static float S(float value) => value * Scale;

        public static void Fill(Rect rect, Color color, float radius = 0f) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);

        public static void Frame(Rect rect, Color color, float width, float radius) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);

        /// <summary>Скруглённая полупрозрачная подложка с обводкой.</summary>
        public static void Panel(Rect rect, float radius, Color? border = null, float borderWidth = 2f)
        {
            Fill(rect, PanelColor, S(radius));
            Frame(rect, border ?? Ink, S(borderWidth), S(radius));
        }

        /// <summary>
        /// Толстая полоска: тёмная основа, заливка с бликом сверху и обводка.
        /// lost — доля справа, отнятая навсегда (радиация), рисуется тёмно-оливковой.
        /// trail — «хвост» недавнего урона светлым цветом.
        /// </summary>
        public static void Bar(Rect rect, float fill, Color color, float lost = 0f, float trail = -1f)
        {
            float radius = rect.height / 2f;
            Fill(rect, new Color(0.04f, 0.04f, 0.04f, 0.75f), radius);

            if (lost > 0.001f)
            {
                var lostRect = new Rect(rect.xMax - rect.width * lost, rect.y, rect.width * lost, rect.height);
                Fill(lostRect, new Color(0.3f, 0.33f, 0.12f, 0.9f), Mathf.Min(radius, lostRect.width / 2f));
            }

            if (trail > fill)
                Part(rect, trail, new Color(1f, 0.92f, 0.75f, 0.85f));
            Part(rect, fill, color);

            // Блик в верхней трети — «пластиковая» мультяшная полоска.
            float width = rect.width * Mathf.Clamp01(fill);
            if (width > rect.height)
            {
                var gloss = new Rect(rect.x + rect.height * 0.35f, rect.y + rect.height * 0.16f, width - rect.height * 0.7f, rect.height * 0.22f);
                Fill(gloss, new Color(1f, 1f, 1f, 0.28f), gloss.height / 2f);
            }

            Frame(rect, Ink, Mathf.Max(1.5f, S(2.5f)), radius);
        }

        private static void Part(Rect rect, float share, Color color)
        {
            float width = rect.width * Mathf.Clamp01(share);
            if (width < 1f)
                return;
            Fill(new Rect(rect.x, rect.y, width, rect.height), color, Mathf.Min(rect.height / 2f, width / 2f));
        }

        public static void Icon(Rect rect, HudIcon icon, Color tint) =>
            GUI.DrawTexture(rect, HudIcons.Get(icon), ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);

        /// <summary>Текст с тёмной тенью, размер шрифта — при 1080p.</summary>
        public static void Label(Rect rect, string value, float fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = true)
        {
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(S(fontSize)));
            text.alignment = anchor;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;

            float offset = Mathf.Max(1f, S(2f));
            text.normal.textColor = new Color(0f, 0f, 0f, 0.8f * color.a);
            // Цветные вставки в тени тоже должны быть тёмными.
            string shadow = ColorTag.Replace(value, "<color=#000000cc>");
            GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), shadow, text);
            text.normal.textColor = color;
            GUI.Label(rect, value, text);
        }

        /// <summary>Мигание для тревожных состояний: 0..1.</summary>
        public static float Blink(float speed = 6f) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
    }
}
