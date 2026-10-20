using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nuclear.UI
{
    /// <summary>
    /// Текстуры HUD: иконки (HudIconShapes) и фактуры (HudArt). Рисуются кодом один раз при первом обращении.
    /// </summary>
    public static class HudIcons
    {
        private const int IconSize = 128;
        private static readonly Dictionary<HudIcon, Texture2D> Icons = new();
        private static readonly Dictionary<HudArtKind, Texture2D> Art = new();

        public static Texture2D Get(HudIcon icon)
        {
            if (Icons.TryGetValue(icon, out var texture) && texture != null)
                return texture;
            texture = Create($"HUD {icon}", IconSize, TextureWrapMode.Clamp, rgba => HudIconShapes.Rasterize(icon, IconSize, rgba));
            Icons[icon] = texture;
            return texture;
        }

        public static Texture2D Get(HudArtKind kind)
        {
            if (Art.TryGetValue(kind, out var texture) && texture != null)
                return texture;
            int size = kind == HudArtKind.Dial ? 256 : 64;
            var wrap = kind == HudArtKind.Dial ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            texture = Create($"HUD {kind}", size, wrap, rgba => HudArt.Rasterize(kind, size, rgba));
            Art[kind] = texture;
            return texture;
        }

        private static Texture2D Create(string name, int size, TextureWrapMode wrap, Action<byte[]> rasterize)
        {
            var rgba = new byte[size * size * 4];
            rasterize(rgba);
            // SetPixels32 + Apply(true): мип-уровни Unity построит сама (LoadRawTextureData ждёт их все сразу).
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                wrapMode = wrap,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
