using System.Collections.Generic;
using Nuclear.EditorTools.LowPoly;
using Nuclear.Hazards;
using Nuclear.Items;
using Nuclear.Level;
using Nuclear.Network;
using Nuclear.Visuals;
using Nuclear.World;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static Nuclear.EditorTools.LowPoly.MeshKit;

namespace Nuclear.EditorTools
{
    /// <summary>
    /// Мир уровня 1 «Бункер-7 → Бункер-12»: заброшенный пригород под пеплом, маршрут с юга на север (~200 м).
    /// Шлюз Бункера-7 → дачный посёлок вдоль грунтовки (пруды) → трасса с брошенными машинами (аккумулятор, Слепыш)
    /// → развилка: короткий путь на западе через радиоактивную воронку или длинный на востоке через тёмный тоннель
    /// в хребте → Бункер-12 (второй Слепыш рядом). Хребет между путями не перелезть; за границей — холмы в тумане.
    /// </summary>
    public static partial class PrototypeSetup
    {
        // Игровая зона: прямоугольник маршрута.
        private const float PlayMinX = -45f, PlayMaxX = 45f, PlayMinZ = -12f, PlayMaxZ = 196f;
        private const float RoadZ = 68f;
        private static readonly Vector3 DoorPosition = new(0f, 0f, 185f);
        private static readonly Vector2 CraterCenter = new(-27f, 121f);
        private const float CraterRadius = 10f;
        private static readonly Vector2 WeakSpotCenter = new(-34f, 143f);

        // Хребет между путями развилки и тоннель сквозь него.
        private static readonly Rect Ridge = new(-14f, 95f, 59f, 50f);
        private const float TunnelX = 30f;
        private const float TunnelHalfWidth = 3f;
        private const float TunnelHeight = 4.5f;

        // Пруды в посёлке (у них будут жить лягушки).
        private static readonly Vector3[] Ponds = { new(-23f, 40f, 3.2f), new(24f, 30f, 2.6f) };

        /// <summary>Машины, у которых вскрывается капот (CarHood): где стоят, куда смотрят, какого цвета.</summary>
        private struct HoodCar
        {
            public Vector2 Position;
            public float Yaw;
            public Swatch Paint;
            public int Seed;
        }

        private static readonly HoodCar[] HoodCars =
        {
            new() { Position = new Vector2(-22f, 67f), Yaw = 90f, Paint = Swatch.PaintRed, Seed = 201 },
            new() { Position = new Vector2(6f, 69.5f), Yaw = 80f, Paint = Swatch.PaintBlue, Seed = 202 },
            new() { Position = new Vector2(27f, 66.5f), Yaw = 100f, Paint = Swatch.PaintBeige, Seed = 203 },
        };

        private static readonly Vector3[] CrateSpawns = { new(2f, 0.4f, 4f), new(3f, 0.4f, 4.5f), new(-3f, 0.4f, 3f) };
        private static readonly Vector3[] CanSpawns = { new(1f, 0.2f, 2f), new(1.4f, 0.2f, 2.3f), new(-1f, 0.2f, 2.5f), new(-1.5f, 0.2f, 1.8f), new(0f, 0.2f, 3f), new(-9f, 0.2f, 22f), new(10f, 0.2f, 48f), new(-4f, 0.2f, 74f) };
        private static readonly Vector3[] FilterSpawns = { new(-10f, 0.2f, 24f), new(10f, 0.2f, 44f), new(-20f, 0.2f, 62f), new(18f, 0.2f, 75f), new(-31f, 0.2f, 108f), new(-27f, 0.2f, 121f), new(30f, 0.2f, 128f), new(-6f, 0.2f, 165f) };
        // Стартовое снаряжение на столе в шлюзе Бункера-7: на всех не хватит, придётся делить.
        private static readonly Vector3[] TableBandages = { new(-3.45f, 0.86f, -2.1f), new(-3.25f, 0.86f, -2.2f) };
        private static readonly Vector3[] TableFlashlights = { new(-2.85f, 0.86f, -1.85f), new(-2.6f, 0.86f, -1.9f) };
        private static readonly Vector3[] TableCrowbar = { new(-3f, 0.84f, -2.28f) };
        private static readonly Vector3[] TableBatteries = { new(-2.45f, 0.84f, -2.25f), new(-2.38f, 0.84f, -2.15f) };
        // По дороге: ещё бинты и батарейки в случайных местах.
        private static readonly Vector3[] BandageSpawns = { new(-14f, 0.2f, 40f), new(14f, 0.2f, 24f), new(-24f, 0.2f, 74f), new(31f, 0.2f, 112f), new(8f, 0.2f, 160f) };
        private static readonly Vector3[] FlashlightBatterySpawns = { new(12f, 0.2f, 33f), new(-12f, 0.2f, 54f), new(24f, 0.2f, 62f), new(32f, 0.2f, 90f), new(-20f, 0.2f, 150f) };
        // Слепыш на трассе и второй — у Бункера-12: шум открывающейся двери его точно приведёт.
        private static readonly Vector3[] BlindOneSpawns = { new(-12f, 0f, 77f), new(12f, 0f, 79f), new(0f, 0f, 83f) };
        // Лягушки сидят у прудов; Рты прикидываются камнями у лута (в посёлке, у дома при трассе, в тоннеле у бинта).
        private static readonly Vector3[] FrogSpawns = { new(-19.3f, 0f, 40f), new(21.1f, 0f, 30f) };
        private static readonly Vector3[] MouthSpawns = { new(11.3f, 0f, 46.8f), new(-30.2f, 0f, 74.6f), new(TunnelX + 1.8f, 0.06f, 108.5f) };
        private static readonly Vector3[] FinaleBlindOneSpawns = { new(-12f, 0f, 168f), new(13f, 0f, 170f) };

        /// <summary>Занятые места: прямоугольники построек и круги вокруг важных точек.</summary>
        private static readonly List<Rect> blockedRects = new();
        private static readonly List<Vector3> blockedCircles = new(); // x, z, радиус

        private static void BuildScene(GameObject playerPrefab, ItemPrefabs items)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            if (UsingUrp)
            {
                CallUrp("PrepareRenderer");
                CallUrp("AddPostProcessing");
            }

            // Камера-заглушка, пока игрок не заспавнился: вид на шлюз со стороны дороги.
            var menuCamera = new GameObject("Menu Camera").AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = FogColor;
            menuCamera.farClipPlane = 120f;
            menuCamera.transform.SetPositionAndRotation(new Vector3(9f, 5.5f, 34f), Quaternion.Euler(13f, 200f, 0f));
            menuCamera.depth = -10f;
            ConfigureCameraForPipeline(menuCamera);

