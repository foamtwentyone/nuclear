using UnityEngine;

namespace Nuclear.EditorTools.LowPoly
{
    /// <summary>Тип поверхности: от него зависят рисунок текстуры, её масштаб в мире и рельеф.</summary>
    public enum Surface
    {
        Ground, Asphalt, Concrete, Metal, Rust, Wood, Bark, Brick, Paint, Fabric, Fur, Skin, Plain,
    }

    /// <summary>
    /// Процедурные текстуры для low-poly моделей: цвет (с узором) и карта нормалей (рельеф).
    /// Все узоры бесшовно тайлятся. Генерация — чистая математика над массивами пикселей,
    /// без API Unity, чтобы её можно было запускать и проверять вне редактора.
    /// </summary>
    public static class Surfaces
    {
        public static Surface Of(Swatch swatch) => swatch switch
        {
            Swatch.AshDark or Swatch.Ash or Swatch.AshLight or Swatch.Dirt or Swatch.DeadGrass or Swatch.Scorched or Swatch.ScorchedLight => Surface.Ground,
            Swatch.Asphalt or Swatch.AsphaltDark => Surface.Asphalt,
            Swatch.Concrete or Swatch.ConcreteDark or Swatch.ConcreteStain => Surface.Concrete,
            Swatch.Metal or Swatch.MetalDark or Swatch.Tin or Swatch.Lead or Swatch.BatteryCase => Surface.Metal,
            Swatch.Rust or Swatch.RustDark => Surface.Rust,
            Swatch.Wood or Swatch.WoodDark or Swatch.WoodPale => Surface.Wood,
            Swatch.Bark or Swatch.BarkDark => Surface.Bark,
            Swatch.Brick => Surface.Brick,
            Swatch.PaintRed or Swatch.PaintBlue or Swatch.PaintGreen or Swatch.PaintBeige or Swatch.RoadLine
                or Swatch.HazardYellow or Swatch.HazardBlack or Swatch.SignalRed or Swatch.FilterOlive or Swatch.FilterOliveDark or Swatch.CanLabel => Surface.Paint,
            Swatch.Jacket or Swatch.JacketDark or Swatch.Trousers or Swatch.Canvas or Swatch.Boots => Surface.Fabric,
            Swatch.Fur or Swatch.FurDark or Swatch.FurPale => Surface.Fur,
            Swatch.Skin or Swatch.SkinDark or Swatch.SkinPale or Swatch.NosePink or Swatch.NoseDark or Swatch.Mouth => Surface.Skin,
            _ => Surface.Plain,
        };

        /// <summary>Сколько метров мира покрывает одна копия текстуры.</summary>
        public static float MetersPerTile(Surface surface) => surface switch
        {
            Surface.Ground => 3f,
            Surface.Asphalt => 2f,
            Surface.Concrete => 2f,
            Surface.Metal => 0.8f,
            Surface.Rust => 1f,
            Surface.Wood => 1f,
            Surface.Bark => 1f,
            Surface.Brick => 1.2f,
            Surface.Paint => 1f,
            Surface.Fabric => 0.35f,
            Surface.Fur => 0.6f,
            Surface.Skin => 0.6f,
            _ => 0.5f,
        };

        public static int Resolution(Surface surface) =>
            surface is Surface.Ground or Surface.Asphalt or Surface.Concrete ? 256 : 128;

        public static float Smoothness(Swatch swatch) => swatch switch
        {
            Swatch.Glass or Swatch.Lens => 0.7f,
            Swatch.Mouth or Swatch.NosePink => 0.45f,
            _ => Of(swatch) switch
            {
                Surface.Metal => 0.4f,
                Surface.Paint => 0.3f,
                Surface.Skin => 0.2f,
                Surface.Plain => 0.2f,
                _ => 0.05f,
            },
        };

        private static float NormalStrength(Surface surface) => surface switch
        {
            Surface.Brick => 3f,
            Surface.Ground or Surface.Rust or Surface.Bark => 2f,
            Surface.Concrete or Surface.Asphalt or Surface.Fur => 1.5f,
            Surface.Wood => 1f,
            Surface.Metal => 0.8f,
            Surface.Plain => 0.5f,
            _ => 1f,
        };

