using UnityEngine;
using static Nuclear.EditorTools.LowPoly.MeshKit;

namespace Nuclear.EditorTools.LowPoly
{
    /// <summary>Мобы перехода 1, вылепленные мягкими формами, как Слепыш: Сом, Лягушка, Рот.</summary>
    public static partial class Models
    {
        // ---------- Сом ----------

        /// <summary>Основания усов Сома (у морды) относительно корня; усы рисуются на ходу (CatfishAnimator).</summary>
        public static readonly Vector3[] CatfishWhiskerRoots =
        {
            new(-0.42f, 0.2f, 1.3f), new(-0.22f, 0.16f, 1.42f), new(0.22f, 0.16f, 1.42f), new(0.42f, 0.2f, 1.3f),
        };

        // Тёмно-синяя спина, светлое серо-голубое брюхо (по концепт-арту), граница волнистая.
        private static Swatch CatfishZone(Vector3 p) => p.y < 0.24f + 0.05f * Mathf.Sin(p.z * 5f) ? Swatch.CatfishBelly : Swatch.CatfishBack;

        /// <summary>Толстая рыба, ползающая по земле: широкая плоская голова с огромным ртом, жирное тело, хвост с плавником.</summary>
        public static void CatfishBody(MeshKit k)
        {
            var sdf = new Sdf();
            sdf.Ellipsoid(new Vector3(0f, 0.36f, 0f), new Vector3(0.62f, 0.4f, 1.25f), Quaternion.identity, 0f, CatfishZone);
            // Голова шире тела и приплюснута.
            sdf.Ellipsoid(new Vector3(0f, 0.32f, 0.85f), new Vector3(0.75f, 0.31f, 0.55f), Quaternion.identity, 0.25f, CatfishZone);
            sdf.RoundCone(new Vector3(0f, 0.34f, -0.95f), new Vector3(0f, 0.38f, -1.95f), 0.32f, 0.08f, 0.25f, CatfishZone);
            // Хвостовой, спинной и грудные плавники.
            sdf.Ellipsoid(new Vector3(0f, 0.42f, -2.1f), new Vector3(0.05f, 0.4f, 0.34f), 0.06f, Swatch.CatfishFin);
            sdf.Ellipsoid(new Vector3(0f, 0.76f, -0.1f), new Vector3(0.04f, 0.24f, 0.4f), Quaternion.Euler(-12f, 0f, 0f), 0.06f, _ => Swatch.CatfishFin);
            foreach (float side in new[] { -1f, 1f })
            {
                sdf.Ellipsoid(new Vector3(0.68f * side, 0.14f, 0.5f), new Vector3(0.38f, 0.045f, 0.24f), Quaternion.Euler(0f, -25f * side, 12f * side), 0.06f, _ => Swatch.CatfishFin);
                // Крошечные глазки-бусинки.
                sdf.Ellipsoid(new Vector3(0.46f * side, 0.5f, 1.05f), new Vector3(0.055f, 0.055f, 0.05f), 0.01f, Swatch.NoseDark);
            }
            // Огромный рот во всю ширину морды, розовые губы.
            sdf.Ellipsoid(new Vector3(0f, 0.21f, 1.33f), new Vector3(0.52f, 0.11f, 0.16f), 0.04f, Swatch.CatfishFin);
            sdf.Carve(new Vector3(0f, 0.2f, 1.42f), new Vector3(0.47f, 0.06f, 0.16f), 0.03f, Swatch.Throat);
            // Розовые шипики-наросты вдоль спины и пятна.
            for (int i = 0; i < 3; i++)
            {
                var spine = new Vector3(0f, 0.78f, 0.55f - i * 0.28f);
                sdf.RoundCone(spine, spine + new Vector3(0f, 0.12f, -0.06f), 0.05f, 0.015f, 0.03f, Swatch.CatfishFin);
            }
            sdf.Ellipsoid(new Vector3(0.38f, 0.62f, -0.6f), new Vector3(0.12f, 0.06f, 0.16f), 0.03f, Swatch.CatfishFin);
            sdf.Ellipsoid(new Vector3(-0.3f, 0.66f, 0.15f), new Vector3(0.1f, 0.05f, 0.12f), 0.03f, Swatch.CatfishBelly);
            sdf.Polygonize(k, new Vector3(-1.2f, -0.1f, -2.5f), new Vector3(1.2f, 1.1f, 1.7f), 0.045f);
        }

        /// <summary>Отрезок уса: цилиндр длиной 1 вдоль +Z (растягивается и поворачивается на ходу).</summary>
        public static void WhiskerSegment(MeshKit k)
        {
            k.Cylinder(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), 0.035f, 0.03f, 1f, 6, Swatch.CatfishFin, Swatch.CatfishFin);
        }

