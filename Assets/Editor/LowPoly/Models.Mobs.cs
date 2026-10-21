using UnityEngine;
using static Nuclear.EditorTools.LowPoly.MeshKit;

namespace Nuclear.EditorTools.LowPoly
{
    /// <summary>Мобы перехода 1, вылепленные мягкими формами, как Слепыш: Сом и Рот; у Лягушки — веки к готовой модели.</summary>
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
        // Тело — готовая модель (Assets/Art/Models/Frog, см. Tools/bake_model.py), глаза на ней нарисованы.
        // Моргают отдельные веки: полусферы чуть больше глазного яблока, поворачиваются вокруг его центра.

        /// <summary>Центр левого глаза на модели лягушки (правый — зеркально по X) и куда он смотрит.</summary>
        public static readonly Vector3 FrogEye = new(-0.25f, 0.76f, 0.43f);
        public static readonly Vector3 FrogGaze = new(-0.35f, 0.3f, 0.89f);

        /// <summary>
        /// Веко: толстая полусфера выпуклостью к +Z, край в плоскости XY через центр глаза.
        /// Закрыто — смотрит вдоль взгляда; открыто — повёрнуто вверх-назад и прячется под кожей.
        /// </summary>
        public static void FrogLid(MeshKit k)
        {
            const float outer = 0.098f, inner = 0.088f;
            const int rings = 6, sides = 14;
            Vector3 Point(float radius, int ring, int side)
            {
                float polar = Mathf.PI * 0.5f * ring / rings;
                float azimuth = Mathf.PI * 2f * side / sides;
                return radius * new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Sin(polar) * Mathf.Sin(azimuth), Mathf.Cos(polar));
            }
            for (int ring = 0; ring < rings; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    k.Quad(Point(outer, ring, next), Point(outer, ring, side), Point(outer, ring + 1, side), Point(outer, ring + 1, next), Swatch.FrogSkin);
                    k.Quad(Point(inner, ring, side), Point(inner, ring, next), Point(inner, ring + 1, next), Point(inner, ring + 1, side), Swatch.FrogSkin);
                }
            }
            // Кромка века.
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                k.Quad(Point(outer, rings, next), Point(outer, rings, side), Point(inner, rings, side), Point(inner, rings, next), Swatch.FrogEyeRing);
            }
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
