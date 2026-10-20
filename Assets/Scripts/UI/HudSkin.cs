using UnityEngine;

namespace Nuclear.UI
{
    /// <summary>
    /// Оформление меню (подключение, микрофон) в духе минималистичного HUD: тёмные полупрозрачные панели,
    /// кнопки со светлой рамкой при наведении, шрифты Rubik и Russo One.
    /// </summary>
    public static class HudSkin
    {
        private static GUISkin skin;

        public static GUISkin Get()
        {
            if (skin != null)
                return skin;

            skin = Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.DontSave;
            var body = Resources.Load<Font>("Fonts/Rubik");
            var heading = Resources.Load<Font>("Fonts/RussoOne-Regular");
            if (body != null)
                skin.font = body;

            var ink = HudDraw.Ink;
            var cream = HudDraw.Cream;
            var panel = new Color(0.06f, 0.06f, 0.06f, 0.72f);
            var steel = new Color(0.16f, 0.16f, 0.15f, 0.9f);
            var accent = new Color(1f, 0.82f, 0.4f);
            var field = new Color(0.03f, 0.03f, 0.03f, 0.85f);
            var faint = new Color(1f, 1f, 1f, 0.15f);

            Style(skin.box, Rounded(panel, faint, 1f, 10f), cream, 14);
            skin.box.padding = new RectOffset(12, 12, 10, 10);

            Style(skin.button, Rounded(steel, faint, 1f, 8f), cream, 15);
            skin.button.hover.background = Rounded(new Color(0.22f, 0.22f, 0.2f, 0.95f), cream, 1.5f, 8f);
            skin.button.hover.textColor = Color.white;
            skin.button.active.background = Rounded(new Color(0.1f, 0.1f, 0.1f, 0.95f), accent, 1.5f, 8f);
            skin.button.active.textColor = accent;
            skin.button.focused = skin.button.normal;
            skin.button.onNormal = skin.button.normal;
            skin.button.onHover = skin.button.hover;
            skin.button.onActive = skin.button.active;
            if (heading != null)
                skin.button.font = heading;
            skin.button.fixedHeight = 32f;
            skin.button.margin = new RectOffset(4, 4, 4, 4);

            Style(skin.textField, Rounded(field, faint, 1f, 6f), cream, 15);
            skin.textField.hover = skin.textField.normal;
            skin.textField.focused.background = Rounded(field, cream, 1.5f, 6f);
            skin.textField.focused.textColor = Color.white;
            skin.textField.padding = new RectOffset(8, 8, 6, 6);
            skin.textField.fixedHeight = 30f;

            skin.label.normal.textColor = cream;
            skin.label.fontSize = 14;

            // Галочка: пустой квадрат и залитый светлый.
            var off = Rounded(field, new Color(1f, 1f, 1f, 0.4f), 1.5f, 5f, 24);
            var on = Rounded(accent, ink, 1.5f, 5f, 24);
            foreach (var state in new[] { skin.toggle.normal, skin.toggle.hover, skin.toggle.active, skin.toggle.focused })
            {
                state.background = off;
                state.textColor = cream;
            }
            foreach (var state in new[] { skin.toggle.onNormal, skin.toggle.onHover, skin.toggle.onActive, skin.toggle.onFocused })
            {
                state.background = on;
                state.textColor = cream;
            }
            skin.toggle.border = new RectOffset(8, 8, 8, 8);
            skin.toggle.padding = new RectOffset(30, 0, 4, 4);
            skin.toggle.overflow = new RectOffset(0, 0, 0, 0);
            skin.toggle.fontSize = 14;

            skin.horizontalSlider.normal.background = Rounded(field, faint, 1f, 4f);
            skin.horizontalSlider.border = new RectOffset(6, 6, 4, 4);
            skin.horizontalSlider.fixedHeight = 12f;
            skin.horizontalSlider.margin = new RectOffset(6, 6, 8, 8);
            var knob = Rounded(cream, ink, 1.5f, 9f, 24);
            skin.horizontalSliderThumb.normal.background = knob;
            skin.horizontalSliderThumb.hover.background = knob;
            skin.horizontalSliderThumb.active.background = knob;
            skin.horizontalSliderThumb.fixedWidth = 20f;
            skin.horizontalSliderThumb.fixedHeight = 20f;
            skin.horizontalSliderThumb.border = new RectOffset(0, 0, 0, 0);
            skin.horizontalSliderThumb.overflow = new RectOffset(0, 0, 4, 4);

            return skin;
        }

        private static void Style(GUIStyle style, Texture2D background, Color text, int fontSize)
        {
            style.normal.background = background;
            style.normal.textColor = text;
            style.border = new RectOffset(10, 10, 10, 10);
            style.fontSize = fontSize;
        }

        /// <summary>Скруглённый прямоугольник с обводкой для девятичастной растяжки (border = 10).</summary>
        private static Texture2D Rounded(Color fill, Color border, float borderWidth, float radius, int size = 32)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    float edge = Mathf.Clamp01(0.5f - (d + borderWidth));
                    var color = Color.Lerp(border, fill, edge);
                    color.a *= alpha;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
