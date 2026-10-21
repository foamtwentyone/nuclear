using Nuclear.EditorTools.LowPoly;
using Nuclear.Visuals;
using UnityEngine;
using static Nuclear.EditorTools.LowPoly.MeshKit;

namespace Nuclear.EditorTools
{
    /// <summary>Части маршрута уровня 1: грунтовка через посёлок, пруды, хребет с тоннелем.</summary>
    public static partial class PrototypeSetup
    {
        /// <summary>Грунтовка от Бункера-7 через дачный посёлок к трассе: неровные края, колеи.</summary>
        private static void BuildLane(Transform level)
        {
            var random = new System.Random(21);
            var kit = new MeshKit();
            const float step = 2.5f, y = 0.025f;
            float previousWest = -2.5f, previousEast = 2.5f;
            for (float z = 5f; z < RoadZ - 3f; z += step)
            {
                float west = -2.5f + Range(random, -0.3f, 0.3f), east = 2.5f + Range(random, -0.3f, 0.3f);
                kit.Quad(new Vector3(previousWest, y, z), new Vector3(west, y, z + step), new Vector3(east, y, z + step), new Vector3(previousEast, y, z),
                    random.NextDouble() < 0.25 ? Swatch.AshDark : Swatch.Dirt);
                // Колеи — две тёмные полосы.
                foreach (float rut in new[] { -0.9f, 0.9f })
                    kit.Box(new Vector3(rut, y + 0.004f, z + step / 2f), new Vector3(0.35f, 0.004f, step), Swatch.AshDark);
                previousWest = west;
                previousEast = east;
            }
            MarkStatic(AddModel(level, "Lane", kit, Vector3.zero, Quaternion.identity, outline: false));
        }

        /// <summary>Пруды в садах: тёмная вода, камыш, кочки по краю.</summary>
        private static void BuildPonds(Transform level)
        {
            var random = new System.Random(22);
            var kit = new MeshKit();
            foreach (var pond in Ponds)
            {
                var center = new Vector3(pond.x, -0.18f, pond.y);
                kit.Cylinder(center, Quaternion.identity, pond.z, pond.z * 0.96f, 0.02f, 18, Swatch.Glass, Swatch.Glass);
                for (int i = 0; i < 26; i++)
                {
                    float angle = Range(random, 0f, Mathf.PI * 2f);
                    float radius = pond.z + Range(random, -0.2f, 0.6f);
                    var edge = OnGround(pond.x + Mathf.Cos(angle) * radius, pond.y + Mathf.Sin(angle) * radius);
                    if (i % 3 == 0)
                    {
                        // Камыш: тонкий стебель с тёмным початком.
                        float height = Range(random, 0.7f, 1.2f);
                        kit.Cylinder(edge, Quaternion.Euler(Range(random, -8f, 8f), 0f, Range(random, -8f, 8f)), 0.015f, 0.01f, height, 4, Swatch.DeadGrass, Swatch.DeadGrass);
                        kit.Cylinder(edge + Vector3.up * (height - 0.18f), Quaternion.identity, 0.035f, 0.03f, 0.16f, 6, Swatch.WoodDark, Swatch.WoodDark);
                    }
                    else
                    {
                        Models.GrassTuft(kit, random, edge);
                    }
                }
                blockedCircles.Add(new Vector3(pond.x, pond.y, pond.z + 1f));
            }
            MarkStatic(AddModel(level, "Ponds", kit, Vector3.zero, Quaternion.identity, outline: false));
        }