            var networkObject = new GameObject("NetworkManager");
            var manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            var transport = networkObject.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(SaveNetworkPrefabs(items.Battery, items.Crate, items.Can, items.Filter, items.BlindOne, items.Door,
                items.Gear.Bandage, items.Gear.Flashlight, items.Gear.Crowbar, items.Gear.Battery,
                items.Hoods[0], items.Hoods[1], items.Hoods[2], items.Catfish, items.Frog, items.Mouth));
            networkObject.AddComponent<ConnectionMenu>();

            BuildSpawner(items);

            new GameObject("Ambient Audio").AddComponent<AmbientAudio>();

            var level = new GameObject("Level").transform;
            BuildWorld(level);

            // Навигационная сетка для Слепыша строится по коллайдерам уровня при загрузке.
            var surface = level.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            level.gameObject.AddComponent<RuntimeNavMesh>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void BuildSpawner(ItemPrefabs items)
        {
            // Аккумулятор — под капотом одной из трёх машин (CarHood); ящики и консервы в шлюзе.
            var spawner = new GameObject("Item Spawner").AddComponent<ItemSpawner>();
            spawner.SetEntries(new[]
            {
                new ItemSpawner.Entry { prefab = items.Crate, positions = CrateSpawns, count = 3 },
                new ItemSpawner.Entry { prefab = items.Can, positions = CanSpawns, count = 7 },
                // Один из вариантов — в центре воронки: фильтр за дозу.
                new ItemSpawner.Entry { prefab = items.Filter, positions = FilterSpawns, count = 5 },
                // Слепыш начинает в дальней части карты, подальше от шлюза.
                new ItemSpawner.Entry { prefab = items.BlindOne, positions = BlindOneSpawns, count = 1 },
                new ItemSpawner.Entry { prefab = items.BlindOne, positions = FinaleBlindOneSpawns, count = 1 },
                // Сом лежит в тоннеле мордой к входу, усы поперёк прохода.
                new ItemSpawner.Entry { prefab = items.Catfish, positions = new[] { new Vector3(TunnelX, 0.06f, 134f) }, yaws = new[] { 180f }, count = 1 },
                new ItemSpawner.Entry { prefab = items.Frog, positions = FrogSpawns, yaws = new[] { 90f, 270f }, count = FrogSpawns.Length },
                new ItemSpawner.Entry { prefab = items.Mouth, positions = MouthSpawns, yaws = new[] { 270f, 150f, 270f }, count = MouthSpawns.Length },
                new ItemSpawner.Entry { prefab = items.Door, positions = new[] { DoorPosition }, count = 1, randomYaw = false },
                new ItemSpawner.Entry { prefab = items.Hoods[0], positions = new[] { OnGround(HoodCars[0].Position.x, HoodCars[0].Position.y) }, yaws = new[] { HoodCars[0].Yaw }, count = 1 },
                new ItemSpawner.Entry { prefab = items.Hoods[1], positions = new[] { OnGround(HoodCars[1].Position.x, HoodCars[1].Position.y) }, yaws = new[] { HoodCars[1].Yaw }, count = 1 },
                new ItemSpawner.Entry { prefab = items.Hoods[2], positions = new[] { OnGround(HoodCars[2].Position.x, HoodCars[2].Position.y) }, yaws = new[] { HoodCars[2].Yaw }, count = 1 },
                new ItemSpawner.Entry { prefab = items.Gear.Bandage, positions = TableBandages, count = TableBandages.Length },
                new ItemSpawner.Entry { prefab = items.Gear.Flashlight, positions = TableFlashlights, count = TableFlashlights.Length },
                new ItemSpawner.Entry { prefab = items.Gear.Crowbar, positions = TableCrowbar, count = 1, randomYaw = false },
                new ItemSpawner.Entry { prefab = items.Gear.Battery, positions = TableBatteries, count = TableBatteries.Length },
                new ItemSpawner.Entry { prefab = items.Gear.Bandage, positions = BandageSpawns, count = 3 },
                new ItemSpawner.Entry { prefab = items.Gear.Battery, positions = FlashlightBatterySpawns, count = 3 },
            });
        }

        /// <summary>
        /// Свет сквозь пепельную дымку: низкое тёплое солнце и прохладная заливка из тени.
        /// Контраст тёплого и холодного делает картинку объёмной, а не просто серой.
        /// </summary>
        private static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.87f, 0.7f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            // Мягкие тени, как в мультяшных играх: форму видно, но без глухой черноты.
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(32f, -35f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.56f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.47f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.27f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.sun = sun;

            // Сглаживание краёв (на low-poly «лесенка» особенно заметна) и чёткие тени.
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 60f;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 2;
        }

        private static void BuildWorld(Transform level)
        {
            blockedRects.Clear();
            blockedCircles.Clear();
            foreach (var spawns in new[] { CrateSpawns, CanSpawns, FilterSpawns, BandageSpawns, FlashlightBatterySpawns, BlindOneSpawns, FinaleBlindOneSpawns, FrogSpawns, MouthSpawns })
            {
                foreach (var point in spawns)
                    blockedCircles.Add(new Vector3(point.x, point.z, 1.8f));
            }
            blockedRects.Add(new Rect(PlayMinX - 30f, RoadZ - 3.5f, PlayMaxX - PlayMinX + 60f, 7f));
            blockedRects.Add(new Rect(-3.5f, 5f, 7f, 58f)); // грунтовка через посёлок
            blockedRects.Add(Ridge);

            BuildGround(level);
            BuildRoad(level);
            BuildLane(level);
            BuildPonds(level);
            BuildRidgeTunnel(level);
            BuildBunker7(level);
            BuildBunker12(level);
            BuildHouses(level);
            BuildCars(level);
            BuildPoles(level);
            BuildFences(level);
            BuildHazardProps(level);
            BuildFireBarrels(level);
            BuildTrees(level);
            BuildScatter(level);
            BuildBoundary(level);
        }

        // ---------- Рельеф ----------

        /// <summary>
        /// Высота земли. Внутри игровой зоны только неглубокие впадины (не выше нуля, чтобы точки появления не утонули),
        /// ровно под дорогой и бункерами, чаша воронки с валом, за границей — холмы.
        /// </summary>
        private static float GroundHeight(float x, float z)
        {
            float h = -0.12f * Mathf.PerlinNoise(x * 0.15f + 100f, z * 0.15f + 100f);

            // Ровно под дорогой, грунтовкой, бункерами и тоннелем.
            float flat = Mathf.InverseLerp(3.5f, 5f, Mathf.Abs(z - RoadZ));
            flat = Mathf.Min(flat, RectFalloff(x, z, new Rect(-7f, -7f, 14f, 14f), 1.5f));
            flat = Mathf.Min(flat, RectFalloff(x, z, new Rect(DoorPosition.x - 9f, DoorPosition.z - 4f, 18f, 12f), 1.5f));
            flat = Mathf.Min(flat, RectFalloff(x, z, new Rect(-3f, 5f, 6f, 58f), 1f));
            flat = Mathf.Min(flat, RectFalloff(x, z, new Rect(TunnelX - 4.5f, Ridge.yMin - 6f, 9f, Ridge.height + 12f), 1f));
            h *= flat;

            // Воронка: чаша с валом.
            float crater = Vector2.Distance(new Vector2(x, z), CraterCenter);
            if (crater < CraterRadius)
                h -= 1.8f * (1f - crater * crater / (CraterRadius * CraterRadius));
            h += 0.5f * Mathf.Exp(-Mathf.Pow((crater - CraterRadius - 0.6f) / 1.6f, 2f));

            // Пруды — неглубокие ямки под водой.
            foreach (var pond in Ponds)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(pond.x, pond.y));
                if (d < pond.z + 0.6f)
                    h -= 0.45f * Mathf.SmoothStep(1f, 0f, d / (pond.z + 0.6f));
            }

