using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nuclear.EditorTools.LowPoly
{
    /// <summary>
    /// «Лепка» из мягких форм: каждая форма задана функцией расстояния до поверхности (SDF),
    /// формы сливаются с плавным переходом (smooth union) или вырезаются. Результат превращается
    /// в один цельный гладкий меш методом Surface Nets — так получаются «пластилиновые» существа
    /// без стыков между частями, как в Peak и Pratfall.
    /// </summary>
    public sealed class Sdf
    {
        private enum Kind { Ellipsoid, RoundCone, RoundBox }

        private sealed class Part
        {
            public Kind Kind;
            public Vector3 Center, A, B, Size;
            public Quaternion Inverse = Quaternion.identity;
            public float RadiusA, RadiusB, Rounding;
            public float Blend;
            public bool Subtract;
            public Func<Vector3, Swatch> Color;
        }

        private readonly List<Part> parts = new();

        /// <summary>Эллипсоид. blend — ширина плавного перехода к уже вылепленному (в метрах).</summary>
        public void Ellipsoid(Vector3 center, Vector3 radii, Quaternion rotation, float blend, Func<Vector3, Swatch> color) =>
            parts.Add(new Part { Kind = Kind.Ellipsoid, Center = center, Size = radii, Inverse = Quaternion.Inverse(rotation), Blend = blend, Color = color });

        public void Ellipsoid(Vector3 center, Vector3 radii, float blend, Swatch swatch) =>
            Ellipsoid(center, radii, Quaternion.identity, blend, _ => swatch);

        /// <summary>Скруглённый конус между точками a и b (конечности, щупальца, шипы).</summary>
        public void RoundCone(Vector3 a, Vector3 b, float radiusA, float radiusB, float blend, Func<Vector3, Swatch> color) =>
            parts.Add(new Part { Kind = Kind.RoundCone, A = a, B = b, RadiusA = radiusA, RadiusB = radiusB, Blend = blend, Color = color });

        public void RoundCone(Vector3 a, Vector3 b, float radiusA, float radiusB, float blend, Swatch swatch) =>
            RoundCone(a, b, radiusA, radiusB, blend, _ => swatch);

        /// <summary>Коробка со скруглёнными рёбрами (зубы, когти).</summary>
        public void RoundBox(Vector3 center, Vector3 halfSize, Quaternion rotation, float rounding, float blend, Swatch swatch) =>
            parts.Add(new Part { Kind = Kind.RoundBox, Center = center, Size = halfSize, Inverse = Quaternion.Inverse(rotation), Rounding = rounding, Blend = blend, Color = _ => swatch });

        /// <summary>Вырезать эллипсоид из уже вылепленного; стенка выемки красится в swatch.</summary>
        public void Carve(Vector3 center, Vector3 radii, float blend, Swatch swatch) =>
            parts.Add(new Part { Kind = Kind.Ellipsoid, Center = center, Size = radii, Blend = blend, Subtract = true, Color = _ => swatch });

        /// <summary>Расстояние до поверхности (меньше нуля — внутри) и цвет ближайшей формы.</summary>
        public float Distance(Vector3 point, out Swatch swatch)
        {
            float distance = float.MaxValue;
            swatch = Swatch.Fur;
            foreach (var part in parts)
            {
                float d = PartDistance(part, point);
                if (part.Subtract)
                {
                    float carved = SmoothMax(distance, -d, part.Blend);
                    if (-d > distance)
                        swatch = part.Color(point);
                    distance = carved;
                }
                else
                {
                    if (d < distance)
                        swatch = part.Color(point);
                    distance = distance == float.MaxValue ? d : SmoothMin(distance, d, part.Blend);
                }
            }
            return distance;
        }

        private static float PartDistance(Part part, Vector3 point)
        {
            switch (part.Kind)
            {
                case Kind.Ellipsoid:
                {
                    // Приближённое расстояние до эллипсоида (Иниго Килес): точное на поверхности, гладкое вне её.
                    Vector3 p = part.Inverse * (point - part.Center);
                    Vector3 r = part.Size;
                    float k0 = new Vector3(p.x / r.x, p.y / r.y, p.z / r.z).magnitude;
                    float k1 = new Vector3(p.x / (r.x * r.x), p.y / (r.y * r.y), p.z / (r.z * r.z)).magnitude;
                    return k1 > 1e-6f ? k0 * (k0 - 1f) / k1 : -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
                }
                case Kind.RoundCone:
                    return RoundConeDistance(point, part.A, part.B, part.RadiusA, part.RadiusB);
                default:
                {
                    Vector3 p = part.Inverse * (point - part.Center);
                    Vector3 q = new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) - part.Size + Vector3.one * part.Rounding;
                    Vector3 outside = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f));
                    return outside.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - part.Rounding;
                }
            }
        }

        /// <summary>Расстояние до скруглённого конуса (формула Иниго Килеса).</summary>
        private static float RoundConeDistance(Vector3 p, Vector3 a, Vector3 b, float r1, float r2)
        {
            Vector3 ba = b - a;
            float l2 = Vector3.Dot(ba, ba);
            float rr = r1 - r2;
            float a2 = l2 - rr * rr;
            float il2 = 1f / l2;
            Vector3 pa = p - a;
            float y = Vector3.Dot(pa, ba);
            float z = y - l2;
            Vector3 xv = pa * l2 - ba * y;
            float x2 = Vector3.Dot(xv, xv);
            float y2 = y * y * l2;
            float z2 = z * z * l2;
            float k = Mathf.Sign(rr) * rr * rr * x2;
            if (Mathf.Sign(z) * a2 * z2 > k)
                return Mathf.Sqrt(x2 + z2) * il2 - r2;
            if (Mathf.Sign(y) * a2 * y2 < k)
                return Mathf.Sqrt(x2 + y2) * il2 - r1;
            return (Mathf.Sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
        }

        /// <summary>Плавный минимум: формы сливаются, как капли, в полосе шириной blend.</summary>
        private static float SmoothMin(float a, float b, float blend)
        {
            if (blend <= 0f)
                return Mathf.Min(a, b);
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / blend);
            return Mathf.Lerp(b, a, h) - blend * h * (1f - h);
        }

        private static float SmoothMax(float a, float b, float blend) => -SmoothMin(-a, -b, blend);

        /// <summary>
        /// Превращает вылепленное в треугольники методом Surface Nets: по вершине в каждой ячейке сетки,
        /// через которую проходит поверхность, и по четырёхугольнику на каждое пересечённое ребро сетки.
        /// Вершина сдвигается на поверхность шагом по градиенту, чтобы форма была точной.
        /// Цвет треугольника — цвет ближайшей формы в его центре.
        /// </summary>
        public void Polygonize(MeshKit kit, Vector3 min, Vector3 max, float cell)
        {
            int nx = Mathf.CeilToInt((max.x - min.x) / cell), ny = Mathf.CeilToInt((max.y - min.y) / cell), nz = Mathf.CeilToInt((max.z - min.z) / cell);
            var values = new float[nx + 1, ny + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= ny; j++)
                    for (int k = 0; k <= nz; k++)
                        values[i, j, k] = Distance(GridPoint(min, cell, i, j, k), out _);

            var cellVertex = new int[nx, ny, nz];
            var positions = new List<Vector3>();
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    for (int k = 0; k < nz; k++)
                    {
                        cellVertex[i, j, k] = -1;
                        Vector3 sum = Vector3.zero;
                        int crossings = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            var (c0, c1) = CubeEdge(e);
                            float v0 = values[i + c0.x, j + c0.y, k + c0.z], v1 = values[i + c1.x, j + c1.y, k + c1.z];
                            if ((v0 < 0f) == (v1 < 0f))
                                continue;
                            float t = v0 / (v0 - v1);
                            sum += Vector3.Lerp(GridPoint(min, cell, i + c0.x, j + c0.y, k + c0.z), GridPoint(min, cell, i + c1.x, j + c1.y, k + c1.z), t);
                            crossings++;
                        }
                        if (crossings == 0)
                            continue;
                        cellVertex[i, j, k] = positions.Count;
                        positions.Add(ProjectToSurface(sum / crossings, cell));
                    }
                }
            }

            // Рёбра сетки вдоль X, Y, Z: четыре ячейки вокруг ребра дают четырёхугольник.
            for (int i = 0; i < nx; i++)
                for (int j = 1; j < ny; j++)
                    for (int k = 1; k < nz; k++)
                        EmitQuad(kit, values[i, j, k], values[i + 1, j, k], Vector3.right,
                            positions, cellVertex[i, j - 1, k - 1], cellVertex[i, j, k - 1], cellVertex[i, j, k], cellVertex[i, j - 1, k]);
            for (int i = 1; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    for (int k = 1; k < nz; k++)
                        EmitQuad(kit, values[i, j, k], values[i, j + 1, k], Vector3.up,
                            positions, cellVertex[i - 1, j, k - 1], cellVertex[i, j, k - 1], cellVertex[i, j, k], cellVertex[i - 1, j, k]);
            for (int i = 1; i < nx; i++)
                for (int j = 1; j < ny; j++)
                    for (int k = 0; k < nz; k++)
                        EmitQuad(kit, values[i, j, k], values[i, j, k + 1], Vector3.forward,
                            positions, cellVertex[i - 1, j - 1, k], cellVertex[i, j - 1, k], cellVertex[i, j, k], cellVertex[i - 1, j, k]);
        }

        private void EmitQuad(MeshKit kit, float from, float to, Vector3 axis, List<Vector3> positions, int a, int b, int c, int d)
        {
            if ((from < 0f) == (to < 0f) || a < 0 || b < 0 || c < 0 || d < 0)
                return;

            Vector3 pa = positions[a], pb = positions[b], pc = positions[c], pd = positions[d];
            // Наружу — от «внутри» (отрицательное расстояние) к «снаружи». Обход разворачиваем так,
            // чтобы нормаль cross(b - a, c - a) смотрела наружу (лицевая сторона в Unity — по часовой).
            Vector3 outward = from < 0f ? axis : -axis;
            if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), outward) < 0f)
                (pb, pd) = (pd, pb);

            kit.Triangle(pa, pb, pc, SwatchAt((pa + pb + pc) / 3f));
            kit.Triangle(pa, pc, pd, SwatchAt((pa + pc + pd) / 3f));
        }

        private Swatch SwatchAt(Vector3 point)
        {
            Distance(point, out var swatch);
            return swatch;
        }

        /// <summary>Шаг Ньютона по численному градиенту: подтягивает вершину точно на поверхность.</summary>
        private Vector3 ProjectToSurface(Vector3 point, float cell)
        {
            float h = cell * 0.1f;
            for (int step = 0; step < 2; step++)
            {
                float d = Distance(point, out _);
                var gradient = new Vector3(
                    Distance(point + Vector3.right * h, out _) - Distance(point - Vector3.right * h, out _),
                    Distance(point + Vector3.up * h, out _) - Distance(point - Vector3.up * h, out _),
                    Distance(point + Vector3.forward * h, out _) - Distance(point - Vector3.forward * h, out _)) / (2f * h);
                float length2 = gradient.sqrMagnitude;
                if (length2 < 1e-8f)
                    break;
                Vector3 move = gradient * (d / length2);
                // Не уводим вершину дальше половины ячейки: иначе на тонких деталях сетка может перекрутиться.
                point -= Vector3.ClampMagnitude(move, cell * 0.5f);
            }
            return point;
        }

        private static Vector3 GridPoint(Vector3 min, float cell, int i, int j, int k) =>
            min + new Vector3(i * cell, j * cell, k * cell);

        /// <summary>Двенадцать рёбер единичного куба как пары углов.</summary>
        private static (Vector3Int, Vector3Int) CubeEdge(int edge) => edge switch
        {
            0 => (new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0)),
            1 => (new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0)),
            2 => (new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1)),
            3 => (new Vector3Int(0, 1, 1), new Vector3Int(1, 1, 1)),
            4 => (new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0)),
            5 => (new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0)),
            6 => (new Vector3Int(0, 0, 1), new Vector3Int(0, 1, 1)),
            7 => (new Vector3Int(1, 0, 1), new Vector3Int(1, 1, 1)),
            8 => (new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1)),
            9 => (new Vector3Int(1, 0, 0), new Vector3Int(1, 0, 1)),
            10 => (new Vector3Int(0, 1, 0), new Vector3Int(0, 1, 1)),
            _ => (new Vector3Int(1, 1, 0), new Vector3Int(1, 1, 1)),
        };
    }
}
