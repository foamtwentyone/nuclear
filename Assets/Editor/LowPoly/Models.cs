using System.Collections.Generic;
using UnityEngine;
using static Nuclear.EditorTools.LowPoly.MeshKit;

namespace Nuclear.EditorTools.LowPoly
{
    /// <summary>
    /// Рецепты low-poly моделей. Каждый метод дописывает геометрию в переданный MeshKit
    /// в своих локальных координатах; ставить модель на место — забота вызывающего.
    /// Все случайности — через переданный System.Random, чтобы сборка была повторяемой.
    /// </summary>
    public static class Models
    {
        // ---------- Персонажи ----------

        /// <summary>
        /// Выживший в противогазе с рюкзаком, из округлых форм (как персонажи Content Warning и Peak).
        /// Опорная точка — центр таза, ступни на -0.9.
        /// </summary>
        public static void Survivor(MeshKit k)
        {
            var random = new System.Random(1);
            Swatch[] Only(Swatch swatch) => new[] { swatch };

            foreach (float side in new[] { -1f, 1f })
            {
                // Ноги и ботинки.
                k.Capsule(new Vector3(0.11f * side, -0.02f, 0f), new Vector3(0.11f * side, -0.76f, 0.02f), 0.085f, Swatch.Trousers, random);
                k.Ellipsoid(new Vector3(0.11f * side, -0.84f, 0.05f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.09f, 0.14f, 0.06f), 4, 8, Only(Swatch.Boots), random);

                // Руки: плечо, локоть, кисть в перчатке.
                var shoulder = new Vector3(0.26f * side, 0.56f, 0f);
                var elbow = new Vector3(0.31f * side, 0.28f, 0.03f);
                var wrist = new Vector3(0.32f * side, 0.04f, 0.07f);
                k.Capsule(shoulder, elbow, 0.068f, Swatch.Jacket, random);
                k.Capsule(elbow, wrist, 0.058f, Swatch.Jacket, random);
                k.Ellipsoid(wrist + new Vector3(0f, -0.06f, 0.01f), Quaternion.identity, new Vector3(0.05f, 0.065f, 0.055f), 4, 8, Only(Swatch.Boots), random);

                // Окуляры маски.
                k.Cylinder(new Vector3(0.055f * side, 0.8f, 0.125f), Quaternion.Euler(90f, 0f, 0f), 0.042f, 0.042f, 0.025f, 8, Swatch.MetalDark, Swatch.Lens);
            }

            // Таз, туловище в куртке, воротник.
            k.Ellipsoid(new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(0.21f, 0.11f, 0.13f), 4, 10, Only(Swatch.JacketDark), random);
            k.Ellipsoid(new Vector3(0f, 0.34f, 0f), Quaternion.identity, new Vector3(0.25f, 0.33f, 0.155f), 6, 12, Only(Swatch.Jacket), random);
            k.Ellipsoid(new Vector3(0f, 0.63f, -0.01f), Quaternion.identity, new Vector3(0.16f, 0.05f, 0.13f), 3, 10, Only(Swatch.JacketDark), random);

            // Рюкзак со скаткой: угловатый, это вещь, а не тело.
            k.Box(new Vector3(0f, 0.36f, -0.22f), new Vector3(0.34f, 0.42f, 0.16f), Swatch.Canvas);
            k.Box(new Vector3(0f, 0.59f, -0.2f), new Vector3(0.32f, 0.06f, 0.18f), Swatch.JacketDark);
            k.Cylinder(new Vector3(-0.2f, 0.66f, -0.22f), Quaternion.Euler(0f, 0f, -90f), 0.07f, 0.07f, 0.4f, 8, Swatch.Canvas, Swatch.Canvas);

            // Голова: капюшон, резиновая маска, фильтр спереди.
            k.Ellipsoid(new Vector3(0f, 0.8f, -0.03f), Quaternion.identity, new Vector3(0.15f, 0.16f, 0.15f), 5, 10, Only(Swatch.JacketDark), random);
            k.Ellipsoid(new Vector3(0f, 0.78f, 0.03f), Quaternion.identity, new Vector3(0.125f, 0.135f, 0.12f), 5, 10, Only(Swatch.Rubber), random);
            k.Cylinder(new Vector3(0f, 0.69f, 0.11f), Quaternion.Euler(90f, 0f, 0f), 0.055f, 0.05f, 0.11f, 8, Swatch.FilterOlive, Swatch.FilterOliveDark);
        }

        // Слепыш — гигантская морская свинка-мутант без глаз, со звёздчатым носом, как у крота-звездоноса.
        // Вылеплен из мягких форм (Sdf) одной цельной тушей, как существа в Peak и Pratfall: голова без шеи
        // перетекает в тело. Отдельно, для анимации (BlindOneAnimator), — нос-звезда и четыре лапы.

        /// <summary>Основание носа относительно корня (нос — дочерний объект тела).</summary>
        public static readonly Vector3 BlindOneNose = new(0f, 1.02f, 1.55f);

        /// <summary>Бёдра и плечи лап: передняя левая, передняя правая, задняя левая, задняя правая.</summary>
        public static readonly Vector3[] BlindOneHips =
        {
            new(-0.48f, 0.5f, 0.6f), new(0.48f, 0.5f, 0.6f), new(-0.52f, 0.5f, -0.85f), new(0.52f, 0.5f, -0.85f),
        };

