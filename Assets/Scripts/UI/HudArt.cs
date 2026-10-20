using System;

namespace Nuclear.UI
{
    public enum HudArtKind
    {
        /// <summary>Циферблат манометра фильтра: дуга 240° со штрихами и красной зоной.</summary>
        Dial,
        /// <summary>Полосы «опасность» под 45°, бесшовная плитка.</summary>
        Hazard,
        /// <summary>Потёртости и пятна для металла, бесшовная плитка (только альфа).</summary>
        Grain,
        /// <summary>Волокна малярного скотча, бесшовная плитка (только альфа).</summary>
        Paper,
    }

    /// <summary>
    /// Растровые «фактуры» HUD без Unity (проверяются вне редактора): rgba, строки снизу вверх.
    /// Цвета уже готовые (не для подкраски), кроме Grain и Paper — у них белый цвет и альфа.
    /// </summary>
    public static class HudArt
    {
        public const float DialSweep = 240f;

        public static void Rasterize(HudArtKind kind, int size, byte[] rgba)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    var (r, g, b, a) = kind switch
                    {
                        HudArtKind.Dial => Dial(u * 2f - 1f, v * 2f - 1f, 2f / size),
                        HudArtKind.Hazard => Hazard(u, v, size),
                        HudArtKind.Grain => (1f, 1f, 1f, Grain(x, y, size)),
                        HudArtKind.Paper => (1f, 1f, 1f, Paper(x, y, size)),
                        _ => (0f, 0f, 0f, 0f),
                    };
                    int i = (y * size + x) * 4;
                    rgba[i] = ToByte(r);
                    rgba[i + 1] = ToByte(g);
                    rgba[i + 2] = ToByte(b);
                    rgba[i + 3] = ToByte(a);
                }
            }
        }

        private static (float, float, float, float) Dial(float x, float y, float pixel)
        {
            float r = MathF.Sqrt(x * x + y * y);
            float outline = Coverage(r - 0.98f, pixel);
            if (outline <= 0f)
                return (0f, 0f, 0f, 0f);

            // Ободок — тёмная сталь с бликом сверху, внутри — старая эмаль.
            (float, float, float) color;
            if (r > 0.93f)
                color = (0.07f, 0.06f, 0.06f);
            else if (r > 0.8f)
                color = Lerp3((0.22f, 0.23f, 0.2f), (0.5f, 0.5f, 0.45f), Clamp01(y * 0.5f + 0.5f));
            else if (r > 0.765f)
                color = (0.07f, 0.06f, 0.06f);
            else
            {
                float stain = 0.04f * Noise(x * 6f, y * 6f);
                color = (0.9f - stain, 0.86f - stain, 0.74f - stain * 1.5f);

                // Угол от «верха» по часовой стрелке, градусы; шкала от −120° до +120°.
                float angle = MathF.Atan2(x, y) * 180f / MathF.PI;
                float half = DialSweep / 2f;
                if (MathF.Abs(angle) <= half + 1f)
                {
                    float t = (angle + half) / DialSweep;
                    // Красная зона — последние 15 %: фильтр почти кончился.
                    if (t < 0.15f && r > 0.56f && r < 0.72f)
                        color = (0.78f, 0.2f, 0.16f);

                    float majorStep = DialSweep / 8f, minorStep = DialSweep / 32f;
                    float major = MathF.Abs(Mod(angle + half + majorStep / 2f, majorStep) - majorStep / 2f) * MathF.PI / 180f * r;
                    float minor = MathF.Abs(Mod(angle + half + minorStep / 2f, minorStep) - minorStep / 2f) * MathF.PI / 180f * r;
                    float tick = 0f;
                    if (r > 0.52f && r < 0.74f)
                        tick = MathF.Max(tick, Coverage(major - 0.028f, pixel));
                    if (r > 0.62f && r < 0.74f)
                        tick = MathF.Max(tick, Coverage(minor - 0.012f, pixel));
                    color = Lerp3(color, (0.1f, 0.09f, 0.08f), tick);
                }
            }
            return (color.Item1, color.Item2, color.Item3, outline);
        }

        private static (float, float, float, float) Hazard(float u, float v, int size)
        {
            // Две полосы на плитку по диагонали — плитка бесшовная.
            float stripe = Mod(u + v, 0.5f) / 0.5f;
            bool yellow = stripe < 0.5f;
            float wear = Noise(u * 9f, v * 9f) * 0.12f;
            return yellow ? (0.86f - wear, 0.68f - wear, 0.16f, 1f) : (0.1f + wear * 0.5f, 0.09f + wear * 0.5f, 0.08f, 1f);
        }

        private static float Grain(int x, int y, int size)
        {
            // Мелкие царапины и пятна: случайные точки плюс мягкий шум.
            float speck = Hash(x, y) > 0.93f ? 0.5f : 0f;
            float blotch = Clamp01(TileNoise(x, y, size, 8) * 1.6f - 0.7f) * 0.6f;
            return Clamp01(speck + blotch);
        }

        private static float Paper(int x, int y, int size)
        {
            // Волокна вдоль ленты: шум, растянутый по горизонтали.
            float fiber = TileNoise(x / 4, y, size / 4, 16) * 0.5f + Hash(x / 3, y) * 0.3f;
            return Clamp01(fiber - 0.35f);
        }

        // ---------- Шум ----------

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        /// <summary>Сглаженный шум ячейками cells, бесшовный на плитке size.</summary>
        private static float TileNoise(int x, int y, int size, int cells)
        {
            float fx = x * (float)cells / Math.Max(1, size), fy = y * (float)cells / Math.Max(1, size);
            int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
            float tx = Smooth(fx - ix), ty = Smooth(fy - iy);
            float Corner(int cx, int cy) => Hash(((cx % cells) + cells) % cells, ((cy % cells) + cells) % cells);
            float a = Corner(ix, iy), b = Corner(ix + 1, iy), c = Corner(ix, iy + 1), d = Corner(ix + 1, iy + 1);
            return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
        }

        private static float Noise(float x, float y)
        {
            int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
            float tx = Smooth(x - ix), ty = Smooth(y - iy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float Coverage(float distance, float pixel) => Clamp01(0.5f - distance / pixel);
        private static (float, float, float) Lerp3((float, float, float) a, (float, float, float) b, float t) =>
            (a.Item1 + (b.Item1 - a.Item1) * t, a.Item2 + (b.Item2 - a.Item2) * t, a.Item3 + (b.Item3 - a.Item3) * t);
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Mod(float a, float m) => a - m * MathF.Floor(a / m);
        private static byte ToByte(float v) => (byte)(Clamp01(v) * 255f + 0.5f);
    }
}