        // ---------- Лягушка ----------
        // По референсу (три вида): почти шар, приземистая, огромное светлое брюхо, толстые бёдра по бокам,
        // короткие лапы с растопыренными пальцами, выпученные жёлтые глаза в тёмных ободках сверху спереди,
        // широкие оранжевые губы уголками вниз, бородавки по спине и бокам, кожа в камуфляжных пятнах.

        /// <summary>Глаза лягушки относительно корня — отдельный меш, чтобы моргать.</summary>
        public static readonly Vector3 FrogEyes = new(0f, 0.86f, 0.44f);

        // Тело — почти шар: центр и радиусы (по ним же сажаются глаза, губы и бородавки).
        private static readonly Vector3 FrogCenter = new(0f, 0.58f, 0f);
        private static readonly Vector3 FrogRadii = new(0.63f, 0.58f, 0.6f);

        /// <summary>Точка на поверхности тела спереди на высоте y и смещении x (для губ).</summary>
        private static Vector3 FrogFront(float x, float y, float lift)
        {
            float v = (y - FrogCenter.y) / FrogRadii.y;
            float horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - v * v));
            float u = x / (FrogRadii.x * horizontal);
            float z = FrogRadii.z * horizontal * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
            return new Vector3(x, y, z + lift);
        }

        /// <summary>Плавный шум 0..1 в пространстве (для камуфляжных пятен).</summary>
        private static float Noise3(Vector3 p)
        {
            static float Hash(int x, int y, int z)
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float Lerp(float a, float b, float t) => a + (b - a) * t;
            float x00 = Lerp(Hash(ix, iy, iz), Hash(ix + 1, iy, iz), fx), x10 = Lerp(Hash(ix, iy + 1, iz), Hash(ix + 1, iy + 1, iz), fx);
            float x01 = Lerp(Hash(ix, iy, iz + 1), Hash(ix + 1, iy, iz + 1), fx), x11 = Lerp(Hash(ix, iy + 1, iz + 1), Hash(ix + 1, iy + 1, iz + 1), fx);
            return Lerp(Lerp(x00, x10, fy), Lerp(x01, x11, fy), fz);
        }

        private static float Camo(Vector3 p) => Noise3(p * 7f) * 0.65f + Noise3(p * 15f + Vector3.one * 7f) * 0.35f;

        /// <summary>Брюхо: большое светлое пятно спереди-снизу с волнистым краем.</summary>
        private static bool FrogBellyArea(Vector3 p)
        {
            var c = new Vector3(0f, 0.36f, 0.6f);
            var d = new Vector3(p.x / 0.5f, (p.y - c.y) / 0.34f, (p.z - c.z) / 0.55f);
            return p.z > 0.12f && d.magnitude < 1f + 0.08f * Mathf.Sin(p.x * 18f + p.y * 9f);
        }

        private static Swatch FrogZone(Vector3 p)
        {
            float camo = Camo(p);
            if (FrogBellyArea(p))
                return camo > 0.7f ? Swatch.FrogSkinLight : camo < 0.27f ? Swatch.FrogSkin : Swatch.FrogBelly;
            return camo > 0.68f ? Swatch.FrogSkinLight : camo < 0.3f ? Swatch.FrogWart : Swatch.FrogSkin;
        }

        public static void FrogBody(MeshKit k)
        {
            var sdf = new Sdf();
            // Почти шар без шеи; брюхо выпирает спереди снизу.
            sdf.Ellipsoid(FrogCenter, FrogRadii, Quaternion.identity, 0f, FrogZone);
            sdf.Ellipsoid(new Vector3(0f, 0.38f, 0.22f), new Vector3(0.56f, 0.38f, 0.44f), Quaternion.identity, 0.12f, FrogZone);

            foreach (float side in new[] { -1f, 1f })
            {
                // Бедро — большой круглый бугор сзади-снизу по боку, колено вперёд, стопа под ним.
                sdf.Ellipsoid(new Vector3(0.53f * side, 0.27f, 0.02f), new Vector3(0.21f, 0.25f, 0.28f), Quaternion.Euler(-15f, 0f, -6f * side), 0.06f, FrogZone);
                sdf.Ellipsoid(new Vector3(0.56f * side, 0.11f, 0.2f), new Vector3(0.14f, 0.11f, 0.15f), Quaternion.identity, 0.06f, FrogZone);
                Foot(sdf, new Vector3(0.6f * side, 0.025f, 0.32f), side, 1f);
                // Короткие толстые передние лапы по углам брюха.
                sdf.RoundCone(new Vector3(0.4f * side, 0.28f, 0.44f), new Vector3(0.44f * side, 0.07f, 0.55f), 0.15f, 0.11f, 0.08f, FrogZone);
                Foot(sdf, new Vector3(0.46f * side, 0.025f, 0.64f), side, 0.95f);

                // Глаз сидит в «чашечке» с тонким тёмным ободком (сам глаз — отдельный меш).
                var eye = FrogEyes + new Vector3(0.28f * side, 0f, 0.02f);
                sdf.Ellipsoid(eye + new Vector3(0f, -0.01f, -0.035f), new Vector3(0.136f, 0.134f, 0.125f), Quaternion.identity, 0.06f,
                    p => (p - eye).magnitude < 0.155f ? Swatch.FrogEyeRing : FrogZone(p));
            }

            // Широкие оранжевые губы по поверхности морды, уголками вниз.
            Vector3 previous = default;
            for (int i = 0; i <= 14; i++)
            {
                float x = Mathf.Lerp(-0.4f, 0.4f, i / 14f);
                float u = x / 0.4f;
                var point = FrogFront(x, 0.71f - 0.07f * u * u, 0.005f);
                if (i > 0)
                    sdf.RoundCone(previous, point, 0.02f, 0.02f, 0.01f, Swatch.FrogLip);
                previous = point;
            }

            // Бородавки: гребень над глазами и густая россыпь по спине и бокам, коричневые и жёлто-оливковые.
            var random = new System.Random(41);
            for (int i = 0; i < 13; i++)
            {
                float x = Mathf.Lerp(-0.5f, 0.5f, i / 12f);
                var top = FrogCenter + new Vector3(x, FrogRadii.y * Mathf.Sqrt(Mathf.Max(0f, 1f - x * x / 0.4f)) * 0.98f, 0.12f);
                sdf.Ellipsoid(top, Vector3.one * Range(random, 0.045f, 0.065f), 0.02f, i % 2 == 0 ? Swatch.FrogWartBrown : Swatch.FrogSkinLight);
            }
            var placed = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < 400 && placed.Count < 70; i++)
            {
                float theta = Range(random, 0f, Mathf.PI * 2f);
                float phi = Range(random, -0.25f, 1.3f);
                var dir = new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(phi), Mathf.Sin(theta) * Mathf.Cos(phi));
                var point = FrogCenter + Vector3.Scale(dir, FrogRadii) * 0.985f;
                // Брюхо и морда гладкие.
                if (FrogBellyArea(point) || (point.z > 0.3f && point.y > 0.6f && Mathf.Abs(point.x) < 0.45f))
                    continue;
                // Ровная россыпь: не ближе 12 см друг к другу, без кучек.
                if (placed.Exists(other => (other - point).sqrMagnitude < 0.0144f))
                    continue;
                placed.Add(point);
                bool brown = random.NextDouble() < 0.4;
                sdf.Ellipsoid(point, Vector3.one * Range(random, 0.04f, 0.07f), 0.015f, brown ? Swatch.FrogWartBrown : Swatch.FrogSkinLight);
            }

            sdf.Polygonize(k, new Vector3(-0.92f, -0.03f, -0.75f), new Vector3(0.92f, 1.25f, 0.9f), 0.025f);
        }

        /// <summary>Стопа: подушечка и четыре растопыренных пальца с коричневыми кончиками.</summary>
        private static void Foot(Sdf sdf, Vector3 center, float side, float scale)
        {
            sdf.Ellipsoid(center + new Vector3(0f, 0.02f, 0f), new Vector3(0.1f, 0.04f, 0.08f) * scale, 0.04f, Swatch.FrogSkin);
            for (int toe = 0; toe < 4; toe++)
            {
                float angle = Mathf.Lerp(-55f, 35f, toe / 3f) * side;
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var tip = center + direction * 0.15f * scale;
                sdf.RoundCone(center, tip, 0.03f * scale, 0.022f * scale, 0.02f, Swatch.FrogSkin);
                sdf.Ellipsoid(tip + Vector3.up * 0.005f, Vector3.one * 0.03f * scale, 0.008f, Swatch.FrogToe);
            }
        }

        /// <summary>Выпученные жёлтые глаза с маленькими чёрными зрачками; начало координат — FrogEyes.</summary>
        public static void FrogEyeBalls(MeshKit k)
        {
            var sdf = new Sdf();
            foreach (float side in new[] { -1f, 1f })
            {
                var eye = new Vector3(0.28f * side, 0f, 0.02f);
                sdf.Ellipsoid(eye, new Vector3(0.125f, 0.125f, 0.12f), 0f, Swatch.FrogEye);
                // Зрачок смотрит вперёд и чуть к центру — взгляд «в упор».
                sdf.Ellipsoid(eye + new Vector3(-0.02f * side, 0.012f, 0.116f), new Vector3(0.02f, 0.024f, 0.013f), 0.004f, Swatch.HazardBlack);
            }
            sdf.Polygonize(k, new Vector3(-0.5f, -0.2f, -0.2f), new Vector3(0.5f, 0.2f, 0.25f), 0.018f);
        }

        // ---------- Рот ----------

        /// <summary>Петля верхней челюсти Рта относительно корня.</summary>
        public static readonly Vector3 MouthHinge = new(0f, 0.5f, -0.42f);

        // Бурая шерсть с пятнами посветлее (по концепт-арту) — на земле Рот похож на кочку.
        private static Swatch Camouflage(Vector3 p) => Mathf.PerlinNoise(p.x * 5f + 3f, p.z * 5f + p.y * 3f) > 0.62f ? Swatch.MouthFurLight : Swatch.MouthFur;

        /// <summary>Нижняя половина Рта: круглый «камень», внутри — пасть с языком и нижними зубами.</summary>
        public static void MouthLower(MeshKit k)
        {
            var sdf = new Sdf();
            sdf.Ellipsoid(new Vector3(0f, 0.3f, 0f), new Vector3(0.58f, 0.32f, 0.52f), Quaternion.identity, 0f, Camouflage);
            // Толстые розовые губы по краю пасти.
            sdf.Ellipsoid(new Vector3(0f, 0.5f, 0.04f), new Vector3(0.52f, 0.06f, 0.47f), 0.03f, Swatch.Lips);
            sdf.Carve(new Vector3(0f, 0.52f, 0.04f), new Vector3(0.45f, 0.22f, 0.4f), 0.04f, Swatch.Throat);
            sdf.Ellipsoid(new Vector3(0f, 0.36f, 0.08f), new Vector3(0.24f, 0.07f, 0.3f), 0.04f, Swatch.Gums);
            // Короткие толстые лапки.
            foreach (var leg in new[] { new Vector3(0.3f, 0.08f, 0.25f), new Vector3(-0.3f, 0.08f, 0.25f), new Vector3(0.3f, 0.08f, -0.25f), new Vector3(-0.3f, 0.08f, -0.25f) })
                sdf.RoundCone(leg + Vector3.up * 0.12f, new Vector3(leg.x * 1.1f, 0.03f, leg.z * 1.1f), 0.09f, 0.07f, 0.06f, _ => Swatch.MouthFur);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-150f, -30f, i / 8f) * Mathf.Deg2Rad;
                var tooth = new Vector3(Mathf.Cos(a) * 0.44f, 0.5f, -Mathf.Sin(a) * 0.38f + 0.04f);
                sdf.RoundCone(tooth, tooth + new Vector3(0f, 0.1f, 0f), 0.035f, 0.008f, 0.01f, Swatch.Teeth);
            }
            sdf.Polygonize(k, new Vector3(-0.7f, -0.05f, -0.65f), new Vector3(0.7f, 0.75f, 0.65f), 0.025f);
        }

        /// <summary>Верхняя челюсть (начало координат — петля MouthHinge): купол-«камень» с верхними зубами и глазками.</summary>
        public static void MouthUpper(MeshKit k)
        {
            var sdf = new Sdf();
            var center = new Vector3(0f, 0.02f, 0.44f);
            sdf.Ellipsoid(center, new Vector3(0.58f, 0.3f, 0.52f), Quaternion.identity, 0f, p => Camouflage(p + MouthHinge));
            sdf.Ellipsoid(center + new Vector3(0f, -0.04f, 0.02f), new Vector3(0.53f, 0.06f, 0.48f), 0.03f, Swatch.Lips);
            sdf.Carve(center + new Vector3(0f, -0.14f, 0.02f), new Vector3(0.48f, 0.2f, 0.43f), 0.04f, Swatch.Throat);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-150f, -30f, i / 8f) * Mathf.Deg2Rad;
                var tooth = center + new Vector3(Mathf.Cos(a) * 0.45f, -0.03f, -Mathf.Sin(a) * 0.39f + 0.03f);
                sdf.RoundCone(tooth, tooth + new Vector3(0f, -0.11f, 0f), 0.035f, 0.008f, 0.01f, Swatch.Teeth);
            }
            // Глазки почти заросли.
            foreach (float side in new[] { -1f, 1f })
                sdf.Ellipsoid(center + new Vector3(0.16f * side, 0.27f, 0.25f), new Vector3(0.045f, 0.03f, 0.04f), 0.01f, Swatch.NoseDark);
            // Розовая болячка на макушке.
            sdf.Ellipsoid(center + new Vector3(0.18f, 0.27f, -0.1f), new Vector3(0.1f, 0.04f, 0.08f), 0.03f, Swatch.Lips);
            sdf.Polygonize(k, new Vector3(-0.7f, -0.35f, -0.15f), new Vector3(0.7f, 0.45f, 1.05f), 0.025f);
        }
    }
}