        /// <summary>Окраска зонами, как у нарисованного персонажа: тёмная спина, рыжие бока, светлое брюхо.</summary>
        private static Swatch FurZone(Vector3 point) =>
            point.y > 1.2f ? Swatch.FurDark : point.y < 0.62f ? Swatch.FurPale : Swatch.Fur;

        /// <summary>Туша с головой: круглое тело, пухлые щёки, уши-лопухи, слепые впадины глаз, резцы, гребень шипов, опухоли.</summary>
        public static void BlindOneBody(MeshKit k)
        {
            var sdf = new Sdf();
            sdf.Ellipsoid(new Vector3(0f, 0.9f, -0.15f), new Vector3(0.85f, 0.78f, 1.25f), Quaternion.identity, 0f, FurZone);
            sdf.Ellipsoid(new Vector3(0f, 0.85f, -0.85f), new Vector3(0.8f, 0.7f, 0.6f), Quaternion.identity, 0.2f, FurZone);
            // Голова без шеи; морда снизу светлая.
            sdf.Ellipsoid(new Vector3(0f, 1f, 0.95f), new Vector3(0.62f, 0.6f, 0.62f), Quaternion.identity, 0.35f,
                p => p.z > 1.25f && p.y < 1.05f ? Swatch.FurPale : FurZone(p));

            foreach (float side in new[] { -1f, 1f })
            {
                sdf.Ellipsoid(new Vector3(0.36f * side, 0.78f, 1.25f), new Vector3(0.3f, 0.26f, 0.3f), 0.15f, Swatch.FurPale);
                sdf.Ellipsoid(new Vector3(0.52f * side, 1.48f, 0.72f), new Vector3(0.25f, 0.06f, 0.21f), Quaternion.Euler(10f, 0f, -38f * side), 0.06f,
                    p => Swatch.NoseDark);
            }

            // Шипастый гребень вдоль спины.
            for (int i = 0; i < 7; i++)
            {
                float z = Mathf.Lerp(-0.95f, 0.5f, i / 6f);
                float along = (z + 0.15f) / 1.25f;
                float top = 0.9f + 0.78f * Mathf.Sqrt(Mathf.Max(0f, 1f - along * along)) - 0.1f;
                float tall = Mathf.Lerp(0.28f, 0.42f, Mathf.Sin(Mathf.PI * i / 6f));
                var baseCenter = new Vector3(0f, top, z);
                sdf.RoundCone(baseCenter, baseCenter + new Vector3(0f, tall, -tall * 0.45f), 0.1f, 0.02f, 0.08f, Swatch.FurDark);
            }

            // Опухоли-наросты.
            sdf.Ellipsoid(new Vector3(0.74f, 0.95f, -0.3f), Vector3.one * 0.2f, 0.08f, Swatch.Skin);
            sdf.Ellipsoid(new Vector3(-0.7f, 0.75f, 0.15f), Vector3.one * 0.15f, 0.07f, Swatch.SkinPale);
            sdf.Ellipsoid(new Vector3(0.52f, 1.3f, -0.9f), Vector3.one * 0.13f, 0.06f, Swatch.NoseDark);

            // Резцы — большие, жёлтые, торчат из-под морды.
            foreach (float side in new[] { -1f, 1f })
                sdf.RoundBox(new Vector3(0.06f * side, 0.5f, 1.53f), new Vector3(0.055f, 0.17f, 0.035f), Quaternion.Euler(-14f, 0f, 0f), 0.025f, 0.01f, Swatch.Teeth);

            // Вместо глаз — заросшие кожей впадины.
            foreach (float side in new[] { -1f, 1f })
                sdf.Carve(new Vector3(0.42f * side, 1.17f, 1.22f), new Vector3(0.11f, 0.08f, 0.1f), 0.05f, Swatch.SkinDark);

            sdf.Polygonize(k, new Vector3(-1.15f, -0.05f, -1.6f), new Vector3(1.15f, 1.9f, 1.75f), 0.05f);
        }

        /// <summary>Нос-звезда: мясистый диск и 22 щупальца по кругу, нижние короче. Опорная точка — основание носа.</summary>
        public static void BlindOneNoseStar(MeshKit k)
        {
            var sdf = new Sdf();
            sdf.Ellipsoid(new Vector3(0f, 0f, 0.06f), new Vector3(0.2f, 0.2f, 0.1f), 0f, Swatch.NosePink);
            const int tentacles = 22;
            for (int i = 0; i < tentacles; i++)
            {
                float angle = 2f * Mathf.PI * (i + 0.5f) / tentacles;
                var outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var root = outward * 0.15f + Vector3.forward * 0.08f;
                float length = Mathf.Lerp(0.2f, 0.3f, (outward.y + 1f) / 2f);
                sdf.RoundCone(root, root + outward * length + Vector3.forward * 0.12f, 0.06f, 0.025f, 0.04f, Swatch.NosePink);
            }
            foreach (float side in new[] { -1f, 1f })
                sdf.Carve(new Vector3(0.07f * side, 0f, 0.16f), new Vector3(0.035f, 0.05f, 0.04f), 0.01f, Swatch.Mouth);

            sdf.Polygonize(k, new Vector3(-0.6f, -0.6f, -0.08f), new Vector3(0.6f, 0.6f, 0.4f), 0.025f);
        }

