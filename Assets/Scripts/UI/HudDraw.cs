using System.Text.RegularExpressions;
using UnityEngine;

namespace Nuclear.UI
{
    /// <summary>
    /// Минималистичное рисование HUD: тонкие полоски, иконки и текст с тёмной обводкой (как контуры в мире),
    /// без подложек. Размеры — «пиксели при 1080p», умножаются на Scale.
    /// </summary>
    public static class HudDraw
    {
        public static readonly Color Ink = new(0.07f, 0.06f, 0.06f, 0.9f);
        public static readonly Color Cream = new(0.96f, 0.93f, 0.84f);
        public static readonly Color Dim = new(0.96f, 0.93f, 0.84f, 0.55f);

        public enum Face
        {
            /// <summary>Russo One: цифры и заголовки.</summary>
            Heading,
            /// <summary>Rubik: подсказки и пояснения.</summary>
            Body,
        }

        private static readonly Regex ColorTag = new("<color=[^>]*>");
        private static GUIStyle text;
        private static Font headingFont, bodyFont;
        private static bool fontsLoaded;

        public static float Scale { get; private set; } = 1f;

        public static void Begin()
        {
            Scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2.5f);
            if (!fontsLoaded)
            {
                fontsLoaded = true;
                headingFont = Resources.Load<Font>("Fonts/RussoOne-Regular");
                bodyFont = Resources.Load<Font>("Fonts/Rubik");
            }
            text ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, clipping = TextClipping.Overflow };
        }

        public static Font FontOf(Face face) => face == Face.Heading ? headingFont : bodyFont;

        public static float S(float value) => value * Scale;

        public static void Fill(Rect rect, Color color, float radius = 0f) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);

        public static void Frame(Rect rect, Color color, float width, float radius) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);

        public static void Circle(Vector2 center, float radius, Color color) =>
            Fill(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), color, radius);

        public static void Icon(Rect rect, HudIcon icon, Color tint) =>
            GUI.DrawTexture(rect, HudIcons.Get(icon), ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);

        /// <summary>
        /// Тонкая полоска: полупрозрачная основа, заливка, тонкая тёмная обводка.
        /// lost — доля справа, отнятая навсегда (радиация); trail — светлый «хвост» недавнего урона.
        /// </summary>
        public static void Bar(Rect rect, float fill, Color color, float lost = 0f, float trail = -1f, float alpha = 1f)
        {
            float radius = rect.height / 2f;
            Fill(HudInset(rect, -S(1.5f)), new Color(Ink.r, Ink.g, Ink.b, 0.75f * alpha), radius + S(1.5f));
            Fill(rect, new Color(1f, 1f, 1f, 0.12f * alpha), radius);
            if (lost > 0.001f)
            {
                var lostRect = new Rect(rect.xMax - rect.width * lost, rect.y, rect.width * lost, rect.height);
                Fill(lostRect, new Color(0.55f, 0.6f, 0.25f, 0.45f * alpha), Mathf.Min(radius, lostRect.width / 2f));
            }
            if (trail > fill)
                Part(rect, trail, new Color(1f, 0.95f, 0.85f, 0.75f * alpha));
            Part(rect, fill, new Color(color.r, color.g, color.b, color.a * alpha));
        }

        private static void Part(Rect rect, float share, Color color)
        {
            float width = rect.width * Mathf.Clamp01(share);
            if (width < 1f)
                return;
            Fill(new Rect(rect.x, rect.y, width, rect.height), color, Mathf.Min(rect.height / 2f, width / 2f));
        }

        /// <summary>
        /// Строка подсказок «[клавиша] действие» по центру; пустая клавиша — просто текст.
        /// Возвращает ширину строки.
        /// </summary>
        public static float KeyHints(Vector2 center, float fontSize, Color color, params (string key, string action)[] parts)
        {
            float gap = S(7f), spacing = S(22f);
            float keySize = fontSize * 0.75f;
            float total = 0f;
            var widths = new float[parts.Length * 2];
            for (int i = 0; i < parts.Length; i++)
            {
                var (key, action) = parts[i];
                widths[i * 2] = string.IsNullOrEmpty(key) ? 0f : Mathf.Max(S(22f), Measure(key, keySize, Face.Heading) + S(10f));
                widths[i * 2 + 1] = Measure(action, fontSize, Face.Body);
                total += widths[i * 2] + (widths[i * 2] > 0f ? gap : 0f) + widths[i * 2 + 1] + (i > 0 ? spacing : 0f);
            }

            float x = center.x - total / 2f;
            float height = S(fontSize * 1.6f);
            for (int i = 0; i < parts.Length; i++)
            {
                var (key, action) = parts[i];
                if (i > 0)
                    x += spacing;
                if (widths[i * 2] > 0f)
                {
                    var box = new Rect(x, center.y - S(11f), widths[i * 2], S(22f));
                    Fill(box, new Color(0f, 0f, 0f, 0.45f * color.a), S(4f));
                    Frame(box, new Color(color.r, color.g, color.b, 0.85f * color.a), S(1.5f), S(4f));
                    Text(box, key, keySize, color, TextAnchor.MiddleCenter, Face.Heading, outline: false);
                    x += widths[i * 2] + gap;
                }
                Text(new Rect(x, center.y - height / 2f, widths[i * 2 + 1] + S(4f), height), action, fontSize, color, TextAnchor.MiddleLeft, Face.Body);
                x += widths[i * 2 + 1];
            }
            return total;
        }

        public static float Measure(string value, float fontSize, Face face)
        {
            text.font = FontOf(face);
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(S(fontSize)));
            text.fontStyle = text.font == null ? FontStyle.Bold : FontStyle.Normal;
            return text.CalcSize(new GUIContent(value)).x;
        }

        /// <summary>Текст; outline — тонкая тёмная обводка для читаемости на любом фоне.</summary>
        public static void Text(Rect rect, string value, float fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
            Face face = Face.Heading, bool outline = true)
        {
            text.font = FontOf(face);
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(S(fontSize)));
            text.fontStyle = text.font == null ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = anchor;

            if (outline)
            {
                string dark = ColorTag.Replace(value, "<color=#121010cc>");
                text.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, 0.8f * color.a);
                float o = Mathf.Max(1f, S(1.5f));
                foreach (var offset in new[] { new Vector2(-o, 0f), new Vector2(o, 0f), new Vector2(0f, -o), new Vector2(0f, o) })
                    GUI.Label(new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height), dark, text);
            }
            text.normal.textColor = color;
            GUI.Label(rect, value, text);
        }

        public static Rect HudInset(Rect rect, float amount) =>
            new(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);

        public static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);

        /// <summary>Мигание для тревожных состояний: 0..1.</summary>
        public static float Blink(float speed = 6f) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
    }
}
