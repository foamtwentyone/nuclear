using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Nuclear.UI
{
    /// <summary>
    /// HUD в духе Peak, но под наш сеттинг — «тетрадь выжившего» и памятка гражданской обороны:
    /// обрывки бумаги и кружки, обведённые маркером от руки (HudSketch), рукописный шрифт Neucha,
    /// красные резиновые штампы шрифтом Ruslan Display. Светлый текст на экране — с толстой тёмной обводкой.
    /// Размеры — «пиксели при 1080p», умножаются на Scale.
    /// </summary>
    public static class HudDraw
    {
        public static readonly Color Ink = new(0.1f, 0.08f, 0.07f, 0.95f);
        public static readonly Color Paper = new(0.94f, 0.9f, 0.79f, 0.93f);
        public static readonly Color Cream = new(0.98f, 0.95f, 0.87f);
        public static readonly Color Dim = new(0.98f, 0.95f, 0.87f, 0.7f);
        public static readonly Color StampRed = new(0.78f, 0.13f, 0.1f, 0.9f);

        public enum Face
        {
            /// <summary>Neucha: рукописные цифры и подсказки.</summary>
            Hand,
            /// <summary>Ruslan Display: штампы и крупные надписи.</summary>
            Stamp,
        }

        private static readonly Regex ColorTag = new("<color=[^>]*>");
        private static readonly Vector2[] OutlineOffsets =
        {
            new(-1f, 0f), new(1f, 0f), new(0f, -1f), new(0f, 1f),
            new(-0.7f, -0.7f), new(0.7f, -0.7f), new(-0.7f, 0.7f), new(0.7f, 0.7f),
        };

        private static GUIStyle text;
        private static Font handFont, stampFont;
        private static bool fontsLoaded;

        public static float Scale { get; private set; } = 1f;

        public static void Begin()
        {
            Scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2.5f);
            if (!fontsLoaded)
            {
                fontsLoaded = true;
                handFont = Resources.Load<Font>("Fonts/Neucha");
                stampFont = Resources.Load<Font>("Fonts/RuslanDisplay");
            }
            text ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, clipping = TextClipping.Overflow };
        }

        public static Font FontOf(Face face) => face == Face.Hand ? handFont : stampFont;

        public static float S(float value) => value * Scale;

        public static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);

        /// <summary>Мигание для тревожных состояний: 0..1.</summary>
        public static float Blink(float speed = 6f) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);

        public static void Icon(Rect rect, HudIcon icon, Color tint) =>
            GUI.DrawTexture(rect, HudIcons.Get(icon), ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);

        // ---------- Бумага и маркер ----------

        /// <summary>Обрывок бумаги: неровная заливка и обводка маркером.</summary>
        public static List<Vector2> PaperShape(Rect rect, float radius, int seed, Color? fill = null, float outline = 3f, float alpha = 1f, float rotation = 0f)
        {
            var shape = HudSketch.RoundedRect(rect, S(radius), seed, S(1.6f), rotation);
            HudSketch.Fill(shape, WithAlpha(fill ?? Paper, alpha));
            HudSketch.Stroke(shape, S(outline), WithAlpha(Ink, alpha), true, seed);
            return shape;
        }

        /// <summary>Кружок-значок: бумага, обводка маркером и иконка внутри.</summary>
        public static void Badge(Vector2 center, float radius, HudIcon icon, Color iconTint, int seed, float alpha = 1f, Color? fill = null, float outline = 3f)
        {
            var circle = HudSketch.Circle(center, S(radius), seed, S(1.3f));
            HudSketch.Fill(circle, WithAlpha(fill ?? Paper, alpha));
            HudSketch.Stroke(circle, S(outline), WithAlpha(Ink, alpha), true, seed);
            float size = S(radius) * 1.3f;
            Icon(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), icon, WithAlpha(iconTint, alpha));
        }

        /// <summary>
        /// Полоска на бумажной подложке: заливка нарисована маркером внутри обрывка.
        /// lost — доля справа, отнятая навсегда (заштрихована); trail — светлый «хвост» недавнего урона;
        /// threat — доля перед концом заливки, которая вот-вот уйдёт (пульсирует).
        /// </summary>
        public static void Bar(Rect rect, float fill, Color color, int seed, float lost = 0f, float trail = -1f, float threat = 0f,
            Color? threatColor = null, Color? lostColor = null, float alpha = 1f)
        {
            var paper = HudSketch.RoundedRect(rect, rect.height / 2f, seed, S(1.4f));
            HudSketch.Fill(paper, WithAlpha(Paper, alpha));

            var inner = Inset(rect, Mathf.Max(S(3.5f), rect.height * 0.18f));
            float radius = inner.height / 2f;
            if (lost > 0.001f)
            {
                var lostRect = new Rect(inner.xMax - inner.width * lost, inner.y, inner.width * lost, inner.height);
                var hatchColor = lostColor ?? new Color(0.35f, 0.5f, 0.15f);
                HudSketch.Fill(HudSketch.RoundedRect(lostRect, radius, seed + 3, S(0.8f)), WithAlpha(hatchColor, 0.3f * alpha));
                HudSketch.Hatch(Inset(lostRect, S(1f)), S(7f), S(2f), WithAlpha(hatchColor, 0.85f * alpha), seed);
            }
            if (trail > fill)
                Part(inner, trail, new Color(1f, 0.97f, 0.9f, 0.95f * alpha), seed + 5, radius);
            Part(inner, fill, WithAlpha(color, alpha), seed + 7, radius);
            if (threat > 0.001f && fill > 0.001f)
            {
                float start = Mathf.Max(0f, fill - threat);
                var threatRect = new Rect(inner.x + inner.width * start, inner.y, inner.width * (fill - start), inner.height);
                var pulse = threatColor ?? new Color(0.55f, 0.3f, 0.75f);
                HudSketch.Fill(HudSketch.RoundedRect(threatRect, Mathf.Min(radius, threatRect.width / 2f), seed + 9, S(0.8f)),
                    WithAlpha(pulse, (0.5f + 0.5f * Blink(7f)) * alpha));
            }

            HudSketch.Stroke(paper, S(3f), WithAlpha(Ink, alpha), true, seed);
        }

        private static void Part(Rect inner, float share, Color color, int seed, float radius)
        {
            float width = inner.width * Mathf.Clamp01(share);
            if (width < 1f)
                return;
            var rect = new Rect(inner.x, inner.y, width, inner.height);
            HudSketch.Fill(HudSketch.RoundedRect(rect, Mathf.Min(radius, width / 2f), seed, S(0.8f)), color);
        }

        /// <summary>Линия маркером с тёмной подводкой (выносливость, заряд).</summary>
        public static void InkLine(Vector2 from, float length, float width, Color color, int seed, float alpha = 1f)
        {
            if (length < 1f)
                return;
            var line = HudSketch.Line(from, from + new Vector2(length, 0f), seed, S(1f));
            HudSketch.Stroke(line, width + S(3.5f), WithAlpha(Ink, 0.85f * alpha), false, seed);
            HudSketch.Stroke(line, width, WithAlpha(color, alpha), false, seed);
        }

        /// <summary>Красный резиновый штамп: надпись в двойной неровной рамке, слегка наискось.</summary>
        public static void Stamp(Vector2 center, string value, float fontSize, Color color, float rotation, int seed)
        {
            float width = Measure(value, fontSize, Face.Stamp) + S(26f);
            float height = S(fontSize * 1.9f);
            var rect = new Rect(center.x - width / 2f, center.y - height / 2f, width, height);
            HudSketch.Stroke(HudSketch.RoundedRect(rect, S(6f), seed, S(1.8f), rotation), S(3.5f), color, true, seed);
            HudSketch.Stroke(HudSketch.RoundedRect(Inset(rect, S(5f)), S(4f), seed + 1, S(1.4f), rotation), S(1.8f), color, true, seed + 1);

            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotation, center);
            Text(rect, value, fontSize, color, TextAnchor.MiddleCenter, Face.Stamp, outline: false);
            GUI.matrix = matrix;
        }

        /// <summary>Клавиша: обрывок бумаги с буквами, обведённый маркером. Возвращает ширину.</summary>
        public static float KeyCap(Vector2 leftCenter, string key, float fontSize, int seed, float alpha = 1f)
        {
            float width = Mathf.Max(S(fontSize * 1.35f), Measure(key, fontSize, Face.Hand) + S(14f));
            float height = S(fontSize * 1.35f);
            var rect = new Rect(leftCenter.x, leftCenter.y - height / 2f, width, height);
            PaperShape(rect, height * 0.35f / Scale, seed, outline: 2.5f, alpha: alpha);
            Text(new Rect(rect.x, rect.y - S(1f), rect.width, rect.height), key, fontSize, WithAlpha(Ink, alpha), TextAnchor.MiddleCenter, outline: false);
            return width;
        }

        /// <summary>Строка подсказок «[клавиша] действие» по центру; пустая клавиша — просто текст.</summary>
        public static void KeyHints(Vector2 center, float fontSize, Color color, params (string key, string action)[] parts)
        {
            float gap = S(8f), spacing = S(26f);
            float keySize = fontSize * 0.85f;
            float total = 0f;
            var widths = new float[parts.Length * 2];
            for (int i = 0; i < parts.Length; i++)
            {
                var (key, action) = parts[i];
                widths[i * 2] = string.IsNullOrEmpty(key) ? 0f : Mathf.Max(S(keySize * 1.35f), Measure(key, keySize, Face.Hand) + S(14f));
                widths[i * 2 + 1] = Measure(action, fontSize, Face.Hand);
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
                    KeyCap(new Vector2(x, center.y), key, keySize, 50 + i * 7, color.a);
                    x += widths[i * 2] + gap;
                }
                Text(new Rect(x, center.y - height / 2f, widths[i * 2 + 1] + S(6f), height), action, fontSize, color);
                x += widths[i * 2 + 1];
            }
        }

        // ---------- Текст ----------

        public static float Measure(string value, float fontSize, Face face)
        {
            Setup(fontSize, face);
            return text.CalcSize(new GUIContent(value)).x;
        }

        /// <summary>Текст; outline — толстая тёмная обводка, чтобы светлые буквы читались на любом фоне.</summary>
        public static void Text(Rect rect, string value, float fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
            Face face = Face.Hand, bool outline = true)
        {
            Setup(fontSize, face);
            text.alignment = anchor;
            if (outline)
            {
                string dark = ColorTag.Replace(value, "<color=#1a1412e6>");
                text.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, 0.9f * color.a);
                float o = Mathf.Max(1.5f, S(2.6f));
                foreach (var offset in OutlineOffsets)
                    GUI.Label(new Rect(rect.x + offset.x * o, rect.y + offset.y * o, rect.width, rect.height), dark, text);
            }
            text.normal.textColor = color;
            GUI.Label(rect, value, text);
        }

        private static void Setup(float fontSize, Face face)
        {
            text.font = FontOf(face);
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(S(fontSize)));
            text.fontStyle = text.font == null ? FontStyle.Bold : FontStyle.Normal;
        }

        public static Rect Inset(Rect rect, float amount) =>
            new(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
    }
}