        /// <summary>Короткая толстая лапа с розовой подушкой и когтями. Опорная точка — бедро; подушка на 0.5 ниже.</summary>
        public static void BlindOneLeg(MeshKit k)
        {
            var sdf = new Sdf();
            sdf.RoundCone(Vector3.zero, new Vector3(0f, -0.34f, 0.06f), 0.2f, 0.15f, 0f, Swatch.Fur);
            sdf.Ellipsoid(new Vector3(0f, -0.42f, 0.1f), new Vector3(0.17f, 0.08f, 0.21f), 0.06f, Swatch.SkinPale);
            foreach (float x in new[] { -0.09f, -0.03f, 0.03f, 0.09f })
                sdf.RoundCone(new Vector3(x, -0.43f, 0.27f), new Vector3(x, -0.47f, 0.37f), 0.03f, 0.012f, 0.01f, Swatch.Claw);

            sdf.Polygonize(k, new Vector3(-0.3f, -0.55f, -0.25f), new Vector3(0.3f, 0.25f, 0.45f), 0.03f);
        }

        // ---------- Предметы ----------

        /// <summary>Автомобильный аккумулятор 0.6×0.4×0.4, центр в начале координат.</summary>
        public static void Battery(MeshKit k)
        {
            k.Box(new Vector3(0f, -0.03f, 0f), new Vector3(0.6f, 0.34f, 0.4f), Swatch.BatteryCase);
            k.Box(new Vector3(0f, 0.16f, 0f), new Vector3(0.62f, 0.06f, 0.42f), Swatch.HazardBlack);
            foreach (float side in new[] { -1f, 1f })
            {
                k.Cylinder(new Vector3(0.2f * side, 0.19f, 0.08f), Quaternion.identity, 0.035f, 0.03f, 0.05f, 8, Swatch.Lead, Swatch.Lead);
                k.Box(new Vector3(0.2f * side, 0.245f, 0.08f), new Vector3(0.05f, 0.012f, 0.05f), side > 0 ? Swatch.SignalRed : Swatch.HazardBlack);
                k.Box(new Vector3(0.12f * side, 0.24f, 0f), new Vector3(0.03f, 0.1f, 0.03f), Swatch.Rubber);
            }
            k.Box(new Vector3(0f, 0.29f, 0f), new Vector3(0.27f, 0.03f, 0.04f), Swatch.Rubber);
            k.Box(new Vector3(0f, -0.02f, -0.201f), new Vector3(0.3f, 0.12f, 0.005f), Swatch.LabelPaper);
            k.Box(new Vector3(0f, -0.07f, -0.203f), new Vector3(0.3f, 0.025f, 0.005f), Swatch.HazardYellow);
        }

        /// <summary>Деревянный ящик с окантовкой, ребро size, центр в начале координат.</summary>
        public static void Crate(MeshKit k, float size)
        {
            float half = size / 2f;
            const float beam = 0.07f;
            float edge = half - beam / 2f;
            k.Box(Vector3.zero, Vector3.one * (size * 0.94f), Swatch.Wood);
            for (int a = -1; a <= 1; a += 2)
            {
                for (int b = -1; b <= 1; b += 2)
                {
                    k.Box(new Vector3(0f, edge * a, edge * b), new Vector3(size, beam, beam), Swatch.WoodDark);
                    k.Box(new Vector3(edge * a, 0f, edge * b), new Vector3(beam, size, beam), Swatch.WoodDark);
                    k.Box(new Vector3(edge * a, edge * b, 0f), new Vector3(beam, beam, size), Swatch.WoodDark);
                }
            }
            // Диагональные распорки на двух гранях и трафаретная наклейка.
            foreach (float side in new[] { -1f, 1f })
                k.Box(new Vector3(0f, 0f, (half - 0.005f) * side), new Vector3(size * 1.25f, 0.06f, 0.02f), Quaternion.Euler(0f, 0f, 45f * side), Swatch.WoodDark);
            k.Box(new Vector3(half - 0.004f, 0.02f, 0f), new Vector3(0.01f, 0.14f, 0.22f), Swatch.LabelPaper);
        }

        /// <summary>Консервная банка, центр в начале координат.</summary>
        public static void Can(MeshKit k)
        {
            k.Cylinder(new Vector3(0f, -0.06f, 0f), Quaternion.identity, 0.045f, 0.045f, 0.12f, 10, Swatch.CanLabel, Swatch.Tin);
            k.Cylinder(new Vector3(0f, -0.062f, 0f), Quaternion.identity, 0.047f, 0.047f, 0.012f, 10, Swatch.Tin, Swatch.Tin);
            k.Cylinder(new Vector3(0f, 0.052f, 0f), Quaternion.identity, 0.047f, 0.047f, 0.012f, 10, Swatch.Tin, Swatch.Tin);
            k.Box(new Vector3(0f, 0f, -0.046f), new Vector3(0.04f, 0.05f, 0.004f), Swatch.LabelPaper);
        }

        /// <summary>Фильтр противогаза: ребристый цилиндр с латунной горловиной, центр в начале координат.</summary>
        public static void Filter(MeshKit k)
        {
            k.Cylinder(new Vector3(0f, -0.05f, 0f), Quaternion.identity, 0.06f, 0.06f, 0.09f, 10, Swatch.FilterOlive, Swatch.FilterOliveDark);
            k.Cylinder(new Vector3(0f, -0.03f, 0f), Quaternion.identity, 0.063f, 0.063f, 0.012f, 10, Swatch.FilterOliveDark, Swatch.FilterOliveDark);
            k.Cylinder(new Vector3(0f, 0.005f, 0f), Quaternion.identity, 0.063f, 0.063f, 0.012f, 10, Swatch.FilterOliveDark, Swatch.FilterOliveDark);
            k.Cylinder(new Vector3(0f, 0.04f, 0f), Quaternion.identity, 0.028f, 0.028f, 0.03f, 8, Swatch.Brass, Swatch.Brass);
        }

