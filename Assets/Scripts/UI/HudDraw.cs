using System.Text.RegularExpressions;
using UnityEngine;

namespace Nuclear.UI
{
    /// <summary>
    /// Рисование HUD в стиле игры: армейское снаряжение после катастрофы. Оливковый и стальной металл
    /// с потёртостями и заклёпками, толстая тёмная обводка (как контуры в мире), жёлтые трафаретные бирки,
    /// полосы «опасность», подсказки на малярном скотче, манометр и ЖК-экран дозиметра.
    /// Размеры — «пиксели при 1080p», умножаются на Scale.
    /// </summary>
    public static class HudDraw
    {
        public static readonly Color Ink = new(0.07f, 0.06f, 0.06f, 0.96f);
        public static readonly Color Cream = new(0.96f, 0.92f, 0.8f);
        public static readonly Color Olive = new(0.29f, 0.31f, 0.22f, 0.94f);
        public static readonly Color Steel = new(0.2f, 0.21f, 0.2f, 0.92f);
        public static readonly Color HazardYellow = new(0.88f, 0.69f, 0.17f);
        public static readonly Color TapeColor = new(0.88f, 0.83f, 0.7f, 0.97f);
        public static readonly Color LcdBack = new(0.07f, 0.12f, 0.07f, 0.97f);
        public static readonly Color LcdText = new(0.58f, 0.96f, 0.42f);

        public enum Face
        {
            /// <summary>Russo One: цифры, заголовки, бирки.</summary>
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

        // ---------- Простые фигуры ----------

        public static void Fill(Rect rect, Color color, float radius = 0f) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);

        public static void Frame(Rect rect, Color color, float width, float radius) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);

