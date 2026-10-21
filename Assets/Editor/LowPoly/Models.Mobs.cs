using UnityEngine;

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

        /// <summary>Глаза лягушки относительно корня — отдельный меш, чтобы моргать.</summary>
        public static readonly Vector3 FrogEyes = new(0f, 0.86f, 0.32f);

        // Оливково-жёлтая кожа, светлое брюхо и горло, тёмные пятна на спине (по концепт-арту).
        private static Swatch FrogZone(Vector3 p)
        {
            if (p.y < 0.36f || (p.z > 0.45f && p.y < 0.52f))
                return Swatch.FrogBelly;
            float spots = Mathf.Sin(p.x * 11f + 1f) * Mathf.Sin(p.z * 9f) * Mathf.Sin(p.y * 7f + 2f);
            return p.y > 0.6f && spots > 0.35f ? Swatch.FrogWart : Swatch.FrogSkin;
        }

        /// <summary>Очень толстая лягушка: огромное тело-мешок, крошечные лапки, широкий рот, светящиеся бородавки.</summary>
        public static void FrogBody(MeshKit k)
        {
            var sdf = new Sdf();
            sdf.Ellipsoid(new Vector3(0f, 0.46f, -0.05f), new Vector3(0.58f, 0.46f, 0.62f), Quaternion.identity, 0f, FrogZone);
            sdf.Ellipsoid(new Vector3(0f, 0.62f, 0.32f), new Vector3(0.5f, 0.32f, 0.4f), Quaternion.identity, 0.25f, FrogZone);
            foreach (float side in new[] { -1f, 1f })
            {
                // Крошечные передние лапки и сложенные задние.
                sdf.RoundCone(new Vector3(0.3f * side, 0.28f, 0.42f), new Vector3(0.36f * side, 0.03f, 0.55f), 0.07f, 0.05f, 0.05f, _ => Swatch.FrogSkin);
                sdf.Ellipsoid(new Vector3(0.38f * side, 0.06f, 0.6f), new Vector3(0.1f, 0.035f, 0.08f), 0.03f, Swatch.FrogBelly);
                sdf.Ellipsoid(new Vector3(0.5f * side, 0.2f, -0.25f), new Vector3(0.18f, 0.16f, 0.32f), Quaternion.identity, 0.08f, _ => Swatch.FrogSkin);
                sdf.Ellipsoid(new Vector3(0.55f * side, 0.03f, -0.05f), new Vector3(0.13f, 0.035f, 0.16f), 0.03f, Swatch.FrogBelly);
            }
            // Рот — широкая недовольная дуга через всю морду (уголки опущены).
            for (int i = 0; i < 9; i++)
            {
                float x = Mathf.Lerp(-0.4f, 0.4f, i / 8f);
                float y = 0.53f - 0.06f * (x / 0.4f) * (x / 0.4f);
                float z = 0.7f - 0.25f * (x / 0.4f) * (x / 0.4f);
                sdf.Carve(new Vector3(x, y, z), new Vector3(0.07f, 0.018f, 0.1f), 0.01f, Swatch.Throat);
            }
            // Бородавки — бугорки темнее кожи.
            foreach (var wart in new[] { new Vector3(0.3f, 0.86f, -0.1f), new Vector3(-0.22f, 0.9f, -0.25f), new Vector3(0.05f, 0.92f, 0.05f), new Vector3(-0.42f, 0.6f, -0.35f), new Vector3(0.45f, 0.55f, -0.3f), new Vector3(-0.1f, 0.8f, -0.5f) })
                sdf.Ellipsoid(wart, Vector3.one * 0.065f, 0.03f, Swatch.FrogWart);
            sdf.Polygonize(k, new Vector3(-0.85f, -0.05f, -0.8f), new Vector3(0.85f, 1.05f, 0.95f), 0.03f);
        }

        /// <summary>Глаза навыкате по бокам головы, начало координат — центр между глазами (FrogEyes).</summary>
        public static void FrogEyeBalls(MeshKit k)
        {
            var sdf = new Sdf();
            foreach (float side in new[] { -1f, 1f })
            {
                // Жёлтые выпученные глаза с тёмным ободком и маленьким зрачком, тяжёлое веко сверху.
                sdf.Ellipsoid(new Vector3(0.38f * side, 0f, 0f), new Vector3(0.2f, 0.2f, 0.2f), 0f, Swatch.FrogEye);
                sdf.Ellipsoid(new Vector3(0.52f * side, 0.01f, 0.06f), new Vector3(0.07f, 0.09f, 0.08f), 0.005f, Swatch.HazardBlack);
                sdf.Ellipsoid(new Vector3(0.36f * side, 0.15f, 0.0f), new Vector3(0.19f, 0.07f, 0.17f), Quaternion.Euler(0f, 0f, -12f * side), 0.02f, _ => Swatch.FrogSkin);
            }
            sdf.Polygonize(k, new Vector3(-0.65f, -0.25f, -0.25f), new Vector3(0.65f, 0.25f, 0.3f), 0.025f);
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