        // ---------- Дверь бункера ----------

        /// <summary>Гермостворка 3×2.6 с рёбрами жёсткости и предупреждающей полосой; центр в начале координат.</summary>
        public static void DoorPanel(MeshKit k, System.Random random)
        {
            k.Box(Vector3.zero, new Vector3(3f, 2.6f, 0.25f), Swatch.MetalDark);
            foreach (float y in new[] { -0.95f, -0.3f, 0.4f, 1.1f })
                k.Box(new Vector3(0f, y, -0.14f), new Vector3(2.9f, 0.08f, 0.04f), Swatch.Metal);
            foreach (float x in new[] { -1.2f, 0f, 1.2f })
                k.Box(new Vector3(x, 0.1f, -0.14f), new Vector3(0.08f, 2.4f, 0.04f), Swatch.Metal);
            for (int i = 0; i < 15; i++)
                k.Box(new Vector3(-1.4f + i * 0.2f, -1.15f, -0.135f), new Vector3(0.2f, 0.26f, 0.02f), i % 2 == 0 ? Swatch.HazardYellow : Swatch.HazardBlack);
            for (int i = 0; i < 5; i++)
                k.Box(new Vector3(Range(random, -1.3f, 1.3f), Range(random, -0.6f, 1f), -0.162f), new Vector3(Range(random, 0.04f, 0.1f), Range(random, 0.3f, 0.9f), 0.005f), Swatch.Rust);
        }

        /// <summary>Электрощиток с дверцей и кабель-каналом вверх; центр в начале координат.</summary>
        public static void Socket(MeshKit k)
        {
            k.Box(Vector3.zero, new Vector3(0.5f, 0.7f, 0.2f), Swatch.HazardYellow);
            k.Box(new Vector3(0f, 0f, -0.105f), new Vector3(0.4f, 0.58f, 0.01f), Swatch.Metal);
            k.Box(new Vector3(0.15f, 0f, -0.115f), new Vector3(0.03f, 0.1f, 0.02f), Swatch.HazardBlack);
            k.Box(new Vector3(0f, 0.2f, -0.112f), new Vector3(0.2f, 0.06f, 0.005f), Swatch.SignalRed);
            k.Box(new Vector3(0f, 0.6f, 0.05f), new Vector3(0.05f, 0.5f, 0.05f), Swatch.MetalDark);
        }

        // ---------- Окружение ----------

        /// <summary>Брошенная машина: длина вдоль Z, перед в +Z, опорная точка на земле под центром.</summary>
        public static void Car(MeshKit k, System.Random random, Swatch paint)
        {
            int missingWheel = random.NextDouble() < 0.35 ? random.Next(4) : -1;
            float tiltX = 0f, tiltZ = 0f;
            if (missingWheel >= 0)
            {
                tiltZ = (missingWheel % 2 == 0 ? 1f : -1f) * 4f;
                tiltX = (missingWheel < 2 ? 1f : -1f) * 3f;
            }
            k.Push(new Vector3(0f, missingWheel >= 0 ? -0.08f : 0f, 0f), new Vector3(tiltX, 0f, tiltZ));

            k.Box(new Vector3(0f, 0.55f, 0f), new Vector3(1.76f, 0.55f, 4.2f), paint);
            k.Box(new Vector3(0f, 0.86f, 1.45f), new Vector3(1.7f, 0.08f, 1.2f), Quaternion.Euler(4f, 0f, 0f), paint);
            k.Box(new Vector3(0f, 0.86f, -1.6f), new Vector3(1.7f, 0.08f, 0.9f), paint);
            k.Frustum(new Vector3(0f, 0.82f, -0.2f), new Vector2(1.66f, 2.1f), new Vector2(1.42f, 1.3f), 0.52f, Swatch.Glass, paint, -0.15f);
            k.Box(new Vector3(0f, 1.348f, -0.35f), new Vector3(1.2f, 0.012f, 1.0f), Swatch.AshLight);

            foreach (float end in new[] { -1f, 1f })
            {
                k.Box(new Vector3(0f, 0.42f, 2.13f * end), new Vector3(1.8f, 0.16f, 0.1f), Swatch.MetalDark);
                foreach (float side in new[] { -1f, 1f })
                    k.Box(new Vector3(0.6f * side, 0.65f, 2.11f * end), new Vector3(0.3f, 0.12f, 0.04f), end > 0 ? Swatch.White : Swatch.SignalRed);
            }

            for (int wheel = 0; wheel < 4; wheel++)
            {
                if (wheel == missingWheel)
                    continue;
                float side = wheel % 2 == 0 ? -1f : 1f;
                float along = wheel < 2 ? 1.35f : -1.35f;
                var rotation = Quaternion.Euler(0f, 0f, side < 0 ? 90f : -90f);
                k.Cylinder(new Vector3(0.74f * side, 0.33f, along), rotation, 0.33f, 0.33f, 0.22f, 8, Swatch.Tire, Swatch.MetalDark);
            }

            // Ржавчина на капоте и бортах.
            for (int i = 0; i < 3; i++)
                k.Box(new Vector3(Range(random, -0.6f, 0.6f), 0.905f, Range(random, 0.95f, 1.95f)), new Vector3(Range(random, 0.2f, 0.5f), 0.01f, Range(random, 0.2f, 0.45f)), Swatch.Rust);
            for (int i = 0; i < 4; i++)
            {
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                k.Box(new Vector3(0.885f * side, Range(random, 0.42f, 0.7f), Range(random, -1.6f, 1.6f)), new Vector3(0.01f, Range(random, 0.12f, 0.3f), Range(random, 0.3f, 0.8f)), Swatch.Rust);
            }

            k.Pop();
            k.AddCollider(new Vector3(0f, 0.72f, 0f), new Vector3(1.8f, 1.3f, 4.25f), Quaternion.identity);
        }

