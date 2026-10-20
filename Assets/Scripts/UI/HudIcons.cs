using System.Collections.Generic;
using UnityEngine;

namespace Nuclear.UI
{
    /// <summary>Текстуры иконок HUD, рисуются из HudIconShapes один раз при первом обращении.</summary>
    public static class HudIcons
    {
        private const int Size = 128;
        private static readonly Dictionary<HudIcon, Texture2D> Cache = new();

        public static Texture2D Get(HudIcon icon)
        {
            if (Cache.TryGetValue(icon, out var texture) && texture != null)
                return texture;

            var rgba = new byte[Size * Size * 4];
            HudIconShapes.Rasterize(icon, Size, rgba);
            // SetPixels32 + Apply(true): мип-уровни Unity построит сама (LoadRawTextureData ждёт их все сразу).
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);

            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = $"HUD {icon}",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            Cache[icon] = texture;
            return texture;
        }
    }
}
