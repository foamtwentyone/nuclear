using UnityEngine;

namespace Nuclear.EditorTools.LowPoly
{
    /// <summary>Тип поверхности: от него зависят рисунок текстуры, её масштаб в мире и рельеф.</summary>
    public enum Surface
    {
        Ground, Asphalt, Concrete, Metal, Rust, Wood, Bark, Brick, Paint, Fabric, Fur, Skin, Plain,
    }

    /// <summary>
    /// Процедурные текстуры для low-poly моделей в мультяшном стиле: цвет (с простым узором) и лёгкий рельеф.
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
            Swatch.Fur or Swatch.FurDark or Swatch.FurPale or Swatch.MouthFur or Swatch.MouthFurLight => Surface.Fur,
            Swatch.Skin or Swatch.SkinDark or Swatch.SkinPale or Swatch.NosePink or Swatch.NoseDark or Swatch.Mouth
                or Swatch.CatfishBack or Swatch.CatfishFin or Swatch.CatfishBelly or Swatch.FrogSkin or Swatch.FrogBelly or Swatch.FrogWart
                or Swatch.FrogSkinLight or Swatch.FrogLip or Swatch.FrogToe or Swatch.FrogWartBrown or Swatch.FrogEyeRing
                or Swatch.Lips or Swatch.Gums or Swatch.Throat => Surface.Skin,
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
            Surface.Fabric => 1f,
            Surface.Fur => 0.6f,
            Surface.Skin => 0.6f,
            _ => 0.5f,
        };

        public static int Resolution(Surface surface) => 128;

        public static float Smoothness(Swatch swatch) => swatch switch
        {
            Swatch.Glass or Swatch.Lens => 0.6f,
            Swatch.NosePink or Swatch.Mouth or Swatch.Gums or Swatch.Throat or Swatch.Lips => 0.35f,
            Swatch.FrogEye or Swatch.FrogSkin => 0.3f,
            _ => Of(swatch) == Surface.Metal ? 0.3f : 0.05f,
        };

        private static float NormalStrength(Surface surface) => surface switch
        {
            Surface.Brick => 1.5f,
            Surface.Wood or Surface.Ground or Surface.Bark => 0.8f,
            Surface.Concrete or Surface.Paint or Surface.Fur => 0.5f,
            _ => 0.2f,
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
        /// и доля цвета-вкрапления. Стиль — мультяшный, «нарисованный»: крупные мягкие пятна
        /// с двумя-тремя ступенями тона, чистые простые формы (доски, кирпичи, сколы), почти без мелкого шума.
        /// </summary>
        private static void Sample(Surface surface, float u, float v, out float height, out float shade, out float accent)
        {
            // Общая основа: крупное мягкое пятно, разбитое на ступени тона, как мазки кистью.
            float wash = Bands(Fbm(u, v, 2, 2, 2, 1), 3);
            shade = 0.94f + 0.1f * wash;
            height = 0.5f;
            accent = 0f;

            switch (surface)
            {
                case Surface.Ground:
                {
                    // Редкие круглые камешки: светлая шапка и тёмный ободок.
                    Cells(u, v, 9, 12, out float nearest, out _, out float stone);
                    float radius = stone > 0.55f ? 0.18f + 0.12f * stone : 0f;
                    float pebble = nearest < radius ? 1f : 0f;
                    float rim = nearest < radius + 0.05f && nearest >= radius ? 1f : 0f;
                    shade = 0.92f + 0.14f * Bands(Fbm(u, v, 3, 3, 2, 11), 3) + 0.14f * pebble - 0.2f * rim;
                    height = 0.5f + 0.4f * pebble;
                    break;
                }
                case Surface.Asphalt:
                {
                    Cells(u, v, 14, 22, out float nearest, out _, out float speck);
                    float dot = speck > 0.7f && nearest < 0.12f ? 1f : 0f;
                    shade += 0.18f * dot;
                    break;
                }
                case Surface.Concrete:
                {
                    // Чистые стыки опалубки и одно-два мягких пятна сырости.
                    float seam = SmoothStep(0.475f, 0.485f, Mathf.Abs(Frac(v * 2f) - 0.5f));
                    float damp = Bands(SmoothStep(0.55f, 0.75f, Fbm(u, v, 2, 2, 3, 34)), 2);
                    shade = 0.95f + 0.07f * wash - 0.1f * seam;
                    height = 0.5f - 0.3f * seam;
                    accent = damp * 0.25f;
                    break;
                }
                case Surface.Metal:
                {
                    // Пара стилизованных бликов-полос вместо шлифовки и царапин.
                    // Целые коэффициенты при u и v — полосы стыкуются между копиями тайла.
                    float streak = SmoothStep(0.93f, 0.96f, Mathf.Sin((u * 2f + v) * 2f * Mathf.PI) * 0.5f + 0.5f);
                    shade = 0.92f + 0.08f * wash + 0.12f * streak;
                    accent = Bands(SmoothStep(0.65f, 0.8f, Fbm(u, v, 3, 3, 2, 43)), 2) * 0.6f;
                    break;
                }
                case Surface.Rust:
                {
                    // Два тона с чёткой границей, как заливка в мультфильме.
                    accent = Bands(SmoothStep(0.45f, 0.6f, Fbm(u, v, 4, 4, 3, 52)), 2) * 0.7f;
                    break;
                }
                case Surface.Wood:
                {
                    // Несколько тёмных волнистых линий волокон на ровной доске.
                    float wave = Mathf.Sin(v * 2f * Mathf.PI * 2f + Hash(Mathf.FloorToInt(u * 6f), 0, 61) * 6f) * 0.03f;
                    float line = 1f - SmoothStep(0.02f, 0.04f, Mathf.Abs(Frac(u * 6f + wave) - 0.5f));
                    Cells(u, v, 3, 63, out float nearest, out _, out float knotSeed);
                    float knot = knotSeed > 0.6f && nearest < 0.08f ? 1f : 0f;
                    shade = 0.95f + 0.08f * wash - 0.16f * line;
                    height = 0.5f - 0.3f * line - 0.3f * knot;
                    accent = knot * 0.55f;
                    break;
                }
                case Surface.Bark:
                {
                    // Вертикальные полосы коры двух тонов.
                    float stripes = Bands(Fbm(u, v, 8, 1, 2, 71), 2);
                    shade = 0.8f + 0.3f * stripes;
                    height = stripes;
                    break;
                }
                case Surface.Brick:
                {
                    const int rows = 6, perRow = 3;
                    float rowF = v * rows;
                    int row = Mathf.FloorToInt(rowF);
                    float shifted = u * perRow + (row % 2) * 0.5f;
                    int column = Mathf.FloorToInt(shifted);
                    float inRowU = Frac(shifted), inRowV = Frac(rowF);
                    bool mortar = inRowU < 0.05f || inRowU > 0.95f || inRowV < 0.1f || inRowV > 0.9f;
                    float tone = Hash(((column % perRow) + perRow) % perRow, row % rows, 81);
                    shade = 0.92f + 0.14f * Bands(tone, 3);
                    height = mortar ? 0.2f : 0.8f;
                    accent = mortar ? 1f : 0f;
                    break;
                }
                case Surface.Paint:
                {
                    // Редкие крупные сколы с чистым краем.
                    float chip = SmoothStep(0.76f, 0.78f, Fbm(u, v, 4, 4, 2, 92));
                    height = 0.6f - 0.3f * chip;
                    accent = chip;
                    break;
                }
                case Surface.Fur:
                {
                    // Пучки шерсти: короткие тёмные штрихи, как нарисованные.
                    float strokes = Bands(Fbm(u, v, 16, 4, 2, 111), 3);
                    shade = 0.88f + 0.2f * strokes;
                    height = strokes;
                    break;
                }
                case Surface.Skin:
                {
                    accent = Bands(SmoothStep(0.62f, 0.78f, Fbm(u, v, 3, 3, 2, 123)), 2) * 0.35f;
                    break;
                }
            }
        }

        /// <summary>Разбивает плавное значение 0..1 на levels ступеней с мягкими краями — «мультяшный» тон.</summary>
        private static float Bands(float value, int levels)
        {
            float scaled = Mathf.Clamp01(value) * levels;
            float step = Mathf.Floor(scaled);
            return Mathf.Clamp01((step + SmoothStep(0.4f, 0.6f, scaled - step)) / levels);
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
    }
}