            h += RidgeHeight(x, z);

            // За границей игровой зоны — холмы.
            float outside = Mathf.Max(Mathf.Max(PlayMinX - x, x - PlayMaxX), Mathf.Max(PlayMinZ - z, z - PlayMaxZ));
            if (outside > -1f)
                h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 15f, outside)) * (5f + 4f * Mathf.PerlinNoise(x * 0.05f, z * 0.05f));

            return h;
        }

        /// <summary>
        /// Хребет между путями развилки: крутые склоны (не взобраться) и прорезь посередине,
        /// в которой стоит бетонный тоннель.
        /// </summary>
        private static float RidgeHeight(float x, float z)
        {
            float inside = Mathf.Min(Mathf.Min(x - Ridge.xMin, Ridge.xMax - x), Mathf.Min(z - Ridge.yMin, Ridge.yMax - z));
            if (inside <= 0f)
                return 0f;
            float hill = 10f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inside / 6f)) * (0.85f + 0.3f * Mathf.PerlinNoise(x * 0.08f + 31f, z * 0.08f));
            float slot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TunnelHalfWidth + 1.5f, TunnelHalfWidth + 5.5f, Mathf.Abs(x - TunnelX)));
            return hill * slot;
        }

        /// <summary>0 внутри прямоугольника, плавно до 1 на расстоянии falloff от него.</summary>
        private static float RectFalloff(float x, float z, Rect rect, float falloff)
        {
            float dx = Mathf.Max(rect.xMin - x, 0f, x - rect.xMax);
            float dz = Mathf.Max(rect.yMin - z, 0f, z - rect.yMax);
            return Mathf.InverseLerp(0f, falloff, Mathf.Sqrt(dx * dx + dz * dz));
        }

        private static Vector3 OnGround(float x, float z) => new(x, GroundHeight(x, z), z);

        private static void BuildGround(Transform level)
        {
            const float cell = 1.25f;
            const float margin = 30f;
            float minX = PlayMinX - margin, minZ = PlayMinZ - margin;
            int cellsX = Mathf.RoundToInt((PlayMaxX - PlayMinX + margin * 2f) / cell);
            int cellsZ = Mathf.RoundToInt((PlayMaxZ - PlayMinZ + margin * 2f) / cell);
            var random = new System.Random(1);
            var kit = new MeshKit();

            for (int ix = 0; ix < cellsX; ix++)
            {
                for (int iz = 0; iz < cellsZ; iz++)
                {
                    float x0 = minX + ix * cell, z0 = minZ + iz * cell;
                    float x1 = x0 + cell, z1 = z0 + cell;
                    var a = OnGround(x0, z0);
                    var b = OnGround(x0, z1);
                    var c = OnGround(x1, z1);
                    var d = OnGround(x1, z0);
                    kit.Triangle(a, b, c, GroundSwatch((a + b + c) / 3f, random));
                    kit.Triangle(a, c, d, GroundSwatch((a + c + d) / 3f, random));
                }
            }

            var ground = AddModel(level, "Ground", kit, Vector3.zero, Quaternion.identity, outline: false);
            ground.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            MarkStatic(ground);
        }

        private static Swatch GroundSwatch(Vector3 point, System.Random random)
        {
            double roll = random.NextDouble();
            float crater = Vector2.Distance(new Vector2(point.x, point.z), CraterCenter);
            if (crater < CraterRadius)
                return roll < 0.6 ? Swatch.Scorched : Swatch.ScorchedLight;
            if (crater < CraterRadius + 2.5f)
                return roll < 0.6 ? Swatch.AshDark : Swatch.Scorched;
            // Склоны хребта и холмов — тёмная земля и пепел.
            if (point.y > 1.2f || point.x < PlayMinX - 1f || point.x > PlayMaxX + 1f || point.z < PlayMinZ - 1f || point.z > PlayMaxZ + 1f)
                return roll < 0.5 ? Swatch.AshDark : Swatch.Dirt;

            // Крупные пятна (трава, грязь, пепел) и мелкая рябь внутри них — по шуму, а не случайно,
            // чтобы соседние треугольники складывались в пятна, а не в «телевизионный снег».
            float patch = Mathf.PerlinNoise(point.x * 0.07f + 7f, point.z * 0.07f + 3f);
            float detail = Mathf.PerlinNoise(point.x * 0.35f + 50f, point.z * 0.35f + 20f);
            if (patch > 0.62f)
                return detail > 0.62f ? Swatch.Dirt : Swatch.DeadGrass;
            if (patch < 0.32f)
                return detail > 0.66f ? Swatch.AshDark : Swatch.Dirt;
            if (detail < 0.36f)
                return Swatch.AshDark;
            return detail > 0.68f ? Swatch.AshLight : Swatch.Ash;
        }

        /// <summary>Старое шоссе с неровными краями, заплатками и стёртой разметкой.</summary>
        private static void BuildRoad(Transform level)
        {
            const float step = 3f;
            const float y = 0.03f;
            var random = new System.Random(2);
            var kit = new MeshKit();
            const float roadStart = PlayMinX - 30f;
            int segments = Mathf.RoundToInt((PlayMaxX - PlayMinX + 60f) / step);
            var near = new float[segments + 1];
            var far = new float[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                near[i] = RoadZ - 3f + Range(random, -0.25f, 0.25f);
                far[i] = RoadZ + 3f + Range(random, -0.25f, 0.25f);
            }

            for (int i = 0; i < segments; i++)
            {
                float x0 = roadStart + i * step, x1 = x0 + step;
                var swatch = random.NextDouble() < 0.2 ? Swatch.AsphaltDark : Swatch.Asphalt;
                kit.Quad(new Vector3(x0, y, near[i]), new Vector3(x0, y, far[i]), new Vector3(x1, y, far[i + 1]), new Vector3(x1, y, near[i + 1]), swatch);

                if (random.NextDouble() < 0.7)
                    kit.Box(new Vector3(x0 + step / 2f, y + 0.003f, RoadZ), new Vector3(1.8f, 0.004f, 0.18f), Swatch.RoadLine);
                if (random.NextDouble() < 0.5)
                {
                    var crack = Quaternion.Euler(0f, Range(random, 0f, 180f), 0f);
                    kit.Box(new Vector3(x0 + Range(random, 0f, step), y + 0.004f, RoadZ + Range(random, -2.5f, 2.5f)), new Vector3(Range(random, 0.6f, 2f), 0.004f, 0.06f), crack, Swatch.AsphaltDark);
                }
            }

            MarkStatic(AddModel(level, "Road", kit, Vector3.zero, Quaternion.identity, outline: false));
        }

        // ---------- Бункеры ----------

        /// <summary>
        /// Шлюз Бункера-7 — старт: бетонная коробка с проёмом на север, к дороге.
        /// Внутри чистый воздух, тёплая лампа, стеллажи. Створка гермодвери распахнута наружу.
        /// </summary>
        private static void BuildBunker7(Transform level)
        {
            var random = new System.Random(7);
            var kit = new MeshKit();

            kit.Box(new Vector3(0f, 0.03f, 0f), new Vector3(10.4f, 0.06f, 10.4f), Quaternion.identity, Swatch.ConcreteDark, Swatch.ConcreteDark);
            kit.Box(new Vector3(0f, 1.5f, -5.2f), new Vector3(10.8f, 3f, 0.4f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(-5.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 10.8f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(5.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 10.8f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(-3.45f, 1.5f, 5.2f), new Vector3(3.9f, 3f, 0.4f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(3.45f, 1.5f, 5.2f), new Vector3(3.9f, 3f, 0.4f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(0f, 2.75f, 5.2f), new Vector3(3f, 0.5f, 0.4f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(0f, 3.15f, 0f), new Vector3(11.2f, 0.3f, 11.2f), Quaternion.identity, Swatch.ConcreteDark, Swatch.AshDark, collider: true);

            ConcreteStains(kit, random, new Vector3(0f, 1.5f, 5.41f), new Vector3(10f, 2.6f, 0f), doorHalfWidth: 1.85f);
            ConcreteStains(kit, random, new Vector3(5.41f, 1.5f, 0f), new Vector3(0f, 2.6f, 10f));
            ConcreteStains(kit, random, new Vector3(-5.41f, 1.5f, 0f), new Vector3(0f, 2.6f, 10f));
            HazardFrame(kit, new Vector3(0f, 0f, 5.42f), 3f, 2.5f);

            // Распахнутая наружу створка, прижатая к стене.
            kit.Box(new Vector3(1.55f, 1.25f, 6.1f), new Vector3(0.1f, 2.5f, 1.4f), Swatch.MetalDark, collider: true);
            kit.Box(new Vector3(1.61f, 0.3f, 6.1f), new Vector3(0.02f, 0.25f, 1.3f), Swatch.HazardYellow);

            // Внутри: стеллажи у задней стены, шкафчики слева, стол.
            for (int i = 0; i < 3; i++)
            {
                float x = -3f + i * 2.4f;
                kit.Box(new Vector3(x, 1f, -4.6f), new Vector3(2f, 2f, 0.5f), Swatch.WoodDark, collider: true);
                for (int shelf = 0; shelf < 3; shelf++)
                    kit.Box(new Vector3(x + Range(random, -0.5f, 0.5f), 0.55f + shelf * 0.6f, -4.45f), new Vector3(Range(random, 0.3f, 0.7f), 0.25f, 0.3f), random.NextDouble() < 0.5 ? Swatch.Canvas : Swatch.FilterOlive);
            }
            for (int i = 0; i < 3; i++)
                kit.Box(new Vector3(-4.65f, 0.95f, -2.5f + i * 0.75f), new Vector3(0.6f, 1.9f, 0.7f), Swatch.MetalDark, collider: true);
            kit.Box(new Vector3(-3f, 0.75f, -2f), new Vector3(1.4f, 0.08f, 0.8f), Swatch.Wood, collider: true);
            foreach (var leg in new[] { new Vector3(-3.6f, 0.35f, -2.3f), new Vector3(-2.4f, 0.35f, -2.3f), new Vector3(-3.6f, 0.35f, -1.7f), new Vector3(-2.4f, 0.35f, -1.7f) })
                kit.Box(leg, new Vector3(0.06f, 0.7f, 0.06f), Swatch.WoodDark);
            kit.Box(new Vector3(0f, 2.95f, -1f), new Vector3(0.8f, 0.08f, 0.25f), Swatch.MetalDark);

            // Мешки с песком у выхода.
            for (int i = 0; i < 6; i++)
                kit.RoughBox(new Vector3(-2.6f - (i % 3) * 0.55f, 0.15f + (i / 3) * 0.28f, 6f), new Vector3(0.55f, 0.28f, 0.35f), Quaternion.Euler(0f, Range(random, -8f, 8f), 0f), Swatch.Canvas, Swatch.Canvas, 0.04f, random);
            kit.AddCollider(new Vector3(-3.15f, 0.3f, 6f), new Vector3(1.7f, 0.6f, 0.4f), Quaternion.identity);

            var bunker = AddModel(level, "Bunker-7", kit, Vector3.zero, Quaternion.identity);
            MarkStatic(bunker);

            var lamp = new GameObject("Bunker-7 Lamp").AddComponent<Light>();
            lamp.transform.SetParent(bunker.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 2.7f, -1f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.75f, 0.45f);
            lamp.intensity = 1.6f;
            lamp.range = 9f;

            AddRoomEcho(bunker.transform, new Vector3(0f, 1.5f, 0f), 5f, 8f);

            var airlock = new GameObject("Airlock (safe air)");
            airlock.transform.SetParent(level, false);
            airlock.transform.localPosition = new Vector3(0f, 2f, 0f);
            airlock.AddComponent<SafeAirZone>().Configure(new Vector3(10f, 5f, 10f));

            blockedRects.Add(new Rect(-7f, -7f, 14f, 15f));
        }

        /// <summary>
        /// Здание Бункера-12 в дальнем конце карты: фасад с проёмом под дверь и комната за ним.
        /// Внутри чистый воздух и зона победы. Сама створка — сетевой префаб.
        /// </summary>
        private static void BuildBunker12(Transform level)
        {
            var random = new System.Random(12);
            var kit = new MeshKit();

            kit.Box(new Vector3(-3.75f, 1.75f, 0f), new Vector3(4.5f, 3.5f, 0.5f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(3.75f, 1.75f, 0f), new Vector3(4.5f, 3.5f, 0.5f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(0f, 3.05f, 0f), new Vector3(3f, 0.9f, 0.5f), Swatch.Concrete, collider: true);
            kit.Box(new Vector3(-3f, 1.75f, 3f), new Vector3(0.3f, 3.5f, 5.5f), Swatch.ConcreteDark, collider: true);
            kit.Box(new Vector3(3f, 1.75f, 3f), new Vector3(0.3f, 3.5f, 5.5f), Swatch.ConcreteDark, collider: true);
            kit.Box(new Vector3(0f, 1.75f, 5.75f), new Vector3(6f, 3.5f, 0.3f), Swatch.ConcreteDark, collider: true);
            kit.Box(new Vector3(0f, 3.6f, 3f), new Vector3(6.4f, 0.2f, 6f), Quaternion.identity, Swatch.ConcreteDark, Swatch.AshDark, collider: true);
            kit.Box(new Vector3(0f, 0.03f, 3f), new Vector3(5.7f, 0.06f, 5.5f), Swatch.ConcreteDark);

            ConcreteStains(kit, random, new Vector3(0f, 1.6f, -0.26f), new Vector3(11f, 2.8f, 0f), doorHalfWidth: 1.85f);
            HazardFrame(kit, new Vector3(0f, 0f, -0.27f), 3f, 2.6f);
            // Цифры чуть впереди пятен, чтобы не мерцать с ними в одной плоскости.
            Models.StencilDigit(kit, 1, new Vector3(-4.6f, 1.6f, -0.272f), 0.9f, Swatch.White);
            Models.StencilDigit(kit, 2, new Vector3(-3.85f, 1.6f, -0.272f), 0.9f, Swatch.White);

            // Мешки с песком по бокам от входа и обломки бетона.
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 6; i++)
                    kit.RoughBox(new Vector3(side * (2.3f + (i % 3) * 0.55f), 0.15f + (i / 3) * 0.28f, -1.4f), new Vector3(0.55f, 0.28f, 0.35f), Quaternion.Euler(0f, Range(random, -8f, 8f), 0f), Swatch.Canvas, Swatch.Canvas, 0.04f, random);
                kit.AddCollider(new Vector3(side * 2.85f, 0.3f, -1.4f), new Vector3(1.7f, 0.6f, 0.4f), Quaternion.identity);
            }
            for (int i = 0; i < 5; i++)
                Models.Rubble(kit, random, new Vector3(Range(random, -6f, 6f), 0f, Range(random, -3.5f, -2f)), Range(random, 0.25f, 0.6f), false);

            var bunker = AddModel(level, "Bunker-12", kit, DoorPosition, Quaternion.identity);
            MarkStatic(bunker);

            var lamp = new GameObject("Bunker-12 Lamp").AddComponent<Light>();
            lamp.transform.SetParent(bunker.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 3.2f, 3f);
            lamp.type = LightType.Point;
            lamp.color = new Color(0.85f, 0.92f, 1f);
            lamp.intensity = 1.3f;
            lamp.range = 7f;

            AddRoomEcho(bunker.transform, new Vector3(0f, 1.5f, 3f), 3f, 5f);

            var goal = new GameObject("Goal (inside Bunker-12)");
            goal.transform.SetParent(bunker.transform, false);
            goal.transform.localPosition = new Vector3(0f, 1.5f, 3.2f);
            goal.AddComponent<GoalZone>().Configure(new Vector3(5.4f, 3f, 4.5f));

            var air = new GameObject("Bunker-12 (safe air)");
            air.transform.SetParent(bunker.transform, false);
            air.transform.localPosition = new Vector3(0f, 1.75f, 3f);
            air.AddComponent<SafeAirZone>().Configure(new Vector3(5.6f, 3.5f, 5f));

            blockedRects.Add(new Rect(DoorPosition.x - 8f, DoorPosition.z - 4f, 16f, 11f));
        }

        /// <summary>Эхо бетонной комнаты: все звуки внутри бункера звучат гулко.</summary>
        private static void AddRoomEcho(Transform parent, Vector3 position, float minDistance, float maxDistance)
        {
            var echo = new GameObject("Room Echo").AddComponent<AudioReverbZone>();
            echo.transform.SetParent(parent, false);
            echo.transform.localPosition = position;
            echo.reverbPreset = AudioReverbPreset.Stoneroom;
            echo.minDistance = minDistance;
            echo.maxDistance = maxDistance;
        }

        /// <summary>
        /// Подтёки и пятна на бетонной стене. size задаёт плоскость стены (одна из осей — ноль).
        /// doorHalfWidth — полуширина проёма в центре стены, где пятен быть не должно (иначе они висят в воздухе).
        /// </summary>
        private static void ConcreteStains(MeshKit kit, System.Random random, Vector3 center, Vector3 size, float doorHalfWidth = 0f)
        {
            bool alongX = size.x > 0f;
            for (int i = 0; i < 6; i++)
            {
                float along = Range(random, -0.45f, 0.45f) * (alongX ? size.x : size.z);
                if (Mathf.Abs(along) < doorHalfWidth)
                    along = Mathf.Sign(along) * Range(random, doorHalfWidth, 0.45f * (alongX ? size.x : size.z));
                float height = Range(random, 0.4f, 1.6f);
                var position = center + (alongX ? new Vector3(along, size.y / 2f - height / 2f - Range(random, 0f, 0.4f), 0f) : new Vector3(0f, size.y / 2f - height / 2f - Range(random, 0f, 0.4f), along));
                var stain = alongX ? new Vector3(Range(random, 0.15f, 0.6f), height, 0.01f) : new Vector3(0.01f, height, Range(random, 0.15f, 0.6f));
                kit.Box(position, stain, random.NextDouble() < 0.7 ? Swatch.ConcreteStain : Swatch.RustDark);
            }
        }

        /// <summary>Жёлто-чёрная рамка вокруг дверного проёма шириной width и высотой height на фасаде (-Z).</summary>
        private static void HazardFrame(MeshKit kit, Vector3 bottomCenter, float width, float height)
        {
            const float band = 0.18f;
            const float block = 0.3f;
            int vertical = Mathf.CeilToInt(height / block);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < vertical; i++)
                    kit.Box(bottomCenter + new Vector3(side * (width / 2f + band / 2f), (i + 0.5f) * block, 0f), new Vector3(band, block, 0.02f), i % 2 == 0 ? Swatch.HazardYellow : Swatch.HazardBlack);
            }
            int horizontal = Mathf.CeilToInt((width + band * 2f) / block);
            for (int i = 0; i < horizontal; i++)
                kit.Box(bottomCenter + new Vector3(-width / 2f - band + (i + 0.5f) * block, height + band / 2f, 0f), new Vector3(block, band, 0.02f), i % 2 == 0 ? Swatch.HazardBlack : Swatch.HazardYellow);
        }

        // ---------- Пригород ----------

        private static void BuildHouses(Transform level)
        {
            var standard = new Models.HouseOptions { Width = 5f, Depth = 4f, WallHeight = 2.4f, Windows = true };
            var shed = new Models.HouseOptions { Width = 5f, Depth = 4f, WallHeight = 2.3f, OpenFront = true };

            // Дачи вдоль грунтовки: западные смотрят дверью на восток (на улицу), восточные — на запад.
            PlaceHouse(level, "Dacha 1", new Vector2(-13f, 18f), 90f, standard, 101);
            PlaceHouse(level, "Dacha 2", new Vector2(-14f, 32f), 90f, standard, 102);
            PlaceHouse(level, "Dacha 3", new Vector2(-13f, 50f), 90f, standard, 103);
            PlaceHouse(level, "Dacha 4", new Vector2(13f, 22f), 270f, standard, 104);
            PlaceHouse(level, "Dacha 5", new Vector2(14f, 40f), 270f, standard, 105);
            // Сарай без передней стены: укрытие, чтобы прятаться от Слепыша.
            PlaceHouse(level, "Shed", new Vector2(13f, 54f), 270f, shed, 106);
            // Дома у трассы и у Бункера-12.
            PlaceHouse(level, "Roadside House", new Vector2(-33f, 78f), 180f, standard, 107);
            PlaceHouse(level, "Guard House", new Vector2(14f, 176f), 270f, shed, 108);
        }

        private static void PlaceHouse(Transform level, string name, Vector2 position, float yaw, Models.HouseOptions options, int seed)
        {
            var kit = new MeshKit();
            Models.House(kit, new System.Random(seed), options);
            MarkStatic(AddModel(level, name, kit, OnGround(position.x, position.y), Quaternion.Euler(0f, yaw, 0f)));
            float half = Mathf.Max(options.Width, options.Depth) / 2f + 1.5f;
            blockedRects.Add(new Rect(position.x - half, position.y - half, half * 2f, half * 2f));
        }

        private static void BuildCars(Transform level)
        {
            // Три машины с открывающимся капотом: под одним из них аккумулятор.
            foreach (var spot in HoodCars)
                PlaceCar(level, new Vector2(spot.Position.x, spot.Position.y), spot.Yaw, spot.Paint, spot.Seed, openableHood: true);
            PlaceCar(level, new Vector2(-36f, 68.5f), 95f, Swatch.PaintGreen, 204);
            PlaceCar(level, new Vector2(-8f, 66.5f), 265f, Swatch.PaintBeige, 205);
            PlaceCar(level, new Vector2(16f, 69f), 275f, Swatch.PaintRed, 206);
            PlaceCar(level, new Vector2(39f, 67f), 85f, Swatch.PaintBlue, 207);
            PlaceCar(level, new Vector2(4.8f, 28f), 8f, Swatch.PaintGreen, 208);
            // Брошенная машина в тоннеле перегораживает полпрохода.
            PlaceCar(level, new Vector2(TunnelX + 0.9f, 120f), 12f, Swatch.PaintBeige, 209);
        }

        private static void PlaceCar(Transform level, Vector2 position, float yaw, Swatch paint, int seed, bool openableHood = false)
        {
            var kit = new MeshKit();
            Models.Car(kit, new System.Random(seed), paint, openableHood);
            MarkStatic(AddModel(level, $"Car {seed - 200}", kit, OnGround(position.x, position.y), Quaternion.Euler(0f, yaw, 0f)));
            blockedCircles.Add(new Vector3(position.x, position.y, 3f));
        }

        /// <summary>Столбы ЛЭП вдоль дороги с провисшими проводами.</summary>
        private static void BuildPoles(Transform level)
        {
            var random = new System.Random(3);
            var wires = new MeshKit();
            Vector3[] previous = null;
            for (float x = PlayMinX - 12f; x <= PlayMaxX + 12f; x += 12f)
            {
                var position = OnGround(x, RoadZ + 4.5f);
                var kit = new MeshKit();
                var anchors = Models.Pole(kit, random);
                var pole = AddModel(level, $"Pole {x}", kit, position, Quaternion.identity);
                var capsule = pole.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 3.75f, 0f);
                capsule.height = 7.5f;
                capsule.radius = 0.15f;
                MarkStatic(pole);

                var world = new[] { position + anchors[0], position + anchors[1] };
                if (previous != null)
                {
                    for (int i = 0; i < 2; i++)
                        Models.Wire(wires, previous[i], world[i], Range(random, 0.5f, 1f));
                }
                previous = world;
            }
            MarkStatic(AddModel(level, "Wires", wires, Vector3.zero, Quaternion.identity, outline: false));
        }

        /// <summary>Штакетник вдоль участков у дорог, с калитками напротив домов.</summary>
        private static void BuildFences(Transform level)
        {
            var random = new System.Random(4);
            var kit = new MeshKit();
            const float segment = 2.4f;
            // Заборы вдоль грунтовки (по Z) с калитками напротив дач.
            FenceAlongZ(kit, random, segment, -6.5f, 10f, 60f, new[] { 18f, 32f, 50f });
            FenceAlongZ(kit, random, segment, 6.5f, 10f, 60f, new[] { 22f, 40f, 54f });
            // Ограда вокруг Бункера-12 с проходами с юга, с запада и с востока.
            FenceAlongX(kit, random, segment, DoorPosition.z - 14f, DoorPosition.x - 20f, DoorPosition.x + 20f, new[] { DoorPosition.x - 1.2f, DoorPosition.x + 1.2f, DoorPosition.x - 3.6f, DoorPosition.x + 3.6f });
            MarkStatic(AddModel(level, "Fences", kit, Vector3.zero, Quaternion.identity));
        }

        private static void FenceAlongZ(MeshKit kit, System.Random random, float segment, float x, float fromZ, float toZ, float[] gates)
        {
            for (float z = fromZ; z + segment <= toZ + 0.01f; z += segment)
            {
                float middle = z + segment / 2f;
                if (System.Array.Exists(gates, gate => Mathf.Abs(middle - gate) < 1.2f) || IsNearSpawn(new Vector2(x, middle), 1.6f))
                    continue;
                // Сегмент строится вдоль +X; поворот на −90° по Y кладёт его вдоль +Z.
                kit.Push(OnGround(x, z), Quaternion.Euler(0f, -90f, 0f));
                Models.FenceSegment(kit, random, segment);
                kit.Pop();
            }
        }

        private static void FenceAlongX(MeshKit kit, System.Random random, float segment, float z, float fromX, float toX, float[] gates)
        {
            for (float x = fromX; x + segment <= toX + 0.01f; x += segment)
            {
                float middle = x + segment / 2f;
                if (System.Array.Exists(gates, gate => Mathf.Abs(middle - gate) < 1.2f) || IsNearSpawn(new Vector2(middle, z), 1.6f))
                    continue;
                kit.Push(OnGround(x, z), Quaternion.identity);
                Models.FenceSegment(kit, random, segment);
                kit.Pop();
            }
        }

        /// <summary>Видимые признаки радиации: светящиеся осколки в воронке, протёкшие бочки у слабого пятна.</summary>
        private static void BuildHazardProps(Transform level)
        {
            var random = new System.Random(5);

            var shards = new MeshKit();
            var craterBottom = OnGround(CraterCenter.x, CraterCenter.y);
            Models.GlowShards(shards, random, craterBottom + new Vector3(1.2f, 0f, 0.8f), 14, 1.6f);
            Models.GlowShards(shards, random, craterBottom + new Vector3(-1.5f, 0.15f, -1.2f), 8, 1f);
            MarkStatic(AddModel(level, "Crater Shards", shards, Vector3.zero, Quaternion.identity, glowMaterial));

            var rim = new MeshKit();
            for (int i = 0; i < 22; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float radius = Range(random, CraterRadius - 0.5f, CraterRadius + 2.5f);
                Models.Rubble(rim, random, OnGround(CraterCenter.x + Mathf.Cos(angle) * radius, CraterCenter.y + Mathf.Sin(angle) * radius), Range(random, 0.3f, 0.9f), false);
            }
            MarkStatic(AddModel(level, "Crater Rubble", rim, Vector3.zero, Quaternion.identity));

            var barrels = new MeshKit();
            var spot = OnGround(WeakSpotCenter.x, WeakSpotCenter.y);
            for (int i = 0; i < 5; i++)
            {
                var offset = new Vector3(Range(random, -2f, 2f), 0f, Range(random, -2f, 2f));
                bool tipped = i == 0;
                var rotation = tipped ? Quaternion.Euler(90f, Range(random, 0f, 360f), 0f) : Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);
                barrels.Push(spot + offset + (tipped ? Vector3.up * 0.3f : Vector3.zero), rotation);
                Models.Barrel(barrels, random, i % 2 == 0);
                barrels.AddCollider(new Vector3(0f, 0.44f, 0f), new Vector3(0.6f, 0.88f, 0.6f), Quaternion.identity);
                barrels.Pop();
            }
            MarkStatic(AddModel(level, "Barrels", barrels, Vector3.zero, Quaternion.identity));

            var puddle = new MeshKit();
            puddle.Cylinder(spot + Vector3.up * 0.01f, Quaternion.identity, 1.6f, 1.5f, 0.02f, 10, Swatch.White, Swatch.White);
            MarkStatic(AddModel(level, "Toxic Puddle", puddle, Vector3.zero, Quaternion.identity, glowMaterial));
            AddPointLight(level, "Puddle Glow", spot + Vector3.up * 0.8f, new Color(0.5f, 1f, 0.3f), 0.9f, 5f);

            var zones = new GameObject("Radiation").transform;
            zones.SetParent(level, false);
            AddRadiationZone(zones, "Crater (radiation)", new Vector3(CraterCenter.x, 1f, CraterCenter.y), CraterRadius + 3f, 4f);
            AddRadiationZone(zones, "Weak Spot (radiation)", new Vector3(WeakSpotCenter.x, 1f, WeakSpotCenter.y), 6f, 1.5f);

            blockedCircles.Add(new Vector3(CraterCenter.x, CraterCenter.y, CraterRadius - 2f));
            blockedCircles.Add(new Vector3(WeakSpotCenter.x, WeakSpotCenter.y, 3f));
        }

        /// <summary>
        /// Бочки с огнём: тёплые островки света в сером мире и ориентиры (у выхода из Бункера-7 и у Бункера-12).
        /// </summary>
        private static void BuildFireBarrels(Transform level)
        {
            var random = new System.Random(15);
            var ember = SaveMaterial("Ember", new Color(1f, 0.45f, 0.15f), emission: new Color(2.4f, 0.9f, 0.25f));
            foreach (var spot in new[] { new Vector2(-4.6f, 6.4f), new Vector2(-4.2f, 60f), new Vector2(TunnelX + 5f, Ridge.yMin - 4f), new Vector2(TunnelX - 5f, Ridge.yMax + 4f), new Vector2(4.9f, DoorPosition.z - 3.7f) })
            {
                var position = OnGround(spot.x, spot.y);
                var kit = new MeshKit();
                Models.Barrel(kit, random, hazard: false);
                kit.AddCollider(new Vector3(0f, 0.44f, 0f), new Vector3(0.6f, 0.88f, 0.6f), Quaternion.identity);
                var barrel = AddModel(level, "Fire Barrel", kit, position, Quaternion.Euler(0f, Range(random, 0f, 360f), 0f));
                MarkStatic(barrel);

                var coalsKit = new MeshKit();
                Models.GlowShards(coalsKit, random, new Vector3(0f, 0.86f, 0f), 7, 0.15f);
                AddModel(barrel.transform, "Coals", coalsKit, Vector3.zero, Quaternion.identity, ember, scale: 0.6f).transform.localPosition = new Vector3(0f, 0.35f, 0f);

                AddFire(barrel.transform, new Vector3(0f, 0.95f, 0f));

                var light = new GameObject("Fire Light").AddComponent<Light>();
                light.transform.SetParent(barrel.transform, false);
                light.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.3f);
                light.intensity = 2.2f;
                light.range = 9f;
                light.shadows = LightShadows.Soft;
                Configure(light.gameObject.AddComponent<LightFlicker>(), new Dictionary<string, object>
                {
                    ["depth"] = 0.3f,
                    ["speed"] = 9f,
                });

                blockedCircles.Add(new Vector3(spot.x, spot.y, 1.5f));
            }
        }

        private static void AddRadiationZone(Transform parent, string name, Vector3 position, float radius, float intensity)
        {
            var zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = position;
            zone.AddComponent<RadiationZone>().Configure(radius, intensity);
            AddRadiationVisuals(parent, new Vector2(position.x, position.z), radius, intensity);
        }

        private static void AddPointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        /// <summary>Мёртвые деревья: в игровой зоне реже, за её границей — силуэтами на холмах.</summary>
        private static void BuildTrees(Transform level)
        {
            var random = new System.Random(6);
            int placed = 0;
            for (int attempt = 0; attempt < 1500 && placed < 130; attempt++)
            {
                var position = new Vector2(Range(random, PlayMinX + 2f, PlayMaxX - 2f), Range(random, PlayMinZ + 2f, PlayMaxZ - 2f));
                if (IsBlocked(position, 1.5f) || (position.magnitude < 12f && random.NextDouble() < 0.6))
                    continue;
                PlaceTree(level, random, position, collider: true);
                blockedCircles.Add(new Vector3(position.x, position.y, 1.2f));
                placed++;
            }

            // За границей и на хребте — без коллайдеров, это фон.
            for (int placedOutside = 0, attempt = 0; placedOutside < 110 && attempt < 5000; attempt++)
            {
                var position = new Vector2(Range(random, PlayMinX - 22f, PlayMaxX + 22f), Range(random, PlayMinZ - 22f, PlayMaxZ + 22f));
                bool outside = position.x < PlayMinX - 3f || position.x > PlayMaxX + 3f || position.y < PlayMinZ - 3f || position.y > PlayMaxZ + 3f;
                // Мёртвый лес и на макушке хребта — силуэтом над тоннелем.
                bool onRidge = Ridge.Contains(position) && RidgeHeight(position.x, position.y) > 7f;
                if (!outside && !onRidge)
                    continue;
                PlaceTree(level, random, position, collider: false);
                placedOutside++;
            }
        }

        private static void PlaceTree(Transform level, System.Random random, Vector2 position, bool collider)
        {
            var kit = new MeshKit();
            float height = Models.DeadTree(kit, random);
            var tree = AddModel(level, "Dead Tree", kit, OnGround(position.x, position.y), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f));
            if (collider)
            {
                var capsule = tree.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, height / 2f, 0f);
                capsule.height = height;
                capsule.radius = 0.22f;
            }
            MarkStatic(tree);
        }

        /// <summary>Мелочь одним мешем: сухая трава, мусор, обломки. Только крупные обломки твёрдые.</summary>
        private static void BuildScatter(Transform level)
        {
            var random = new System.Random(8);
            var kit = new MeshKit();

            // Трава гуще там, где земля и так травяная (те же пятна шума, что в GroundSwatch): заросли, а не посыпка.
            for (int i = 0; i < 12000; i++)
            {
                // Почти вся трава в игровой зоне, немного — на склонах за ней.
                float pad = i % 6 == 0 ? 12f : 0f;
                var position = new Vector2(Range(random, PlayMinX - pad, PlayMaxX + pad), Range(random, PlayMinZ - pad, PlayMaxZ + pad));
                if (IsInsideBuilding(position) || Mathf.Abs(position.y - RoadZ) < 3.2f || IsInTunnelSlot(position))
                    continue;
                float patch = Mathf.PerlinNoise(position.x * 0.07f + 7f, position.y * 0.07f + 3f);
                if (random.NextDouble() > (patch > 0.55f ? 1.0 : 0.2))
                    continue;
                Models.GrassTuft(kit, random, OnGround(position.x, position.y));
            }

            for (int i = 0; i < 180; i++)
            {
                var position = new Vector2(Range(random, PlayMinX + 1f, PlayMaxX - 1f), Range(random, PlayMinZ + 1f, PlayMaxZ - 1f));
                if (IsInsideBuilding(position))
                    continue;
                Models.Debris(kit, random, OnGround(position.x, position.y));
            }

            for (int i = 0; i < 140; i++)
            {
                var position = new Vector2(Range(random, PlayMinX + 1f, PlayMaxX - 1f), Range(random, PlayMinZ + 1f, PlayMaxZ - 1f));
                if (IsBlocked(position, 1f))
                    continue;
                Models.Rubble(kit, random, OnGround(position.x, position.y), Range(random, 0.2f, 0.8f), random.NextDouble() < 0.3);
            }

            MarkStatic(AddModel(level, "Scatter", kit, Vector3.zero, Quaternion.identity, outline: false));
        }

        /// <summary>Невидимые стены по краю игровой зоны, чтобы не уходить в холмы.</summary>
        private static void BuildBoundary(Transform level)
        {
            var boundary = new GameObject("Boundary");
            boundary.transform.SetParent(level, false);
            float width = PlayMaxX - PlayMinX, length = PlayMaxZ - PlayMinZ;
            float centerX = (PlayMinX + PlayMaxX) / 2f, centerZ = (PlayMinZ + PlayMaxZ) / 2f;
            var walls = new List<(Vector3 center, Vector3 size)>
            {
                (new Vector3(centerX, 3f, PlayMaxZ), new Vector3(width, 8f, 1f)),
                (new Vector3(centerX, 3f, PlayMinZ), new Vector3(width, 8f, 1f)),
                (new Vector3(PlayMaxX, 3f, centerZ), new Vector3(1f, 8f, length)),
                (new Vector3(PlayMinX, 3f, centerZ), new Vector3(1f, 8f, length)),
            };
            // Хребет: стенки по подножию, кроме проёмов тоннеля, — чтобы не залезть на склон.
            float gapWest = TunnelX - TunnelHalfWidth - 0.5f, gapEast = TunnelX + TunnelHalfWidth + 0.5f;
            foreach (float z in new[] { Ridge.yMin, Ridge.yMax })
            {
                walls.Add((new Vector3((Ridge.xMin + gapWest) / 2f, 5f, z), new Vector3(gapWest - Ridge.xMin, 12f, 1f)));
                walls.Add((new Vector3((gapEast + Ridge.xMax) / 2f, 5f, z), new Vector3(Ridge.xMax - gapEast, 12f, 1f)));
            }
            walls.Add((new Vector3(Ridge.xMin, 5f, Ridge.center.y), new Vector3(1f, 12f, Ridge.height)));
            foreach (var (center, size) in walls)
            {
                var wall = new GameObject("Wall");
                wall.transform.SetParent(boundary.transform, false);
                wall.transform.localPosition = center;
                wall.AddComponent<BoxCollider>().size = size;
            }
            MarkStatic(boundary);
        }

        // ---------- Занятые места ----------

        private static bool IsInsideBuilding(Vector2 point)
        {
            // Первый прямоугольник — полоса дороги; постройки идут после него.
            for (int i = 1; i < blockedRects.Count; i++)
            {
                if (blockedRects[i].Contains(point))
                    return true;
            }
            return false;
        }

        private static bool IsNearSpawn(Vector2 point, float margin)
        {
            foreach (var spawns in new[] { FilterSpawns, BandageSpawns, FlashlightBatterySpawns, BlindOneSpawns, FinaleBlindOneSpawns })
            {
                foreach (var spawn in spawns)
                {
                    if (Vector2.Distance(point, new Vector2(spawn.x, spawn.z)) < margin)
                        return true;
                }
            }
            return false;
        }

        private static bool IsInTunnelSlot(Vector2 point) =>
            Mathf.Abs(point.x - TunnelX) < TunnelHalfWidth + 3f && point.y > Ridge.yMin - 1f && point.y < Ridge.yMax + 1f;

        private static bool IsBlocked(Vector2 point, float margin)
        {
            foreach (var rect in blockedRects)
            {
                var grown = new Rect(rect.x - margin, rect.y - margin, rect.width + margin * 2f, rect.height + margin * 2f);
                if (grown.Contains(point))
                    return true;
            }
            foreach (var circle in blockedCircles)
            {
                if (Vector2.Distance(point, new Vector2(circle.x, circle.y)) < circle.z + margin)
                    return true;
            }
            return false;
        }
    }
}
