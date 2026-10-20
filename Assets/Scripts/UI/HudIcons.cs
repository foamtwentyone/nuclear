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
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = $"HUD {icon}",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.LoadRawTextureData(rgba);
            texture.Apply(true, true);
            Cache[icon] = texture;
            return texture;
        }
    }
}