        /// <summary>
        /// Бетонный тоннель сквозь хребет (длинный путь развилки): портальные стены с жёлто-чёрной рамкой,
        /// внутри темно, кабели, разбитые лампы, лужи и завалы, красная аварийная лампа посередине.
        /// </summary>
        private static void BuildRidgeTunnel(Transform level)
        {
            var random = new System.Random(23);
            var kit = new MeshKit();
            float half = TunnelHalfWidth, height = TunnelHeight;
            float length = Ridge.height, centerZ = Ridge.center.y;

            kit.Box(new Vector3(TunnelX, 0.03f, centerZ), new Vector3(half * 2f + 0.4f, 0.06f, length), Quaternion.identity, Swatch.AsphaltDark, Swatch.AsphaltDark);
            foreach (float side in new[] { -1f, 1f })
                kit.Box(new Vector3(TunnelX + side * (half + 0.25f), height / 2f, centerZ), new Vector3(0.5f, height, length), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(TunnelX, height + 0.25f, centerZ), new Vector3(half * 2f + 1f, 0.5f, length), Quaternion.identity, Swatch.ConcreteDark, Swatch.AshDark, collider: true);

            // Порталы: стена с проёмом, сверху — козырёк до склона.
            foreach (float face in new[] { -1f, 1f })
            {
                float z = centerZ + face * length / 2f;
                float wing = 4.5f;
                foreach (float side in new[] { -1f, 1f })
                    kit.Box(new Vector3(TunnelX + side * (half + 0.5f + wing / 2f), 3.5f, z), new Vector3(wing, 7f, 0.6f), Swatch.Concrete, collider: true);
                kit.Box(new Vector3(TunnelX, (height + 7f) / 2f + 0.25f, z), new Vector3(half * 2f + 1f, 7f - height - 0.5f, 0.6f), Swatch.Concrete, collider: true);
                kit.Push(new Vector3(TunnelX, 0f, z - face * 0.31f), face < 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
                HazardFrame(kit, Vector3.zero, half * 2f, height - 0.2f);
                kit.Pop();
                ConcreteStains(kit, random, new Vector3(TunnelX, 3.5f, z - face * 0.31f), new Vector3(half * 2f + 9f, 6f, 0f), doorHalfWidth: half + 0.3f);
            }

            // Внутри: кабельные лотки по стенам, разбитые лампы под потолком, лужи и завалы.
            for (float z = Ridge.yMin + 3f; z < Ridge.yMax - 2f; z += 8f)
            {
                kit.Box(new Vector3(TunnelX, height - 0.08f, z), new Vector3(0.5f, 0.12f, 0.25f), Swatch.MetalDark);
                kit.Box(new Vector3(TunnelX, height - 0.16f, z), new Vector3(0.4f, 0.04f, 0.2f), random.NextDouble() < 0.5 ? Swatch.Glass : Swatch.MetalDark);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                kit.Box(new Vector3(TunnelX + side * (half - 0.08f), 2.6f, centerZ), new Vector3(0.12f, 0.08f, length), Swatch.Rubber);
                kit.Box(new Vector3(TunnelX + side * (half - 0.08f), 2.75f, centerZ), new Vector3(0.12f, 0.06f, length), Swatch.MetalDark);
            }
            for (int i = 0; i < 6; i++)
            {
                var puddle = new Vector3(TunnelX + Range(random, -half + 0.8f, half - 0.8f), 0.065f, Range(random, Ridge.yMin + 4f, Ridge.yMax - 4f));
                kit.Cylinder(puddle, Quaternion.identity, Range(random, 0.5f, 1.1f), Range(random, 0.4f, 0.9f), 0.01f, 10, Swatch.Glass, Swatch.Glass);
            }
            for (int i = 0; i < 10; i++)
                Models.Rubble(kit, random, new Vector3(TunnelX + Range(random, -half + 0.4f, half - 0.4f), 0.06f, Range(random, Ridge.yMin + 2f, Ridge.yMax - 2f)), Range(random, 0.2f, 0.6f), random.NextDouble() < 0.4);

            var tunnel = AddModel(level, "Tunnel", kit, Vector3.zero, Quaternion.identity);
            MarkStatic(tunnel);

            // Красная аварийная лампа мигает посередине — единственный свет внутри.
            var alarm = new GameObject("Emergency Lamp").AddComponent<Light>();
            alarm.transform.SetParent(tunnel.transform, false);
            alarm.transform.localPosition = new Vector3(TunnelX - half + 0.4f, height - 0.6f, centerZ + 6f);
            alarm.type = LightType.Point;
            alarm.color = new Color(1f, 0.2f, 0.12f);
            alarm.intensity = 0.9f;
            alarm.range = 7f;
            Configure(alarm.gameObject.AddComponent<LightFlicker>(), new System.Collections.Generic.Dictionary<string, object>
            {
                ["depth"] = 0.6f,
                ["speed"] = 2.5f,
            });

            var dark = new GameObject("Tunnel Darkness");
            dark.transform.SetParent(tunnel.transform, false);
            dark.transform.localPosition = new Vector3(TunnelX, height / 2f, centerZ);
            dark.AddComponent<DarkZone>().Configure(new Vector3(half * 2f, height, length - 4f), 0.08f);

            AddRoomEcho(tunnel.transform, new Vector3(TunnelX, 1.5f, centerZ), 12f, 26f);
        }
    }
}
