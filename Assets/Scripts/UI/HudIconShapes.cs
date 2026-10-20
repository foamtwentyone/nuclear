using System;

namespace Nuclear.UI
{
    public enum HudIcon
    {
        Heart,
        Stamina,
        Filter,
        Bandage,
        Flashlight,
        Crowbar,
        Cell,
        Radiation,
        Microphone,
        Can,
    }

    /// <summary>
    /// Иконки HUD как поля расстояний (без Unity, чтобы их можно было проверить вне редактора).
    /// Координаты в квадрате [-1, 1], y вверх; расстояние отрицательно внутри фигуры.
    /// Растр: белая заливка с лёгким градиентом и тёмная обводка, как у контуров в мире.
    /// </summary>
    public static class HudIconShapes
    {
        private const float Outline = 0.1f;

        /// <summary>Заполняет rgba (size × size × 4, строки снизу вверх, как у Texture2D).</summary>
        public static void Rasterize(HudIcon icon, int size, byte[] rgba)
        {
            float pixel = 2f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2f - 1f;
                    float py = (y + 0.5f) / size * 2f - 1f;
                    float d = Distance(icon, px, py);
                    float fill = Clamp01(0.5f - d / pixel);
                    float shape = Clamp01(0.5f - (d - Outline) / pixel);
                    // Сверху светлее, снизу чуть темнее — объём как у мультяшных иконок.
                    float shade = 0.84f + 0.16f * Clamp01(py * 0.5f + 0.5f);
                    float value = fill * shade;
                    int i = (y * size + x) * 4;
                    byte v = (byte)(value * 255f + 0.5f);
                    rgba[i] = v;
                    rgba[i + 1] = v;
                    rgba[i + 2] = v;
                    rgba[i + 3] = (byte)(shape * 255f + 0.5f);
                }
            }
        }

        public static float Distance(HudIcon icon, float x, float y) => icon switch
        {
            HudIcon.Heart => Heart(x, y),
            HudIcon.Stamina => Bolt(x, y),
            HudIcon.Filter => Filter(x, y),
            HudIcon.Bandage => Bandage(x, y),
            HudIcon.Flashlight => Flashlight(x, y),
            HudIcon.Crowbar => Crowbar(x, y),
            HudIcon.Cell => Cell(x, y),
            HudIcon.Radiation => Radiation(x, y),
            HudIcon.Microphone => Microphone(x, y),
            HudIcon.Can => Can(x, y),
            _ => 1f,
        };

        // ---------- Фигуры ----------

        private static float Heart(float x, float y)
        {
            // Формула Иниго Квилеса, сердце в [-0.5,0.5]×[0,1]; растягиваем на иконку.
            float s = 1.3f;
            float px = MathF.Abs(x) / s, py = (y + 0.72f) / s;
            float d;
            if (py + px > 1f)
                d = Length(px - 0.25f, py - 0.75f) - MathF.Sqrt(2f) / 4f;
            else
            {
                float m = 0.5f * MathF.Max(px + py, 0f);
                d = MathF.Sqrt(MathF.Min(Dot2(px, py - 1f), Dot2(px - m, py - m))) * MathF.Sign(px - py);
            }
            return d * s;
        }

        private static float Bolt(float x, float y) => Polygon(x, y, new[]
        {
            0.22f, 0.86f, -0.5f, -0.02f, -0.02f, -0.02f, -0.24f, -0.86f, 0.52f, 0.12f, 0.04f, 0.12f,
        });

        private static float Filter(float x, float y)
        {
            float body = RoundBox(x, y + 0.18f, 0.56f, 0.6f, 0.16f);
            float neck = RoundBox(x, y - 0.6f, 0.26f, 0.2f, 0.06f);
            float shape = MathF.Min(body, neck);
            // Рёбра — тонкие прорези поперёк корпуса.
            foreach (float rib in new[] { 0.12f, -0.18f, -0.48f })
                shape = MathF.Max(shape, -RoundBox(x, y - rib, 0.44f, 0.035f, 0.03f));
            return shape;
        }

        private static float Bandage(float x, float y)
        {
            Rotate(ref x, ref y, -0.6f);
            float strip = RoundBox(x, y, 0.92f, 0.36f, 0.34f);
            float pad = RoundBox(x, y, 0.3f, 0.26f, 0.08f);
            // Подушечка — внутренний контур, по бокам ряды дырочек.
            float shape = MathF.Max(strip, -MathF.Abs(pad) + 0.035f);
            foreach (float hx in new[] { -0.62f, -0.48f, 0.48f, 0.62f })
                foreach (float hy in new[] { -0.12f, 0.12f })
                    shape = MathF.Max(shape, -(Length(x - hx, y - hy) - 0.045f));
            return shape;
        }

        private static float Flashlight(float x, float y)
        {
            Rotate(ref x, ref y, 0.6f);
            float body = RoundBox(x + 0.22f, y, 0.52f, 0.2f, 0.06f);
            float head = Polygon(x, y, new[] { 0.28f, -0.2f, 0.62f, -0.36f, 0.62f, 0.36f, 0.28f, 0.2f });
            float lens = RoundBox(x - 0.7f, y, 0.06f, 0.34f, 0.04f);
            float button = RoundBox(x + 0.05f, y - 0.24f, 0.1f, 0.06f, 0.03f);
            return MathF.Min(MathF.Min(body, head), MathF.Min(lens, button));
        }

        private static float Crowbar(float x, float y)
        {
            float shaft = Segment(x, y, -0.62f, -0.72f, 0.5f, 0.56f) - 0.12f;
            // Гвоздодёр: загиб назад и раздвоенный кончик.
            float hook = Segment(x, y, 0.5f, 0.56f, 0.36f, 0.82f) - 0.12f;
            float tip = Segment(x, y, 0.36f, 0.82f, 0.14f, 0.8f) - 0.1f;
            float shape = MathF.Min(shaft, MathF.Min(hook, tip));
            float blade = Segment(x, y, -0.62f, -0.72f, -0.82f, -0.66f) - 0.09f;
            return MathF.Min(shape, blade);
        }

        private static float Cell(float x, float y)
        {
            float body = RoundBox(x, y + 0.08f, 0.4f, 0.74f, 0.14f);
            float cap = RoundBox(x, y - 0.76f, 0.16f, 0.1f, 0.04f);
            float shape = MathF.Min(body, cap);
            // Плюс на корпусе.
            float plus = MathF.Min(RoundBox(x, y - 0.2f, 0.2f, 0.05f, 0.02f), RoundBox(x, y - 0.2f, 0.05f, 0.2f, 0.02f));
            shape = MathF.Max(shape, -plus);
            float band = RoundBox(x, y + 0.42f, 0.42f, 0.035f, 0.01f);
            return MathF.Max(shape, -band);
        }

        private static float Radiation(float x, float y)
        {
            float center = Length(x, y) - 0.17f;
            float r = Length(x, y);
            float ring = MathF.Max(0.3f - r, r - 0.8f);
            // Три лопасти по 60° через каждые 120°, одна смотрит вверх.
            float angle = MathF.Atan2(x, y);
            float sector = MathF.PI / 3f;
            float local = Mod(angle + sector / 2f, 2f * sector) - sector / 2f;
            float blades = MathF.Max(ring, (MathF.Abs(local) - sector / 2f) * r);
            return MathF.Min(center, blades);
        }

        private static float Microphone(float x, float y)
        {
            float head = RoundBox(x, y - 0.3f, 0.28f, 0.48f, 0.28f);
            // Дуга-держатель: кольцо, оставляем нижнюю половину.
            float r = Length(x, y - 0.12f);
            float arc = MathF.Max(MathF.Abs(r - 0.5f) - 0.07f, y - 0.12f);
            float stem = RoundBox(x, y + 0.5f, 0.07f, 0.14f, 0.02f);
            float foot = RoundBox(x, y + 0.66f, 0.3f, 0.06f, 0.04f);
            return MathF.Min(MathF.Min(head, arc), MathF.Min(stem, foot));
        }

        private static float Can(float x, float y)
        {
            float body = RoundBox(x, y, 0.5f, 0.66f, 0.12f);
            // Ободки сверху и снизу и этикетка — прорези.
            float shape = body;
            foreach (float rim in new[] { 0.48f, -0.48f })
                shape = MathF.Max(shape, -RoundBox(x, y - rim, 0.42f, 0.03f, 0.02f));
            float label = RoundBox(x, y, 0.3f, 0.2f, 0.06f);
            return MathF.Max(shape, -(MathF.Abs(label) - 0.03f));
        }

        // ---------- Примитивы ----------

        private static float RoundBox(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            float qx = MathF.Abs(x) - halfWidth + radius;
            float qy = MathF.Abs(y) - halfHeight + radius;
            return Length(MathF.Max(qx, 0f), MathF.Max(qy, 0f)) + MathF.Min(MathF.Max(qx, qy), 0f) - radius;
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Length(pax - bax * h, pay - bay * h);
        }

        /// <summary>Многоугольник по вершинам x0,y0,x1,y1… (формула Иниго Квилеса).</summary>
        private static float Polygon(float x, float y, float[] v)
        {
            int n = v.Length / 2;
            float d = Dot2(x - v[0], y - v[1]);
            float s = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float ex = v[j * 2] - v[i * 2], ey = v[j * 2 + 1] - v[i * 2 + 1];
                float wx = x - v[i * 2], wy = y - v[i * 2 + 1];
                float h = Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                d = MathF.Min(d, Dot2(wx - ex * h, wy - ey * h));
                bool c1 = y >= v[i * 2 + 1], c2 = y < v[j * 2 + 1], c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                    s = -s;
            }
            return s * MathF.Sqrt(d);
        }

        private static void Rotate(ref float x, ref float y, float angle)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            (x, y) = (c * x - s * y, s * x + c * y);
        }

        private static float Length(float x, float y) => MathF.Sqrt(x * x + y * y);
        private static float Dot2(float x, float y) => x * x + y * y;
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Mod(float a, float m) => a - m * MathF.Floor(a / m);
    }
}
