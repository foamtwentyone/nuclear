using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nuclear.UI
{
    /// <summary>
    /// «Нарисованные от руки» фигуры для HUD через GL: неровные контуры маркером, заливки, штриховка.
    /// Линии дрожат по шуму (seed), края сглажены прозрачной каймой. Вызывать только из OnGUI;
    /// на событиях кроме Repaint ничего не рисует. Координаты — пиксели экрана, y вниз (как у GUI).
    /// </summary>
    public static class HudSketch
    {
        private const float Feather = 1.2f;

        private static Material material;
        private static readonly List<Vector2> Points = new();
        private static readonly List<Vector2> Normals = new();

        private static Material Material
        {
            get
            {
                if (material != null)
                    return material;
                material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.DontSave };
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Cull", (int)CullMode.Off);
                material.SetInt("_ZWrite", 0);
                material.SetInt("_ZTest", (int)CompareFunction.Always);
                return material;
            }
        }

        /// <summary>Номер «кадра» дрожания для тревожных элементов: линии перерисовываются ~8 раз в секунду.</summary>
        public static int Boil => Mathf.FloorToInt(Time.unscaledTime * 8f) * 31;

        // ---------- Контуры ----------

        /// <summary>Неровный скруглённый прямоугольник; amp — сила дрожания в пикселях. Поворот — вокруг центра.</summary>
        public static List<Vector2> RoundedRect(Rect rect, float radius, int seed, float amp, float rotation = 0f)
        {
            var points = new List<Vector2>();
            radius = Mathf.Min(radius, rect.width / 2f, rect.height / 2f);
            float step = Mathf.Max(6f, HudDraw.S(9f));
            var corners = new[]
            {
                new Vector2(rect.xMax - radius, rect.y + radius),
                new Vector2(rect.xMax - radius, rect.yMax - radius),
                new Vector2(rect.x + radius, rect.yMax - radius),
                new Vector2(rect.x + radius, rect.y + radius),
            };
            for (int c = 0; c < 4; c++)
            {
                // Дуга угла (по часовой в экранных координатах), потом прямая до следующего угла.
                float start = -90f + c * 90f;
                for (int i = 0; i <= 5; i++)
                {
                    float a = (start + i * 18f) * Mathf.Deg2Rad;
                    points.Add(corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
                var from = points[points.Count - 1];
                float nextA = (start + 90f) * Mathf.Deg2Rad;
                var to = corners[(c + 1) % 4] + new Vector2(Mathf.Cos(nextA), Mathf.Sin(nextA)) * radius;
                int segments = Mathf.FloorToInt(Vector2.Distance(from, to) / step);
                for (int i = 1; i < segments; i++)
                    points.Add(Vector2.Lerp(from, to, i / (float)segments));
            }
            Displace(points, seed, amp, true);
            if (rotation != 0f)
                Rotate(points, rect.center, rotation);
            return points;
        }

        public static List<Vector2> Circle(Vector2 center, float radius, int seed, float amp)
        {
            var points = new List<Vector2>();
            int count = Mathf.Clamp(Mathf.RoundToInt(radius * 0.9f), 16, 64);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                points.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            Displace(points, seed, amp, true);
            return points;
        }

        /// <summary>Неровная линия из a в b.</summary>
        public static List<Vector2> Line(Vector2 a, Vector2 b, int seed, float amp)
        {
            var points = new List<Vector2>();
            int segments = Mathf.Max(1, Mathf.FloorToInt(Vector2.Distance(a, b) / Mathf.Max(6f, HudDraw.S(10f))));
            for (int i = 0; i <= segments; i++)
                points.Add(Vector2.Lerp(a, b, i / (float)segments));
            Displace(points, seed, amp, false);
            return points;
        }

        private static void Displace(List<Vector2> points, int seed, float amp, bool closed)
        {
            if (amp <= 0f)
                return;
            ComputeNormals(points, closed);
            float length = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0)
                    length += Vector2.Distance(points[i], points[i - 1]);
                // Концы открытой линии не двигаем: так она остаётся там, где её начали и закончили.
                float pin = closed ? 1f : Mathf.Min(1f, Mathf.Min(i, points.Count - 1 - i));
                points[i] += Normals[i] * (Noise(seed, length * 0.045f / HudDraw.Scale) * amp * pin);
            }
        }

        public static void Rotate(List<Vector2> points, Vector2 pivot, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            for (int i = 0; i < points.Count; i++)
            {
                var d = points[i] - pivot;
                points[i] = pivot + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
            }
        }

        /// <summary>Плавный шум −1..1: сумма синусов с «неровными» частотами.</summary>
        public static float Noise(int seed, float t) =>
            Mathf.Sin(t * 1.7f + seed * 12.9898f) * 0.5f + Mathf.Sin(t * 3.13f + seed * 78.233f) * 0.32f + Mathf.Sin(t * 5.37f + seed * 3.71f) * 0.18f;

        // ---------- Рисование ----------

        /// <summary>Заливка контура (веером от центра — для выпуклых и почти выпуклых фигур).</summary>
        public static void Fill(List<Vector2> points, Color color)
        {
            if (Event.current.type != EventType.Repaint || points.Count < 3)
                return;
            ComputeNormals(points, true);
            var center = Vector2.zero;
            foreach (var p in points)
                center += p;
            center /= points.Count;
            var clear = new Color(color.r, color.g, color.b, 0f);

            Begin(GL.TRIANGLES);
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                Vertex(center, color);
                Vertex(points[i], color);
                Vertex(points[j], color);
                // Кайма сглаживания.
                Vertex(points[i], color);
                Vertex(points[i] + Normals[i] * Feather, clear);
                Vertex(points[j] + Normals[j] * Feather, clear);
                Vertex(points[i], color);
                Vertex(points[j] + Normals[j] * Feather, clear);
                Vertex(points[j], color);
            }
            End();
        }

        /// <summary>Линия маркером: толщина слегка «гуляет», края сглажены.</summary>
        public static void Stroke(List<Vector2> points, float width, Color color, bool closed, int seed = 0)
        {
            if (Event.current.type != EventType.Repaint || points.Count < 2)
                return;
            ComputeNormals(points, closed);
            var clear = new Color(color.r, color.g, color.b, 0f);
            int count = closed ? points.Count : points.Count - 1;

            Begin(GL.TRIANGLES);
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % points.Count;
                float wi = HalfWidth(width, seed, i), wj = HalfWidth(width, seed, j);
                Vector2 pi = points[i], pj = points[j], ni = Normals[i], nj = Normals[j];
                Quad(pi + ni * wi, pj + nj * wj, pj - nj * wj, pi - ni * wi, color, color);
                Quad(pi + ni * (wi + Feather), pj + nj * (wj + Feather), pj + nj * wj, pi + ni * wi, clear, color);
                Quad(pi - ni * wi, pj - nj * wj, pj - nj * (wj + Feather), pi - ni * (wi + Feather), color, clear);
            }
            End();

            // Круглые концы у открытой линии.
            if (!closed)
            {
                Fill(Circle(points[0], HalfWidth(width, seed, 0), 0, 0f), color);
                Fill(Circle(points[points.Count - 1], HalfWidth(width, seed, points.Count - 1), 0, 0f), color);
            }
        }

        /// <summary>Косая штриховка внутри прямоугольника (линии под 45°).</summary>
        public static void Hatch(Rect rect, float spacing, float width, Color color, int seed)
        {
            if (rect.width <= 1f || rect.height <= 1f)
                return;
            for (float offset = -rect.height; offset < rect.width; offset += spacing)
            {
                // Линия x = rect.x + offset + (rect.yMax - y); обрезаем по прямоугольнику.
                float x0 = rect.x + offset, y0 = rect.yMax;
                float x1 = x0 + rect.height, y1 = rect.y;
                if (x0 < rect.x)
                {
                    y0 -= rect.x - x0;
                    x0 = rect.x;
                }
                if (x1 > rect.xMax)
                {
                    y1 += x1 - rect.xMax;
                    x1 = rect.xMax;
                }
                if (y0 - y1 < 1f)
                    continue;
                Stroke(Line(new Vector2(x0, y0), new Vector2(x1, y1), seed + (int)offset, 0f), width, color, false);
            }
        }

        private static float HalfWidth(float width, int seed, int index) =>
            width * 0.5f * (1f + 0.18f * Noise(seed + 101, index * 0.6f));

        private static void ComputeNormals(List<Vector2> points, bool closed)
        {
            Normals.Clear();
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            // Нормаль наружу для замкнутых контуров при любом обходе.
            float sign = closed && area < 0f ? -1f : 1f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 prev = closed ? points[(i - 1 + points.Count) % points.Count] : points[Mathf.Max(0, i - 1)];
                Vector2 next = closed ? points[(i + 1) % points.Count] : points[Mathf.Min(points.Count - 1, i + 1)];
                var tangent = (next - prev).normalized;
                Normals.Add(new Vector2(tangent.y, -tangent.x) * sign);
            }
        }

        private static void Begin(int mode)
        {
            Material.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
            GL.Begin(mode);
        }

        private static void End()
        {
            GL.End();
            GL.PopMatrix();
        }

        private static void Vertex(Vector2 p, Color color)
        {
            GL.Color(color);
            GL.Vertex3(p.x, p.y, 0f);
        }

        private static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ab, Color cd)
        {
            Vertex(a, ab);
            Vertex(b, ab);
            Vertex(c, cd);
            Vertex(a, ab);
            Vertex(c, cd);
            Vertex(d, cd);
        }
    }
}