        public static void Circle(Vector2 center, float radius, Color color) =>
            Fill(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), color, radius);

        /// <summary>Плитка текстуры на весь прямоугольник; tile — размер плитки в пикселях экрана.</summary>
        public static void Tiled(Rect rect, Texture texture, float tile, Color color, Vector2 offset = default)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(rect, texture,
                new Rect(offset.x + rect.x / tile, offset.y - rect.y / tile, rect.width / tile, rect.height / tile), true);
            GUI.color = previous;
        }

        public static void Icon(Rect rect, HudIcon icon, Color tint) =>
            GUI.DrawTexture(rect, HudIcons.Get(icon), ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);

        // ---------- Детали снаряжения ----------

        /// <summary>
        /// Металлическая пластина: тень, основа, потёртости, светлая фаска по краю и толстая обводка.
        /// </summary>
        public static void Plate(Rect rect, float radius, Color color, Color? border = null, float borderWidth = 3f)
        {
            float r = S(radius);
            Fill(new Rect(rect.x + S(4f), rect.y + S(5f), rect.width, rect.height), new Color(0f, 0f, 0f, 0.35f), r);
            Fill(rect, color, r);
            Tiled(Inset(rect, r * 0.7f), HudIcons.Get(HudArtKind.Grain), S(96f), new Color(0f, 0f, 0f, 0.28f), new Vector2(rect.width * 0.013f, 0f));
            Frame(Inset(rect, S(3f)), new Color(1f, 1f, 1f, 0.07f), S(2f), Mathf.Max(0f, r - S(3f)));
            Frame(rect, border ?? Ink, S(borderWidth), r);
        }

        /// <summary>Заклёпка: тёмный кружок с бликом.</summary>
        public static void Rivet(Vector2 center, float radius = 4.5f)
        {
            float r = S(radius);
            Circle(center, r, Ink);
            Circle(center, r * 0.7f, new Color(0.55f, 0.55f, 0.5f));
            Circle(center + new Vector2(-r * 0.2f, -r * 0.25f), r * 0.3f, new Color(0.85f, 0.85f, 0.8f));
        }

        public static void Rivets(Rect rect, float margin = 12f, float radius = 4.5f)
        {
            float m = S(margin);
            Rivet(new Vector2(rect.x + m, rect.y + m), radius);
            Rivet(new Vector2(rect.xMax - m, rect.y + m), radius);
            Rivet(new Vector2(rect.x + m, rect.yMax - m), radius);
            Rivet(new Vector2(rect.xMax - m, rect.yMax - m), radius);
        }

        /// <summary>Полоса «опасность» в рамке.</summary>
        public static void HazardStrip(Rect rect)
        {
            float r = Mathf.Min(rect.height / 2f, S(4f));
            Fill(rect, Ink, r);
            Tiled(Inset(rect, S(1.5f)), HudIcons.Get(HudArtKind.Hazard), Mathf.Max(8f, Mathf.Min(rect.width, rect.height) * 1.2f), Color.white);
            Frame(rect, Ink, S(2f), r);
        }

        /// <summary>Жёлтая трафаретная бирка с тёмным текстом.</summary>
        public static void Tag(Rect rect, string value, float fontSize, Color? color = null)
        {
            Fill(new Rect(rect.x + S(2f), rect.y + S(3f), rect.width, rect.height), new Color(0f, 0f, 0f, 0.35f), S(5f));
            Fill(rect, color ?? HazardYellow, S(5f));
            Frame(rect, Ink, S(2.5f), S(5f));
            Text(rect, value, fontSize, Ink, TextAnchor.MiddleCenter, Face.Heading, outline: false);
        }

        /// <summary>Клавиша-кнопка: «E», «ПКМ», «R».</summary>
        public static float KeyCap(Vector2 leftCenter, string key, float fontSize = 15f)
        {
            float width = Mathf.Max(S(28f), Measure(key, fontSize, Face.Heading) + S(14f));
            float height = S(28f);
            var rect = new Rect(leftCenter.x, leftCenter.y - height / 2f, width, height);
            Fill(new Rect(rect.x, rect.y + S(3f), rect.width, rect.height), Ink, S(6f));
            Fill(rect, new Color(0.24f, 0.24f, 0.22f), S(6f));
            Fill(new Rect(rect.x + S(3f), rect.y + S(3f), rect.width - S(6f), rect.height * 0.45f), new Color(1f, 1f, 1f, 0.09f), S(4f));
            Frame(rect, Ink, S(2.5f), S(6f));
            Text(rect, key, fontSize, Cream, TextAnchor.MiddleCenter, Face.Heading, outline: false);
            return width;
        }

        /// <summary>
        /// Полоска малярного скотча (слегка наискось) с подсказками вида «клавиша — действие».
        /// Пустая клавиша — просто текст. Возвращает прямоугольник ленты.
        /// </summary>
        public static Rect Tape(Vector2 center, Color textColor, float fontSize, params (string key, string action)[] parts)
        {
            float gap = S(8f), padding = S(16f);
            float width = padding * 2f - gap * 1.5f;
            foreach (var (key, action) in parts)
            {
                if (!string.IsNullOrEmpty(key))
                    width += Mathf.Max(S(28f), Measure(key, 15f, Face.Heading) + S(14f)) + gap;
                width += Measure(action, fontSize, Face.Body) + gap * 2.5f;
            }
            float height = S(40f);
            var rect = new Rect(center.x - width / 2f, center.y - height / 2f, width, height);

            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-0.8f, center);
            Fill(new Rect(rect.x + S(3f), rect.y + S(4f), rect.width, rect.height), new Color(0f, 0f, 0f, 0.3f), S(2f));
            Fill(rect, TapeColor, S(2f));
            Tiled(rect, HudIcons.Get(HudArtKind.Paper), S(64f), new Color(0.45f, 0.38f, 0.25f, 0.35f));
            // Рваные края: тёмные зубчики на концах ленты.
            for (int i = 0; i < 4; i++)
            {
                float y = rect.y + rect.height * (i + 0.5f) / 4f;
                Fill(new Rect(rect.x - S(2f), y - S(3f), S(4f), S(6f)), new Color(0f, 0f, 0f, 0.25f), S(1f));
                Fill(new Rect(rect.xMax - S(2f), y - S(2f), S(4f), S(5f)), new Color(0f, 0f, 0f, 0.25f), S(1f));
            }

            float x = rect.x + padding;
            foreach (var (key, action) in parts)
            {
                if (!string.IsNullOrEmpty(key))
                    x += KeyCap(new Vector2(x, center.y - S(1f)), key) + gap;
                float actionWidth = Measure(action, fontSize, Face.Body);
                Text(new Rect(x, rect.y, actionWidth + S(4f), rect.height), action, fontSize, textColor, TextAnchor.MiddleLeft, Face.Body, outline: false);
                x += actionWidth + gap * 2.5f;
            }
            GUI.matrix = matrix;
            return rect;
        }

        /// <summary>
        /// Толстая полоска из сегментов: тёмная основа, заливка с бликом, риски между сегментами.
        /// lost — доля справа, отнятая навсегда (радиация); trail — светлый «хвост» недавнего урона.
        /// </summary>
        public static void Bar(Rect rect, float fill, Color color, int segments = 1, float lost = 0f, float trail = -1f)
        {
            float radius = Mathf.Min(rect.height / 2f, S(7f));
            Fill(rect, new Color(0.04f, 0.04f, 0.04f, 0.85f), radius);

            if (lost > 0.001f)
            {
                var lostRect = new Rect(rect.xMax - rect.width * lost, rect.y, rect.width * lost, rect.height);
                Fill(lostRect, new Color(0.32f, 0.36f, 0.12f, 0.95f), Mathf.Min(radius, lostRect.width / 2f));
                Tiled(lostRect, HudIcons.Get(HudArtKind.Hazard), rect.height * 2.2f, new Color(0f, 0f, 0f, 0.35f));
            }

            if (trail > fill)
                Part(rect, trail, new Color(1f, 0.93f, 0.78f, 0.9f), radius);
            Part(rect, fill, color, radius);

            float width = rect.width * Mathf.Clamp01(fill);
            if (width > rect.height * 0.8f)
            {
                var gloss = new Rect(rect.x + S(4f), rect.y + rect.height * 0.14f, width - S(8f), rect.height * 0.24f);
                Fill(gloss, new Color(1f, 1f, 1f, 0.3f), gloss.height / 2f);
                Fill(new Rect(rect.x, rect.yMax - rect.height * 0.28f, width, rect.height * 0.28f), new Color(0f, 0f, 0f, 0.18f), radius);
            }

            for (int i = 1; i < segments; i++)
            {
                float x = rect.x + rect.width * i / segments;
                Fill(new Rect(x - S(1.5f), rect.y + S(2f), S(3f), rect.height - S(4f)), new Color(0.04f, 0.04f, 0.04f, 0.85f));
            }

            Frame(rect, Ink, Mathf.Max(1.5f, S(3f)), radius);
        }

        private static void Part(Rect rect, float share, Color color, float radius)
        {
            float width = rect.width * Mathf.Clamp01(share);
            if (width < 1f)
                return;
            Fill(new Rect(rect.x, rect.y, width, rect.height), color, Mathf.Min(radius, width / 2f));
        }

        /// <summary>Манометр: циферблат и стрелка; value 0..1 — от левого края шкалы до правого.</summary>
        public static void Dial(Vector2 center, float radius, float value, Color needle)
        {
            Fill(new Rect(center.x - radius + S(4f), center.y - radius + S(5f), radius * 2f, radius * 2f), new Color(0f, 0f, 0f, 0.35f), radius);
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), HudIcons.Get(HudArtKind.Dial));

            float angle = -HudArt.DialSweep / 2f + HudArt.DialSweep * Mathf.Clamp01(value);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            float length = radius * 0.7f, width = S(6f);
            Fill(new Rect(center.x - width / 2f - S(1.5f), center.y - length - S(1.5f), width + S(3f), length + S(3f)), Ink, width);
            Fill(new Rect(center.x - width / 2f, center.y - length, width, length), needle, width / 2f);
            GUI.matrix = matrix;

            Circle(center, S(9f), Ink);
            Circle(center, S(6f), new Color(0.5f, 0.5f, 0.46f));
        }

        // ---------- Текст ----------

        public static float Measure(string value, float fontSize, Face face)
        {
            text.font = FontOf(face);
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(S(fontSize)));
            text.fontStyle = FontOf(face) == null ? FontStyle.Bold : FontStyle.Normal;
            return text.CalcSize(new GUIContent(value)).x;
        }

        /// <summary>Текст; outline — тёмная обводка, как у контуров в мире.</summary>
        public static void Text(Rect rect, string value, float fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
            Face face = Face.Heading, bool outline = true)
        {
            text.font = FontOf(face);
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(S(fontSize)));
            text.fontStyle = text.font == null ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = anchor;

            if (outline)
            {
                string dark = ColorTag.Replace(value, "<color=#121010f0>");
                text.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, Ink.a * color.a);
                float o = Mathf.Max(1f, S(2f));
                foreach (var offset in new[] { new Vector2(-o, -o), new Vector2(o, -o), new Vector2(-o, o), new Vector2(o, o), new Vector2(0f, o * 1.5f) })
                    GUI.Label(new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height), dark, text);
            }
            text.normal.textColor = color;
            GUI.Label(rect, value, text);
        }

        public static Rect Inset(Rect rect, float amount) =>
            new(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);

        /// <summary>Мигание для тревожных состояний: 0..1.</summary>
        public static float Blink(float speed = 6f) => 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
    }
}