        /// <summary>Цвет «вкраплений»: трещины, ржавчина под краской, лишайник, раствор между кирпичами.</summary>
        private static Color Accent(Surface surface, Color color) => surface switch
        {
            Surface.Ground or Surface.Asphalt or Surface.Wood => color * 0.45f,
            Surface.Concrete => color * 0.62f,
            Surface.Metal => new Color(0.45f, 0.24f, 0.12f),
            Surface.Rust => new Color(0.68f, 0.35f, 0.13f),
            Surface.Bark => new Color(0.55f, 0.58f, 0.48f),
            Surface.Brick => new Color(0.6f, 0.58f, 0.54f),
            Surface.Paint => new Color(0.36f, 0.2f, 0.12f),
            Surface.Skin => new Color(0.62f, 0.35f, 0.35f),
            _ => color,
        };

        /// <summary>Пиксели цветной текстуры: цвет палитры, промодулированный узором поверхности.</summary>
        public static Color[] AlbedoPixels(Swatch swatch)
        {
            var surface = Of(swatch);
            int size = Resolution(surface);
            var color = Palette.ColorOf(swatch);
            var accent = Accent(surface, color);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Sample(surface, (x + 0.5f) / size, (y + 0.5f) / size, out _, out float shade, out float mix);
                    var shaded = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
                    pixels[y * size + x] = Color.Lerp(shaded, accent, Mathf.Clamp01(mix));
                }
            }
            return pixels;
        }

        /// <summary>
        /// Пиксели карты нормалей из высоты узора. Хранятся в RGB с альфой 1: такую текстуру
        /// встроенный рендер и URP распаковывают правильно без импорта как Normal Map.
        /// </summary>
        public static Color[] NormalPixels(Surface surface)
        {
            int size = Resolution(surface);
            var heights = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Sample(surface, (x + 0.5f) / size, (y + 0.5f) / size, out float height, out _, out _);
                    heights[y * size + x] = height;
                }
            }

            float strength = NormalStrength(surface) * size / 64f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x - 1 + size) % size];
                    float dy = heights[((y + 1) % size) * size + x] - heights[((y - 1 + size) % size) * size + x];
                    var normal = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    pixels[y * size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                }
            }
            return pixels;
        }

        /// <summary>
        /// Узор поверхности в точке (u, v) тайла: высота для рельефа, множитель яркости
        /// и доля цвета-вкрапления.
        /// </summary>
        private static void Sample(Surface surface, float u, float v, out float height, out float shade, out float accent)
        {
            accent = 0f;
            switch (surface)
            {
                case Surface.Ground:
                {
                    float soil = Fbm(u, v, 4, 4, 5, 11);
                    Cells(u, v, 18, 12, out float nearest, out _, out float pebbleSeed);
                    float radius = 0.15f + 0.25f * pebbleSeed;
                    float pebble = nearest < radius ? Mathf.Sqrt(1f - nearest / radius) : 0f;
                    // Трещины высохшей грязи — пятнами, а не сеткой по всей земле.
                    Cells(u, v, 7, 13, out float crackNear, out float crackSecond, out _);
                    float crack = (1f - SmoothStep(0f, 0.035f, crackSecond - crackNear)) * SmoothStep(0.5f, 0.68f, Fbm(u, v, 3, 3, 3, 14));
                    height = soil * 0.6f + pebble * 0.5f - crack * 0.3f;
                    shade = 0.8f + 0.35f * soil + (pebbleSeed - 0.45f) * 0.7f * pebble;
                    accent = crack * 0.6f;
                    break;
                }
                case Surface.Asphalt:
                {
                    float grain = Fbm(u, v, 32, 32, 3, 21);
                    Cells(u, v, 60, 22, out float nearest, out _, out float stoneSeed);
                    float stone = nearest < 0.3f ? 1f - nearest / 0.3f : 0f;
                    Cells(u, v, 5, 23, out float crackNear, out float crackSecond, out _);
                    float crack = (1f - SmoothStep(0f, 0.025f, crackSecond - crackNear)) * SmoothStep(0.55f, 0.7f, Fbm(u, v, 2, 2, 3, 24));
                    height = grain * 0.5f + stone * 0.5f - crack * 0.4f;
                    shade = 0.85f + 0.2f * grain + 0.6f * stone * (stoneSeed - 0.3f);
                    accent = crack * 0.7f;
                    break;
                }
                case Surface.Concrete:
                {
                    float blotch = Fbm(u, v, 3, 3, 4, 31);
                    float grain = Fbm(u, v, 48, 48, 2, 32);
                    Cells(u, v, 40, 33, out float nearest, out _, out float poreSeed);
                    float pore = nearest < 0.15f * poreSeed ? 1f : 0f;
                    // Следы стыков опалубки: тонкая линия у краёв каждой четверти тайла.
                    float line = SmoothStep(0.47f, 0.49f, Mathf.Abs(Frac(v * 4f) - 0.5f));
                    height = 0.5f + 0.2f * grain - 0.4f * pore - 0.3f * line;
                    shade = 0.82f + 0.25f * blotch + 0.08f * grain - 0.3f * pore - 0.08f * line;
                    accent = SmoothStep(0.58f, 0.8f, Fbm(u, v, 3, 2, 5, 34)) * 0.35f;
                    break;
                }
                case Surface.Metal:
                {
                    float brushed = Fbm(u, v, 2, 40, 3, 41);
                    float scratch = Scratches(u, v, 14, 42);
                    height = brushed * 0.3f - scratch * 0.3f;
                    shade = 0.85f + 0.2f * brushed + 0.25f * scratch;
                    accent = SmoothStep(0.72f, 0.9f, Fbm(u, v, 4, 4, 4, 43)) * 0.7f;
                    break;
                }
                case Surface.Rust:
                {
                    float coarse = Fbm(u, v, 4, 4, 5, 51);
                    float flakes = Fbm(u, v, 12, 12, 3, 52);
                    Cells(u, v, 30, 53, out float nearest, out _, out _);
                    float pit = nearest < 0.25f ? 1f - nearest / 0.25f : 0f;
                    height = coarse * 0.5f + flakes * 0.3f - pit * 0.4f;
                    shade = 0.7f + 0.5f * coarse - 0.2f * pit;
                    accent = SmoothStep(0.45f, 0.8f, flakes);
                    break;
                }
                case Surface.Wood:
                {
                    float warp = Fbm(u, v, 3, 3, 3, 61);
                    float grain = 0.5f + 0.5f * Mathf.Sin((u * 24f + warp * 0.8f) * 2f * Mathf.PI);
                    float fibres = Fbm(u, v, 64, 4, 3, 62);
                    Cells(u, v, 3, 63, out float nearest, out _, out float knotSeed);
                    float knot = knotSeed > 0.6f && nearest < 0.12f ? 1f - nearest / 0.12f : 0f;
                    height = grain * 0.5f + fibres * 0.3f - knot * 0.3f;
                    shade = 0.8f + 0.12f * grain + 0.14f * fibres;
                    accent = knot * 0.6f;
                    break;
                }
                case Surface.Bark:
                {
                    float ridges = Fbm(u, v, 12, 2, 4, 71);
                    float grooves = SmoothStep(0.3f, 0.7f, ridges);
                    height = grooves;
                    shade = 0.55f + 0.6f * grooves;
                    accent = SmoothStep(0.68f, 0.85f, Fbm(u, v, 16, 16, 3, 72)) * 0.35f * grooves;
                    break;
                }
                case Surface.Brick:
                {
                    const int rows = 8, perRow = 3;
                    float rowF = v * rows;
                    int row = Mathf.FloorToInt(rowF);
                    float shifted = u * perRow + (row % 2) * 0.5f;
                    int column = Mathf.FloorToInt(shifted);
                    float inRowU = Frac(shifted), inRowV = Frac(rowF);
                    bool mortar = inRowU < 0.04f || inRowU > 0.96f || inRowV < 0.08f || inRowV > 0.92f;
                    float brickTone = Hash(((column % perRow) + perRow) % perRow, row % rows, 81);
                    float grain = Fbm(u, v, 24, 24, 2, 82);
                    height = mortar ? 0.2f : 0.8f + 0.1f * grain;
                    shade = 0.85f + 0.3f * (brickTone - 0.5f) + 0.1f * grain;
                    accent = mortar ? 1f : 0f;
                    break;
                }
                case Surface.Paint:
                {
                    float body = Fbm(u, v, 6, 6, 4, 91);
                    float chip = SmoothStep(0.7f, 0.74f, Fbm(u, v, 16, 16, 3, 92));
                    height = 0.6f - 0.4f * chip + 0.05f * body;
                    shade = 0.9f + 0.12f * body;
                    accent = chip;
                    break;
                }
                case Surface.Fabric:
                {
                    float weave = Mathf.Sin(u * 2f * Mathf.PI * 32f) * Mathf.Sin(v * 2f * Mathf.PI * 32f);
                    float dirt = Fbm(u, v, 4, 4, 3, 101);
                    height = 0.5f + 0.3f * weave;
                    shade = 0.85f + 0.08f * weave + 0.15f * dirt;
                    break;
                }
                case Surface.Fur:
                {
                    float strands = Fbm(u, v, 48, 6, 3, 111);
                    float clumps = Fbm(u, v, 4, 4, 3, 112);
                    height = strands;
                    shade = 0.7f + 0.45f * strands * (0.7f + 0.3f * clumps);
                    break;
                }
                case Surface.Skin:
                {
                    float mottle = Fbm(u, v, 4, 4, 4, 121);
                    Cells(u, v, 50, 122, out float nearest, out _, out _);
                    float pore = nearest < 0.2f ? 1f - nearest / 0.2f : 0f;
                    height = 0.5f + 0.2f * mottle - 0.3f * pore;
                    shade = 0.85f + 0.2f * mottle - 0.15f * pore;
                    accent = SmoothStep(0.65f, 0.85f, Fbm(u, v, 3, 3, 3, 123)) * 0.5f;
                    break;
                }
                default:
                {
                    float noise = Fbm(u, v, 6, 6, 3, 131);
                    height = 0.5f + 0.1f * noise;
                    shade = 0.93f + 0.1f * noise;
                    break;
                }
            }
        }

        // ---------- Бесшовный шум ----------

        private static float Frac(float x) => x - Mathf.Floor(x);

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        private static int Wrap(int value, int period) => ((value % period) + period) % period;

        /// <summary>Шум значений на решётке periodX×periodY, замкнутой по краям — поэтому тайл бесшовный.</summary>
        private static float ValueNoise(float u, float v, int periodX, int periodY, int seed)
        {
            float x = u * periodX, y = v * periodY;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Wrap(x0, periodX), Wrap(y0, periodY), seed);
            float b = Hash(Wrap(x0 + 1, periodX), Wrap(y0, periodY), seed);
            float c = Hash(Wrap(x0, periodX), Wrap(y0 + 1, periodY), seed);
            float d = Hash(Wrap(x0 + 1, periodX), Wrap(y0 + 1, periodY), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Сумма октав шума (фрактальный шум): каждая следующая вдвое мельче и вдвое тише.</summary>
        private static float Fbm(float u, float v, int periodX, int periodY, int octaves, int seed)
        {
            float sum = 0f, amplitude = 1f, total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u, v, periodX, periodY, seed + o * 17) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                periodX *= 2;
                periodY *= 2;
            }
            return sum / total;
        }

        /// <summary>
        /// Клеточный шум: по одной случайной точке в каждой из count×count клеток (тоже замкнутых по краям).
        /// nearest/second — расстояния до ближайшей и второй точки в долях клетки, cellSeed — случайное число ближайшей.
        /// </summary>
        private static void Cells(float u, float v, int count, int seed, out float nearest, out float second, out float cellSeed)
        {
            float x = u * count, y = v * count;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            nearest = second = float.MaxValue;
            cellSeed = 0f;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = Wrap(cx + ox, count), gy = Wrap(cy + oy, count);
                    float px = cx + ox + Hash(gx, gy, seed);
                    float py = cy + oy + Hash(gx, gy, seed + 1);
                    float distance = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (distance < nearest)
                    {
                        second = nearest;
                        nearest = distance;
                        cellSeed = Hash(gx, gy, seed + 2);
                    }
                    else if (distance < second)
                    {
                        second = distance;
                    }
                }
            }
        }

        /// <summary>Тонкие светлые царапины: несколько случайных отрезков по тайлу.</summary>
        private static float Scratches(float u, float v, int count, int seed)
        {
            float result = 0f;
            for (int i = 0; i < count; i++)
            {
                float ax = Hash(i, 0, seed), ay = Hash(i, 1, seed);
                float angle = Hash(i, 2, seed) * Mathf.PI;
                float length = 0.1f + 0.3f * Hash(i, 3, seed);
                float dx = Mathf.Cos(angle), dy = Mathf.Sin(angle);
                // Ближайшая копия начала отрезка с учётом тайлинга.
                float rx = u - ax, ry = v - ay;
                rx -= Mathf.Round(rx);
                ry -= Mathf.Round(ry);
                float along = Mathf.Clamp(rx * dx + ry * dy, 0f, length);
                float distance = Mathf.Sqrt((rx - dx * along) * (rx - dx * along) + (ry - dy * along) * (ry - dy * along));
                result = Mathf.Max(result, 1f - SmoothStep(0f, 0.004f, distance));
            }
            return result;
        }
    }
}
