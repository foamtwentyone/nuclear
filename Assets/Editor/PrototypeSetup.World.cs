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
    /// Мир уровня 1 «Бункер-7 → Бункер-12»: заброшенный пригород под пеплом.
    /// Шлюз Бункера-7 в центре, дорога с брошенными машинами, дачи, воронка, Бункер-12 на севере.
    /// Всё статичное собирается из low-poly моделей; за границей игровой зоны — холмы в тумане.
    /// </summary>
    public static partial class PrototypeSetup
    {
        private const float RoadZ = 10f;
        private const float PlayAreaHalf = 31f;
        private static readonly Vector3 DoorPosition = new(0f, 0f, 24f);
        private static readonly Vector2 CraterCenter = new(14f, -14f);
        private static readonly Vector2 WeakSpotCenter = new(-14f, -10f);

        // Точки появления предметов и Слепыша: рядом с ними ничего не ставим.
        private static readonly Vector3[] BatterySpawns = { new(8f, 0.3f, -6f), new(-8f, 0.3f, 12f), new(12f, 0.3f, 10f) };
        private static readonly Vector3[] CrateSpawns = { new(2f, 0.4f, 4f), new(3f, 0.4f, 4.5f), new(-3f, 0.4f, 3f) };
        private static readonly Vector3[] CanSpawns = { new(1f, 0.2f, 2f), new(1.4f, 0.2f, 2.3f), new(-1f, 0.2f, 2.5f), new(-1.5f, 0.2f, 1.8f), new(0f, 0.2f, 3f) };
        private static readonly Vector3[] FilterSpawns = { new(6f, 0.2f, 10f), new(-10f, 0.2f, 5f), new(10f, 0.2f, -2f), new(-4f, 0.2f, -12f), new(16f, 0.2f, 4f), new(14f, 0.2f, -14f) };
        // Стартовое снаряжение на столе в шлюзе Бункера-7: на всех не хватит, придётся делить.
        private static readonly Vector3[] TableBandages = { new(-3.45f, 0.86f, -2.1f), new(-3.25f, 0.86f, -2.2f) };
        private static readonly Vector3[] TableFlashlights = { new(-2.85f, 0.86f, -1.85f), new(-2.6f, 0.86f, -1.9f) };
        private static readonly Vector3[] TableCrowbar = { new(-3f, 0.84f, -2.28f) };
        private static readonly Vector3[] TableBatteries = { new(-2.45f, 0.84f, -2.25f), new(-2.38f, 0.84f, -2.15f) };
        // По дороге: ещё бинты и батарейки в случайных местах.
        private static readonly Vector3[] BandageSpawns = { new(7f, 0.2f, 9f), new(-9f, 0.2f, 6f), new(11f, 0.2f, -3f), new(-6f, 0.2f, 14f) };
        private static readonly Vector3[] FlashlightBatterySpawns = { new(5.5f, 0.2f, 13.5f), new(-11f, 0.2f, 3f), new(7f, 0.2f, -10.5f), new(15f, 0.2f, 6f) };
        private static readonly Vector3[] BlindOneSpawns = { new(-8f, 0f, 20f), new(-18f, 0f, 16f), new(20f, 0f, 14f) };

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
            menuCamera.transform.SetPositionAndRotation(new Vector3(-9f, 4.5f, 17f), Quaternion.Euler(14f, 150f, 0f));
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
                items.Gear.Bandage, items.Gear.Flashlight, items.Gear.Crowbar, items.Gear.Battery));
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
            // Аккумулятор один, в случайной из трёх точек у машин; ящики и консервы в шлюзе.
            var spawner = new GameObject("Item Spawner").AddComponent<ItemSpawner>();
            spawner.SetEntries(new[]
            {
                new ItemSpawner.Entry { prefab = items.Battery, positions = BatterySpawns, count = 1 },
                new ItemSpawner.Entry { prefab = items.Crate, positions = CrateSpawns, count = 3 },
                new ItemSpawner.Entry { prefab = items.Can, positions = CanSpawns, count = 5 },
                // Один из вариантов — в центре воронки: фильтр за дозу.
                new ItemSpawner.Entry { prefab = items.Filter, positions = FilterSpawns, count = 3 },
                // Слепыш начинает в дальней части карты, подальше от шлюза.
                new ItemSpawner.Entry { prefab = items.BlindOne, positions = BlindOneSpawns, count = 1 },
                new ItemSpawner.Entry { prefab = items.Door, positions = new[] { DoorPosition }, count = 1, randomYaw = false },
                new ItemSpawner.Entry { prefab = items.Gear.Bandage, positions = TableBandages, count = TableBandages.Length },
                new ItemSpawner.Entry { prefab = items.Gear.Flashlight, positions = TableFlashlights, count = TableFlashlights.Length },
                new ItemSpawner.Entry { prefab = items.Gear.Crowbar, positions = TableCrowbar, count = 1, randomYaw = false },
                new ItemSpawner.Entry { prefab = items.Gear.Battery, positions = TableBatteries, count = TableBatteries.Length },
                new ItemSpawner.Entry { prefab = items.Gear.Bandage, positions = BandageSpawns, count = 2 },
                new ItemSpawner.Entry { prefab = items.Gear.Battery, positions = FlashlightBatterySpawns, count = 2 },
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
            foreach (var spawns in new[] { BatterySpawns, CrateSpawns, CanSpawns, FilterSpawns, BandageSpawns, FlashlightBatterySpawns, BlindOneSpawns })
            {
                foreach (var point in spawns)
                    blockedCircles.Add(new Vector3(point.x, point.z, 1.8f));
            }
            blockedRects.Add(new Rect(-PlayAreaHalf - 20f, RoadZ - 3.5f, PlayAreaHalf * 2f + 40f, 7f));

            BuildGround(level);
            BuildRoad(level);
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

            float flat = Mathf.InverseLerp(3.5f, 5f, Mathf.Abs(z - RoadZ));
            flat = Mathf.Min(flat, RectFalloff(x, z, new Rect(-7f, -7f, 14f, 14f), 1.5f));
            flat = Mathf.Min(flat, RectFalloff(x, z, new Rect(-8f, 21f, 16f, 10f), 1.5f));
            h *= flat;

            float crater = Vector2.Distance(new Vector2(x, z), CraterCenter);
            if (crater < 7f)
                h -= 1.1f * (1f - crater * crater / 49f);
            h += 0.35f * Mathf.Exp(-Mathf.Pow((crater - 7.5f) / 1.2f, 2f));

            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (edge > 30f)
                h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 46f, edge)) * (5f + 4f * Mathf.PerlinNoise(x * 0.05f, z * 0.05f));

            return h;
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
            const float extent = 60f;
            const float cell = 1.25f;
            int cells = Mathf.RoundToInt(extent * 2f / cell);
            var random = new System.Random(1);
            var kit = new MeshKit();

            for (int ix = 0; ix < cells; ix++)
            {
                for (int iz = 0; iz < cells; iz++)
                {
                    float x0 = -extent + ix * cell, z0 = -extent + iz * cell;
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
            if (crater < 7.5f)
                return roll < 0.6 ? Swatch.Scorched : Swatch.ScorchedLight;
            if (crater < 10f)
                return roll < 0.6 ? Swatch.AshDark : Swatch.Scorched;
            if (Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.z)) > 32f)
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
            int segments = Mathf.RoundToInt(120f / step);
            var near = new float[segments + 1];
            var far = new float[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                near[i] = RoadZ - 3f + Range(random, -0.25f, 0.25f);
                far[i] = RoadZ + 3f + Range(random, -0.25f, 0.25f);
            }

            for (int i = 0; i < segments; i++)
            {
                float x0 = -60f + i * step, x1 = x0 + step;
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

            blockedRects.Add(new Rect(-8f, 20f, 16f, 11f));
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

            // Дома к северу от дороги смотрят дверью на юг (на дорогу), к югу — на север.
            PlaceHouse(level, "Dacha 1", new Vector2(-15f, -1f), 180f, standard, 101);
            PlaceHouse(level, "Dacha 2", new Vector2(19f, -1f), 180f, standard, 102);
            PlaceHouse(level, "Dacha 3", new Vector2(-17f, 20f), 0f, standard, 103);
            PlaceHouse(level, "Dacha 4", new Vector2(19f, 20f), 0f, standard, 104);
            PlaceHouse(level, "Dacha 5", new Vector2(-9f, -17f), 180f, standard, 105);
            // Сарай без передней стены: укрытие, чтобы прятаться от Слепыша.
            PlaceHouse(level, "Shed", new Vector2(8f, 18f), 0f, shed, 106);
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
            // Машины у трёх точек появления аккумулятора: «он в одной из машин».
            PlaceCar(level, new Vector2(-8f, 9f), 90f, Swatch.PaintRed, 201);
            PlaceCar(level, new Vector2(13.5f, 8.5f), 80f, Swatch.PaintBlue, 202);
            PlaceCar(level, new Vector2(10.5f, -8f), 20f, Swatch.PaintBeige, 203);
            PlaceCar(level, new Vector2(-21f, 11f), 100f, Swatch.PaintGreen, 204);
            PlaceCar(level, new Vector2(3f, 11.5f), 260f, Swatch.PaintBeige, 205);
            PlaceCar(level, new Vector2(24f, 9.5f), 95f, Swatch.PaintRed, 206);
        }

        private static void PlaceCar(Transform level, Vector2 position, float yaw, Swatch paint, int seed)
        {
            var kit = new MeshKit();
            Models.Car(kit, new System.Random(seed), paint);
            MarkStatic(AddModel(level, $"Car {seed - 200}", kit, OnGround(position.x, position.y), Quaternion.Euler(0f, yaw, 0f)));
            blockedCircles.Add(new Vector3(position.x, position.y, 3f));
        }

        /// <summary>Столбы ЛЭП вдоль дороги с провисшими проводами.</summary>
        private static void BuildPoles(Transform level)
        {
            var random = new System.Random(3);
            var wires = new MeshKit();
            Vector3[] previous = null;
            foreach (float x in new[] { -38f, -26f, -14f, -2f, 10f, 22f, 34f })
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
            // Линии: начало по X, конец по X, Z линии, X калитки.
            var lines = new[]
            {
                new Vector4(-21f, -11f, 2.5f, -15f),
                new Vector4(13f, 25f, 2.5f, 19f),
                new Vector4(-23f, -11f, 16.8f, -17f),
                new Vector4(13f, 25f, 16.8f, 19f),
            };

            foreach (var line in lines)
            {
                for (float x = line.x; x + segment <= line.y + 0.01f; x += segment)
                {
                    float middle = x + segment / 2f;
                    if (Mathf.Abs(middle - line.w) < 1.2f || IsNearSpawn(new Vector2(middle, line.z), 1.6f))
                        continue;
                    kit.Push(OnGround(x, line.z), Quaternion.identity);
                    Models.FenceSegment(kit, random, segment);
                    kit.Pop();
                }
            }
            MarkStatic(AddModel(level, "Fences", kit, Vector3.zero, Quaternion.identity));
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
            AddPointLight(level, "Crater Glow", craterBottom + Vector3.up * 1.2f, new Color(0.5f, 1f, 0.3f), 1.3f, 8f);

            var rim = new MeshKit();
            for (int i = 0; i < 14; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float radius = Range(random, 6.5f, 9.5f);
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
            AddRadiationZone(zones, "Crater (radiation)", new Vector3(CraterCenter.x, 1f, CraterCenter.y), 9f, 4f);
            AddRadiationZone(zones, "Weak Spot (radiation)", new Vector3(WeakSpotCenter.x, 1f, WeakSpotCenter.y), 6f, 1.5f);

            blockedCircles.Add(new Vector3(CraterCenter.x, CraterCenter.y, 6.5f));
            blockedCircles.Add(new Vector3(WeakSpotCenter.x, WeakSpotCenter.y, 3f));
        }

        /// <summary>
        /// Бочки с огнём: тёплые островки света в сером мире и ориентиры (у выхода из Бункера-7 и у Бункера-12).
        /// </summary>
        private static void BuildFireBarrels(Transform level)
        {
            var random = new System.Random(15);
            var ember = SaveMaterial("Ember", new Color(1f, 0.45f, 0.15f), emission: new Color(2.4f, 0.9f, 0.25f));
            foreach (var spot in new[] { new Vector2(-4.6f, 6.4f), new Vector2(4.9f, 21.3f) })
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
                light.gameObject.AddComponent<LightFlicker>();

                blockedCircles.Add(new Vector3(spot.x, spot.y, 1.5f));
            }
        }

        /// <summary>Пламя (яркие язычки, тают к верху) и дым над ним.</summary>
        private static void AddFire(Transform parent, Vector3 position)
        {
            var flames = NewParticles(parent, "Flames", position);
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startColor = new Color(1f, 0.75f, 0.3f, 0.9f);
            var emission = flames.emission;
            emission.rateOverTime = 30f;
            var shape = flames.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.18f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = flames.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var color = flames.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.5f), new GradientColorKey(new Color(0.4f, 0.1f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var smoke = NewParticles(parent, "Smoke", position + Vector3.up * 0.4f);
            var smokeMain = smoke.main;
            smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            smokeMain.startColor = new Color(0.25f, 0.24f, 0.23f, 0.35f);
            var smokeEmission = smoke.emission;
            smokeEmission.rateOverTime = 5f;
            var smokeShape = smoke.shape;
            smokeShape.shapeType = ParticleSystemShapeType.Cone;
            smokeShape.angle = 12f;
            smokeShape.radius = 0.2f;
            smokeShape.rotation = new Vector3(-90f, 0f, 0f);
            var smokeSize = smoke.sizeOverLifetime;
            smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.5f));
            var smokeColor = smoke.colorOverLifetime;
            smokeColor.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            smokeColor.color = fade;
        }

        private static ParticleSystem NewParticles(Transform parent, string name, Vector3 position)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            var particles = holder.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            holder.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial();
            return particles;
        }

        private static void AddRadiationZone(Transform parent, string name, Vector3 position, float radius, float intensity)
        {
            var zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = position;
            zone.AddComponent<RadiationZone>().Configure(radius, intensity);
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
            for (int attempt = 0; attempt < 400 && placed < 34; attempt++)
            {
                var position = new Vector2(Range(random, -29f, 29f), Range(random, -29f, 29f));
                if (IsBlocked(position, 1.5f) || (position.magnitude < 12f && random.NextDouble() < 0.6))
                    continue;
                PlaceTree(level, random, position, collider: true);
                blockedCircles.Add(new Vector3(position.x, position.y, 1.2f));
                placed++;
            }

            // За границей: квадратное кольцо, чтобы и по диагонали деревья не попали в игровую зону.
            for (int i = 0; i < 40; i++)
            {
                var position = new Vector2(Range(random, -50f, 50f), Range(random, -50f, 50f));
                if (Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.y)) < 34f)
                {
                    i--;
                    continue;
                }
                PlaceTree(level, random, position, collider: false);
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
            for (int i = 0; i < 3600; i++)
            {
                // Почти вся трава в игровой зоне, немного — на склонах за ней.
                float extent = i % 6 == 0 ? 45f : 31f;
                var position = new Vector2(Range(random, -extent, extent), Range(random, -extent, extent));
                if (IsInsideBuilding(position) || Mathf.Abs(position.y - RoadZ) < 3.2f)
                    continue;
                float patch = Mathf.PerlinNoise(position.x * 0.07f + 7f, position.y * 0.07f + 3f);
                if (random.NextDouble() > (patch > 0.55f ? 1.0 : 0.2))
                    continue;
                Models.GrassTuft(kit, random, OnGround(position.x, position.y));
            }

            for (int i = 0; i < 40; i++)
            {
                var position = new Vector2(Range(random, -29f, 29f), Range(random, -29f, 29f));
                if (IsInsideBuilding(position))
                    continue;
                Models.Debris(kit, random, OnGround(position.x, position.y));
            }

            for (int i = 0; i < 30; i++)
            {
                var position = new Vector2(Range(random, -29f, 29f), Range(random, -29f, 29f));
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
            foreach (var (center, size) in new[]
            {
                (new Vector3(0f, 3f, PlayAreaHalf), new Vector3(PlayAreaHalf * 2f, 8f, 1f)),
                (new Vector3(0f, 3f, -PlayAreaHalf), new Vector3(PlayAreaHalf * 2f, 8f, 1f)),
                (new Vector3(PlayAreaHalf, 3f, 0f), new Vector3(1f, 8f, PlayAreaHalf * 2f)),
                (new Vector3(-PlayAreaHalf, 3f, 0f), new Vector3(1f, 8f, PlayAreaHalf * 2f)),
            })
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
            foreach (var spawns in new[] { BatterySpawns, FilterSpawns, BandageSpawns, FlashlightBatterySpawns, BlindOneSpawns })
            {
                foreach (var spawn in spawns)
                {
                    if (Vector2.Distance(point, new Vector2(spawn.x, spawn.z)) < margin)
                        return true;
                }
            }
            return false;
        }

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
