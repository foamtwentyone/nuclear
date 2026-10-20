using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nuclear.EditorTools.LowPoly
{
    /// <summary>Коробка-коллайдер в пространстве модели: центр, размер и поворот.</summary>
    public readonly struct BoxShape
    {
        public readonly Vector3 Center;
        public readonly Vector3 Size;
        public readonly Quaternion Rotation;

        public BoxShape(Vector3 center, Vector3 size, Quaternion rotation)
        {
            Center = center;
            Size = size;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// Сборщик low-poly мешей. У каждого треугольника свои вершины, поэтому затенение плоское,
    /// а цвет задаётся UV-клеткой палитры. Поддерживает стек преобразований, как в старом OpenGL:
    /// можно собрать деталь в локальных координатах и поставить её куда нужно.
    /// Порядок вершин — по часовой стрелке, если смотреть на лицевую сторону (так принято в Unity).
    /// </summary>
    public sealed class MeshKit
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int> triangles = new();
        private readonly Stack<Matrix4x4> stack = new();
        private Matrix4x4 matrix = Matrix4x4.identity;

        /// <summary>Коллайдеры, отмеченные при сборке, уже в пространстве модели.</summary>
        public readonly List<BoxShape> Colliders = new();

        public int TriangleCount => triangles.Count / 3;

        public void Push(Vector3 position, Quaternion rotation, float scale = 1f)
        {
            stack.Push(matrix);
            matrix *= Matrix4x4.TRS(position, rotation, Vector3.one * scale);
        }

        public void Push(Vector3 position, Vector3 euler) => Push(position, Quaternion.Euler(euler));

        public void Pop() => matrix = stack.Pop();

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Swatch swatch)
        {
            Vector2 uv = Palette.UV(swatch);
            int start = vertices.Count;
            vertices.Add(matrix.MultiplyPoint3x4(a));
            vertices.Add(matrix.MultiplyPoint3x4(b));
            vertices.Add(matrix.MultiplyPoint3x4(c));
            uvs.Add(uv);
            uvs.Add(uv);
            uvs.Add(uv);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        /// <summary>Четырёхугольник a-b-c-d по часовой стрелке со стороны лицевой грани.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Swatch swatch)
        {
            Triangle(a, b, c, swatch);
            Triangle(a, c, d, swatch);
        }

        /// <summary>Двусторонний четырёхугольник: для тонких листов (крыша, ткань).</summary>
        public void Sheet(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Swatch swatch)
        {
            Quad(a, b, c, d, swatch);
            Quad(d, c, b, a, swatch);
        }

        public void Box(Vector3 center, Vector3 size, Swatch swatch, bool collider = false) =>
            Box(center, size, Quaternion.identity, swatch, swatch, collider);

        public void Box(Vector3 center, Vector3 size, Quaternion rotation, Swatch swatch, bool collider = false) =>
            Box(center, size, rotation, swatch, swatch, collider);

        /// <summary>Коробка с отдельным цветом верха (например, ржавая крыша или пыльная крышка).</summary>
        public void Box(Vector3 center, Vector3 size, Quaternion rotation, Swatch sides, Swatch top, bool collider = false)
        {
            Vector3 h = size * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            EmitBox(center, rotation, corners, sides, top);

            if (collider)
                AddCollider(center, size, rotation);
        }

        /// <summary>
        /// Коробка со случайно сдвинутыми углами — для обломков, камней и неровных стен.
        /// Углы общие у соседних граней, поэтому форма остаётся замкнутой.
        /// </summary>
        public void RoughBox(Vector3 center, Vector3 size, Quaternion rotation, Swatch sides, Swatch top, float jitter, System.Random random)
        {
            Vector3 h = size * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                corners[i] = corner + new Vector3(Range(random, -jitter, jitter), Range(random, -jitter, jitter), Range(random, -jitter, jitter));
            }
            EmitBox(center, rotation, corners, sides, top);
        }

        /// <summary>Усечённая пирамида: низ bottom×bottom, верх top×top, верх можно сдвинуть по Z (кабина машины).</summary>
        public void Frustum(Vector3 baseCenter, Vector2 bottom, Vector2 top, float height, Swatch sides, Swatch topSwatch, float topShiftZ = 0f)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                bool upper = (i & 2) != 0;
                Vector2 extent = (upper ? top : bottom) * 0.5f;
                corners[i] = new Vector3(
                    (i & 1) == 0 ? -extent.x : extent.x,
                    upper ? height : 0f,
                    ((i & 4) == 0 ? -extent.y : extent.y) + (upper ? topShiftZ : 0f));
            }
            EmitBox(baseCenter, Quaternion.identity, corners, sides, topSwatch);
        }

        /// <summary>
        /// Цилиндр вдоль локальной оси Y от base вверх. topRadius = 0 даёт конус.
        /// </summary>
        public void Cylinder(Vector3 baseCenter, Quaternion rotation, float radius, float topRadius, float height, int sides, Swatch swatch, Swatch caps)
        {
            Push(baseCenter, rotation);

            var bottomRing = new Vector3[sides];
            var topRing = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bottomRing[i] = direction * radius;
                topRing[i] = direction * topRadius + Vector3.up * height;
            }

            var apex = Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                if (topRadius <= 0f)
                    Triangle(bottomRing[i], apex, bottomRing[next], swatch);
                else
                    Quad(bottomRing[i], topRing[i], topRing[next], bottomRing[next], swatch);

                if (topRadius > 0f)
                    Triangle(apex, topRing[next], topRing[i], caps);
                Triangle(Vector3.zero, bottomRing[i], bottomRing[next], caps);
            }

            Pop();
        }

        /// <summary>Цилиндр между двумя точками — для веток, арматуры, проводов, конечностей.</summary>
        public void Limb(Vector3 from, Vector3 to, float radius, float topRadius, int sides, Swatch swatch)
        {
            Vector3 axis = to - from;
            if (axis.sqrMagnitude < 1e-6f)
                return;
            var rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            Cylinder(from, rotation, radius, topRadius, axis.magnitude, sides, swatch, swatch);
        }

        /// <summary>Отмечает коллайдер-коробку в текущем пространстве (учитывает стек преобразований).</summary>
        public void AddCollider(Vector3 center, Vector3 size, Quaternion rotation)
        {
            var world = matrix * Matrix4x4.TRS(center, rotation, Vector3.one);
            Vector3 scale = matrix.lossyScale;
            Colliders.Add(new BoxShape(world.MultiplyPoint3x4(Vector3.zero), Vector3.Scale(size, scale), world.rotation));
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);

        /// <summary>
        /// Шесть граней коробки по восьми углам. Индекс угла: бит 0 — +X, бит 1 — +Y, бит 2 — +Z.
        /// Порядок обхода проверен: нормаль cross(b - a, c - a) смотрит наружу.
        /// </summary>
        private void EmitBox(Vector3 center, Quaternion rotation, Vector3[] c, Swatch sides, Swatch top)
        {
            Push(center, rotation);
            Quad(c[0], c[2], c[3], c[1], sides); // -Z
            Quad(c[5], c[7], c[6], c[4], sides); // +Z
            Quad(c[4], c[6], c[2], c[0], sides); // -X
            Quad(c[1], c[3], c[7], c[5], sides); // +X
            Quad(c[2], c[6], c[7], c[3], top);   // +Y
            Quad(c[1], c[5], c[4], c[0], sides); // -Y
            Pop();
        }
    }
}