        /// <summary>Параметры дачного домика.</summary>
        public struct HouseOptions
        {
            public float Width;
            public float Depth;
            public float WallHeight;
            public bool OpenFront;
            public bool Windows;
        }

        /// <summary>
        /// Разрушенный дачный домик из досок: дверной проём спереди (-Z), окна по бокам и сзади,
        /// двускатная жестяная крыша с дырами. Опорная точка на земле под центром. Коллайдеры — по стенам.
        /// </summary>
        public static void House(MeshKit k, System.Random random, HouseOptions o)
        {
            const float floor = 0.2f;
            float w = o.Width, d = o.Depth, h = o.WallHeight;
            float top = floor + h;

            k.Box(new Vector3(0f, floor / 2f, 0f), new Vector3(w + 0.3f, floor, d + 0.3f), Quaternion.identity, Swatch.ConcreteDark, Swatch.Concrete, collider: true);
            k.Box(new Vector3(0f, floor + 0.01f, 0f), new Vector3(w - 0.2f, 0.02f, d - 0.2f), Swatch.WoodDark);

            // Одна из стен частично обрушена: доски там ниже.
            int brokenWall = random.Next(4);
            var door = new Opening(w / 2f - 0.5f, w / 2f + 0.5f, floor, floor + 2f);
            var sideWindow = new Opening(d / 2f - 0.45f, d / 2f + 0.45f, floor + 0.8f, floor + 1.5f);
            var backWindow = new Opening(w / 2f - 0.5f, w / 2f + 0.5f, floor + 0.8f, floor + 1.5f);

            var frontLeft = new Vector3(-w / 2f, 0f, -d / 2f);
            var frontRight = new Vector3(w / 2f, 0f, -d / 2f);
            var backLeft = new Vector3(-w / 2f, 0f, d / 2f);
            var backRight = new Vector3(w / 2f, 0f, d / 2f);

            if (!o.OpenFront)
            {
                PlankWall(k, random, frontLeft, frontRight, floor, h, brokenWall == 0, door);
                float side = w / 2f - 0.5f;
                k.AddCollider(new Vector3(-w / 2f + side / 2f, top / 2f, -d / 2f), new Vector3(side, top, 0.15f), Quaternion.identity);
                k.AddCollider(new Vector3(w / 2f - side / 2f, top / 2f, -d / 2f), new Vector3(side, top, 0.15f), Quaternion.identity);
                k.AddCollider(new Vector3(0f, (floor + 2f + top) / 2f, -d / 2f), new Vector3(1f, top - floor - 2f, 0.15f), Quaternion.identity);
            }
            PlankWall(k, random, frontRight, backRight, floor, h, brokenWall == 1, o.Windows ? sideWindow : null);
            PlankWall(k, random, backRight, backLeft, floor, h, brokenWall == 2, o.Windows ? backWindow : null);
            PlankWall(k, random, backLeft, frontLeft, floor, h, brokenWall == 3, o.Windows ? sideWindow : null);
            k.AddCollider(new Vector3(w / 2f, top / 2f, 0f), new Vector3(0.15f, top, d), Quaternion.identity);
            k.AddCollider(new Vector3(-w / 2f, top / 2f, 0f), new Vector3(0.15f, top, d), Quaternion.identity);
            k.AddCollider(new Vector3(0f, top / 2f, d / 2f), new Vector3(w, top, 0.15f), Quaternion.identity);

            foreach (var corner in new[] { frontLeft, frontRight, backLeft, backRight })
                k.Box(corner + Vector3.up * (floor + h / 2f), new Vector3(0.12f, h, 0.12f), Swatch.WoodDark);

            Roof(k, random, w, d, top);
        }

        private sealed class Opening
        {
            public readonly float From, To, Bottom, Top;

            public Opening(float from, float to, float bottom, float top)
            {
                From = from;
                To = to;
                Bottom = bottom;
                Top = top;
            }
        }

        /// <summary>Стена из вертикальных досок от a до b (по часовой при взгляде сверху — снаружи слева).</summary>
        private static void PlankWall(MeshKit k, System.Random random, Vector3 a, Vector3 b, float floor, float height, bool broken, Opening opening)
        {
            const float plank = 0.24f;
            Vector3 along = b - a;
            float length = along.magnitude;
            var rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, along.normalized));
            int count = Mathf.CeilToInt(length / plank);
            float brokenFrom = Range(random, 0.1f, 0.5f) * length;
            float brokenTo = brokenFrom + Range(random, 0.25f, 0.45f) * length;

