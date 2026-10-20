using UnityEngine;

namespace Nuclear.UI
{
    /// <summary>
    /// Оформление меню (подключение, микрофон) в стиле HUD: оливковые пластины, стальные кнопки
    /// с жёлтой рамкой при наведении, тёмные поля ввода, жёлтая ручка ползунка, шрифты Rubik и Russo One.
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
            var steel = new Color(0.22f, 0.23f, 0.21f);

            Style(skin.box, Rounded(HudDraw.Olive, ink, 3f, 10f), cream, 14);
            skin.box.padding = new RectOffset(12, 12, 10, 10);

            Style(skin.button, Rounded(steel, ink, 3f, 8f), cream, 15);
            skin.button.hover.background = Rounded(new Color(0.28f, 0.29f, 0.26f), HudDraw.HazardYellow, 3f, 8f);
            skin.button.hover.textColor = HudDraw.HazardYellow;
            skin.button.active.background = Rounded(new Color(0.14f, 0.14f, 0.13f), HudDraw.HazardYellow, 3f, 8f);
            skin.button.active.textColor = HudDraw.HazardYellow;
            skin.button.focused = skin.button.normal;
            skin.button.onNormal = skin.button.normal;
            skin.button.onHover = skin.button.hover;
            skin.button.onActive = skin.button.active;
            if (heading != null)
                skin.button.font = heading;
            skin.button.fixedHeight = 32f;
            skin.button.margin = new RectOffset(4, 4, 4, 4);

            Style(skin.textField, Rounded(HudDraw.LcdBack, ink, 3f, 6f), HudDraw.LcdText, 15);
            skin.textField.hover = skin.textField.normal;
            skin.textField.focused.background = Rounded(HudDraw.LcdBack, HudDraw.HazardYellow, 3f, 6f);
            skin.textField.focused.textColor = HudDraw.LcdText;
            skin.textField.padding = new RectOffset(8, 8, 6, 6);
            skin.textField.fixedHeight = 30f;

            skin.label.normal.textColor = cream;
            skin.label.fontSize = 14;

            // Галочка: пустой тёмный квадрат и квадрат с жёлтой «лампочкой».
            var off = Rounded(new Color(0.1f, 0.1f, 0.09f), ink, 3f, 5f, 24);
            var on = Rounded(HudDraw.HazardYellow, ink, 3f, 5f, 24, inner: new Color(0.1f, 0.1f, 0.09f));
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

            skin.horizontalSlider.normal.background = Rounded(new Color(0.06f, 0.06f, 0.06f), ink, 2f, 4f);
            skin.horizontalSlider.border = new RectOffset(6, 6, 4, 4);
            skin.horizontalSlider.fixedHeight = 12f;
            skin.horizontalSlider.margin = new RectOffset(6, 6, 8, 8);
            var knob = Rounded(HudDraw.HazardYellow, ink, 3f, 9f, 24);
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
        private static Texture2D Rounded(Color fill, Color border, float borderWidth, float radius, int size = 32, Color? inner = null)
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
                    // Внутренний квадратик (для включённой галочки).
                    if (inner.HasValue && Mathf.Abs(x + 0.5f - half) < size * 0.2f && Mathf.Abs(y + 0.5f - half) < size * 0.2f)
                        color = Color.Lerp(fill, inner.Value, 0.15f);
                    // Лёгкая фаска: верх светлее.
                    if (edge > 0.5f && y > size - borderWidth - 3f)
                        color = Color.Lerp(color, Color.white, 0.08f);
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