            for (int i = 0; i < count; i++)
            {
                float position = (i + 0.5f) * length / count;
                float plankTop = floor + height - Range(random, 0f, 0.12f);
                if (broken && position > brokenFrom && position < brokenTo)
                    plankTop = floor + Range(random, 0.5f, 1.6f);

                Swatch swatch = random.NextDouble() switch
                {
                    < 0.5 => Swatch.Wood,
                    < 0.85 => Swatch.WoodDark,
                    _ => Swatch.WoodPale,
                };
                Vector3 basePoint = a + along.normalized * position;

                if (opening != null && position > opening.From && position < opening.To)
                {
                    PlankPiece(k, basePoint, floor, Mathf.Min(opening.Bottom, plankTop), rotation, length / count, swatch);
                    PlankPiece(k, basePoint, opening.Top, plankTop, rotation, length / count, swatch);
                }
                else
                {
                    PlankPiece(k, basePoint, floor, plankTop, rotation, length / count, swatch);
                }
            }

            if (opening != null)
            {
                // Наличники вокруг проёма.
                Vector3 dir = along.normalized;
                foreach (float edge in new[] { opening.From, opening.To })
                    k.Box(a + dir * edge + Vector3.up * ((opening.Bottom + opening.Top) / 2f), new Vector3(0.06f, opening.Top - opening.Bottom, 0.1f), rotation, Swatch.WoodPale);
                k.Box(a + dir * ((opening.From + opening.To) / 2f) + Vector3.up * opening.Top, new Vector3(opening.To - opening.From, 0.06f, 0.1f), rotation, Swatch.WoodPale);
            }
        }

        private static void PlankPiece(MeshKit k, Vector3 basePoint, float from, float to, Quaternion rotation, float width, Swatch swatch)
        {
            if (to - from < 0.05f)
                return;
            k.Box(basePoint + Vector3.up * ((from + to) / 2f), new Vector3(width * 0.96f, to - from, 0.06f), rotation, swatch);
        }

        /// <summary>Двускатная крыша из жестяных листов: часть листов сорвана, один провис.</summary>
        private static void Roof(MeshKit k, System.Random random, float w, float d, float eave)
        {
            const float overhang = 0.3f;
            const int strips = 6;
            float rise = 1.1f;
            float run = d / 2f + overhang;
            float slope = Mathf.Sqrt(rise * rise + run * run);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float stripWidth = (w + 2f * overhang) / strips;
            int sagging = random.Next(strips * 2);

            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1f : 1f;
                for (int i = 0; i < strips; i++)
                {
                    if (random.NextDouble() < 0.15)
                        continue;
                    float x = -w / 2f - overhang + (i + 0.5f) * stripWidth;
                    float extra = side * strips + i == sagging ? 8f : 0f;
                    // Локальная Z листа идёт вдоль ската: спереди вверх к коньку, сзади вниз от него.
                    var rotation = Quaternion.Euler(sign * (angle + extra), 0f, 0f);
                    var center = new Vector3(x, eave + rise / 2f, sign * run / 2f);
                    Swatch swatch = random.NextDouble() switch
                    {
                        < 0.45 => Swatch.Rust,
                        < 0.8 => Swatch.RustDark,
                        _ => Swatch.Tin,
                    };
                    k.Box(center, new Vector3(stripWidth * 0.97f, 0.04f, slope), rotation, swatch);
                }
            }

            k.Box(new Vector3(0f, eave + rise, 0f), new Vector3(w + 2f * overhang, 0.08f, 0.08f), Swatch.WoodDark);

            // Фронтоны: треугольники из досок с обеих сторон, видны снаружи и изнутри.
            foreach (float x in new[] { -w / 2f, w / 2f })
            {
                var left = new Vector3(x, eave, -d / 2f);
                var right = new Vector3(x, eave, d / 2f);
                var apex = new Vector3(x, eave + rise * (d / 2f) / run, 0f);
                k.Triangle(left, apex, right, Swatch.WoodDark);
                k.Triangle(right, apex, left, Swatch.WoodDark);
            }
        }

        /// <summary>Мёртвое дерево: кривой ствол и голые ветви. Возвращает высоту ствола (для коллайдера).</summary>
        public static float DeadTree(MeshKit k, System.Random random)
        {
            float height = Range(random, 3.5f, 6f);
            const int segments = 3;
            var points = new Vector3[segments + 1];
            var direction = Vector3.up;
            for (int i = 1; i <= segments; i++)
            {
                direction = Quaternion.Euler(Range(random, -12f, 12f), 0f, Range(random, -12f, 12f)) * direction;
                points[i] = points[i - 1] + direction * (height / segments);
            }

            k.Cylinder(Vector3.zero, Quaternion.identity, 0.32f, 0.2f, 0.3f, 6, Swatch.BarkDark, Swatch.BarkDark);
            for (int i = 0; i < segments; i++)
            {
                float r0 = Mathf.Lerp(0.2f, 0.05f, i / (float)segments);
                float r1 = Mathf.Lerp(0.2f, 0.05f, (i + 1) / (float)segments);
                k.Limb(points[i], points[i + 1], r0, r1, 6, Swatch.Bark);
            }

            // Ветви расходятся по кругу (шаг по азимуту плюс разброс), нижние длиннее верхних.
            int branches = random.Next(6, 10);
            float azimuth = Range(random, 0f, 360f);
            for (int b = 0; b < branches; b++)
            {
                float t = Mathf.Lerp(0.35f, 0.95f, (b + Range(random, 0f, 0.8f)) / branches);
                float scaled = t * segments;
                int segment = Mathf.Min(segments - 1, (int)scaled);
                Vector3 start = Vector3.Lerp(points[segment], points[segment + 1], scaled - segment);
                azimuth += 137.5f + Range(random, -25f, 25f);
                var branchDirection = Quaternion.Euler(-Range(random, 20f, 55f), azimuth, 0f) * Vector3.forward;
                float length = Range(random, 1.2f, 2.4f) * (1.15f - t * 0.6f);
                Vector3 end = start + branchDirection * length;
                k.Limb(start, end, Mathf.Lerp(0.08f, 0.04f, t), 0.012f, 4, Swatch.BarkDark);

                // Одна-две веточки от середины ветви, загнутые вверх.
                int twigs = random.Next(1, 3);
                for (int tw = 0; tw < twigs; tw++)
                {
                    Vector3 middle = Vector3.Lerp(start, end, Range(random, 0.4f, 0.75f));
                    var twig = Quaternion.Euler(-Range(random, 35f, 70f), azimuth + Range(random, -60f, 60f), 0f) * Vector3.forward;
                    k.Limb(middle, middle + twig * length * Range(random, 0.35f, 0.6f), 0.03f, 0.008f, 3, Swatch.BarkDark);
                }
            }

            return height;
        }

        /// <summary>Деревянный столб ЛЭП с перекладиной. Возвращает две точки крепления проводов.</summary>
        public static Vector3[] Pole(MeshKit k, System.Random random)
        {
            var lean = Quaternion.Euler(Range(random, -3f, 3f), 0f, Range(random, -3f, 3f));
            k.Push(Vector3.zero, lean);
            k.Cylinder(Vector3.zero, Quaternion.identity, 0.13f, 0.1f, 7.5f, 6, Swatch.WoodDark, Swatch.WoodDark);
            k.Box(new Vector3(0f, 7f, 0f), new Vector3(1.6f, 0.12f, 0.12f), Swatch.WoodDark);
            foreach (float side in new[] { -0.6f, 0.6f })
                k.Cylinder(new Vector3(side, 7.06f, 0f), Quaternion.identity, 0.04f, 0.03f, 0.1f, 6, Swatch.White, Swatch.White);
            k.Pop();
            return new[] { lean * new Vector3(-0.6f, 7.16f, 0f), lean * new Vector3(0.6f, 7.16f, 0f) };
        }

        /// <summary>Провисший провод между двумя точками.</summary>
        public static void Wire(MeshKit k, Vector3 from, Vector3 to, float sag)
        {
            const int segments = 8;
            Vector3 previous = from;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 point = Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));
                k.Limb(previous, point, 0.012f, 0.012f, 3, Swatch.HazardBlack);
                previous = point;
            }
        }

        /// <summary>Пролёт штакетника длиной length вдоль +X от начала координат. Часть штакетин выбита.</summary>
        public static void FenceSegment(MeshKit k, System.Random random, float length)
        {
            k.Push(Vector3.zero, new Vector3(Range(random, -6f, 6f), 0f, 0f));
            k.Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.1f, 1.1f, 0.1f), Swatch.WoodDark);
            foreach (float y in new[] { 0.35f, 0.85f })
                k.Box(new Vector3(length / 2f, y, 0.04f), new Vector3(length, 0.07f, 0.04f), Swatch.WoodDark);
            for (float x = 0.15f; x < length - 0.05f; x += 0.2f)
            {
                if (random.NextDouble() < 0.2)
                    continue;
                float tall = Range(random, 0.9f, 1.15f);
                var tilt = Quaternion.Euler(0f, 0f, random.NextDouble() < 0.15 ? Range(random, -12f, 12f) : 0f);
                k.Box(new Vector3(x, tall / 2f + 0.05f, -0.0f), new Vector3(0.08f, tall, 0.025f), tilt, random.NextDouble() < 0.6 ? Swatch.WoodPale : Swatch.Wood);
            }
            k.AddCollider(new Vector3(length / 2f, 0.55f, 0.02f), new Vector3(length, 1.1f, 0.12f), Quaternion.identity);
            k.Pop();
        }

        /// <summary>Бетонный обломок, иногда с торчащей арматурой.</summary>
        public static void Rubble(MeshKit k, System.Random random, Vector3 position, float size, bool brick)
        {
            var rotation = Quaternion.Euler(Range(random, -15f, 15f), Range(random, 0f, 360f), Range(random, -15f, 15f));
            var dimensions = new Vector3(size * Range(random, 0.8f, 1.4f), size * Range(random, 0.4f, 0.8f), size * Range(random, 0.8f, 1.3f));
            var sides = brick ? Swatch.Brick : (random.NextDouble() < 0.5 ? Swatch.Concrete : Swatch.ConcreteDark);
            k.RoughBox(position + Vector3.up * dimensions.y * 0.35f, dimensions, rotation, sides, brick ? Swatch.Brick : Swatch.Concrete, size * 0.18f, random);
            // Крупные обломки твёрдые, через мелкие можно пройти.
            if (size >= 0.5f)
                k.AddCollider(position + Vector3.up * dimensions.y * 0.35f, dimensions * 0.85f, rotation);

            if (!brick && random.NextDouble() < 0.4)
            {
                Vector3 start = position + Vector3.up * dimensions.y * 0.4f;
                var direction = Quaternion.Euler(-Range(random, 20f, 70f), Range(random, 0f, 360f), 0f) * Vector3.forward;
                k.Limb(start, start + direction * size * Range(random, 0.8f, 1.6f), 0.012f, 0.012f, 3, Swatch.Rust);
            }
        }

        /// <summary>Ржавая бочка, стоящая на донышке; опорная точка на земле под центром.</summary>
        public static void Barrel(MeshKit k, System.Random random, bool hazard)
        {
            var body = random.NextDouble() < 0.6 ? Swatch.Rust : Swatch.RustDark;
            k.Cylinder(Vector3.zero, Quaternion.identity, 0.3f, 0.3f, 0.88f, 10, body, Swatch.MetalDark);
            foreach (float y in new[] { 0.22f, 0.62f })
                k.Cylinder(new Vector3(0f, y, 0f), Quaternion.identity, 0.31f, 0.31f, 0.04f, 10, Swatch.RustDark, Swatch.RustDark);
            if (hazard)
                k.Cylinder(new Vector3(0f, 0.36f, 0f), Quaternion.identity, 0.305f, 0.305f, 0.14f, 10, Swatch.HazardYellow, Swatch.HazardYellow);
        }

        /// <summary>Пучок сухой травы.</summary>
        public static void GrassTuft(MeshKit k, System.Random random, Vector3 position)
        {
            int blades = random.Next(3, 6);
            for (int i = 0; i < blades; i++)
            {
                var offset = new Vector3(Range(random, -0.1f, 0.1f), 0f, Range(random, -0.1f, 0.1f));
                var rotation = Quaternion.Euler(Range(random, -25f, 25f), 0f, Range(random, -25f, 25f));
                k.Cylinder(position + offset, rotation, 0.035f, 0f, Range(random, 0.2f, 0.45f), 3, random.NextDouble() < 0.7 ? Swatch.DeadGrass : Swatch.Dirt, Swatch.Dirt);
            }
        }

        /// <summary>Мелкий мусор на земле: доска, покрышка, кусок жести.</summary>
        public static void Debris(MeshKit k, System.Random random, Vector3 position)
        {
            float yaw = Range(random, 0f, 360f);
            switch (random.Next(3))
            {
                case 0:
                    k.Box(position + Vector3.up * 0.02f, new Vector3(Range(random, 0.8f, 1.8f), 0.04f, 0.15f), Quaternion.Euler(0f, yaw, Range(random, -4f, 4f)), Swatch.WoodDark);
                    break;
                case 1:
                    k.Cylinder(position, Quaternion.Euler(0f, yaw, 0f), 0.33f, 0.33f, 0.2f, 8, Swatch.Tire, Swatch.Tire);
                    break;
                default:
                    k.Box(position + Vector3.up * 0.01f, new Vector3(Range(random, 0.5f, 1.2f), 0.015f, Range(random, 0.4f, 0.9f)), Quaternion.Euler(Range(random, -8f, 8f), yaw, 0f), Swatch.Rust);
                    break;
            }
        }

        /// <summary>Светящиеся радиоактивные осколки (для отдельного светящегося материала).</summary>
        public static void GlowShards(MeshKit k, System.Random random, Vector3 center, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                var position = center + new Vector3(Range(random, -spread, spread), 0f, Range(random, -spread, spread));
                var rotation = Quaternion.Euler(Range(random, -30f, 30f), Range(random, 0f, 360f), Range(random, -30f, 30f));
                k.Cylinder(position - Vector3.up * 0.05f, rotation, Range(random, 0.08f, 0.18f), 0f, Range(random, 0.3f, 0.8f), 5, Swatch.White, Swatch.White);
            }
        }

        /// <summary>Цифра из прямоугольников, как трафаретная краска на бетоне (для номера бункера).</summary>
        public static void StencilDigit(MeshKit k, int digit, Vector3 origin, float height, Swatch swatch)
        {
            // Семисегментная запись: верх, верх-право, низ-право, низ, низ-лево, верх-лево, середина.
            int[] masks = { 0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110, 0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111 };
            int mask = masks[Mathf.Clamp(digit, 0, 9)];
            float w = height * 0.5f, t = height * 0.12f, half = height / 2f;
            var segments = new List<(Vector3 center, Vector3 size)>
            {
                (new Vector3(w / 2f, height, 0f), new Vector3(w, t, 0.01f)),
                (new Vector3(w, half * 1.5f, 0f), new Vector3(t, half, 0.01f)),
                (new Vector3(w, half * 0.5f, 0f), new Vector3(t, half, 0.01f)),
                (new Vector3(w / 2f, 0f, 0f), new Vector3(w, t, 0.01f)),
                (new Vector3(0f, half * 0.5f, 0f), new Vector3(t, half, 0.01f)),
                (new Vector3(0f, half * 1.5f, 0f), new Vector3(t, half, 0.01f)),
                (new Vector3(w / 2f, half, 0f), new Vector3(w, t, 0.01f)),
            };
            for (int s = 0; s < segments.Count; s++)
            {
                if ((mask & (1 << s)) != 0)
                    k.Box(origin + segments[s].center, segments[s].size, swatch);
            }
        }
    }
}
