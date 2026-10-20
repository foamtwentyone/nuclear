using System.Collections.Generic;
using Nuclear.Enemies;
using Nuclear.Hazards;
using Nuclear.Items;
using Nuclear.Level;
using Nuclear.Network;
using Nuclear.Player;
using Nuclear.World;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nuclear.EditorTools
{
    /// <summary>
    /// Собирает префабы игрока и предметов и серую тестовую сцену одной командой меню,
    /// чтобы не настраивать их руками и не хранить сгенерированный YAML.
    /// </summary>
    public static class PrototypeSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SceneFolder = "Assets/Scenes";
        private const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        private const string ScenePath = SceneFolder + "/Prototype.unity";
        private const string MaterialFolder = "Assets/Materials";
        private static readonly Vector3 DoorPosition = new(0f, 0f, 24f);
        private const string NetworkPrefabsPath = PrefabFolder + "/NetworkPrefabs.asset";

        [MenuItem("Nuclear/Собрать сцену прототипа")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);

            var playerPrefab = BuildPlayerPrefab();
            var items = new ItemPrefabs
            {
                Battery = BuildBatteryPrefab(),
                Crate = BuildCratePrefab(),
                Can = BuildCanPrefab(),
                Filter = BuildFilterPrefab(),
                BlindOne = BuildBlindOnePrefab(),
                Door = BuildDoorPrefab(),
            };
            BuildScene(playerPrefab, items);

            Debug.Log($"Готово: {ScenePath}. Нажми Play и выбери «Создать игру (хост)».");
        }

        private static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<NetworkObject>();

            var netTransform = root.AddComponent<OwnerNetworkTransform>();
            // Игрок поворачивается только вокруг вертикали, масштаб не меняется.
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;

            root.AddComponent<PlayerStamina>();
            root.AddComponent<PlayerController>();
            root.AddComponent<PlayerCarry>();
            root.AddComponent<PlayerSurvival>();
            root.AddComponent<Dosimeter>();
            root.AddComponent<PlayerNoise>();
            root.AddComponent<PlayerHud>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 75f;
            cameraObject.AddComponent<AudioListener>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private struct ItemPrefabs
        {
            public GameObject Battery;
            public GameObject Crate;
            public GameObject Can;
            public GameObject Filter;
            public GameObject BlindOne;
            public GameObject Door;
        }

        private static GameObject BuildBatteryPrefab()
        {
            var root = CreateItemRoot("Battery", mass: 25f, continuous: false);
            AddModel(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.6f, 0.4f, 0.4f),
                SaveMaterial("Battery", new Color(0.18f, 0.2f, 0.24f)), withCollider: true);

            var terminal = SaveMaterial("Terminal", new Color(0.7f, 0.6f, 0.35f));
            AddModel(root, PrimitiveType.Cylinder, new Vector3(-0.18f, 0.23f, 0f), new Vector3(0.07f, 0.03f, 0.07f), terminal, withCollider: false);
            AddModel(root, PrimitiveType.Cylinder, new Vector3(0.18f, 0.23f, 0f), new Vector3(0.07f, 0.03f, 0.07f), terminal, withCollider: false);

            var indicator = AddModel(root, PrimitiveType.Cube, new Vector3(0f, 0.08f, -0.205f), new Vector3(0.12f, 0.06f, 0.02f),
                SaveMaterial("Indicator", Color.green), withCollider: false);
            indicator.name = "Indicator";

            AddSparks(root, new Vector3(0f, 0.25f, 0f));

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = "Аккумулятор",
                ["maxCarriers"] = 2,
                ["holdDistance"] = 1.3f,
                ["soloHoldDrop"] = 0.7f,
                ["soloSpeedMultiplier"] = 0.45f,
                ["sharedSpeedMultiplier"] = 0.85f,
                ["canThrow"] = false,
                ["impactNoiseRadius"] = 15f,
            });
            root.AddComponent<Battery>();

            return SavePrefab(root, "Battery");
        }

        private static GameObject BuildCratePrefab()
        {
            var root = CreateItemRoot("Crate", mass: 6f, continuous: false);
            AddModel(root, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.6f,
                SaveMaterial("Crate", new Color(0.45f, 0.35f, 0.25f)), withCollider: true);

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = "Ящик",
                ["soloSpeedMultiplier"] = 0.8f,
                ["throwImpulse"] = 18f,
                ["impactNoiseRadius"] = 10f,
            });

            return SavePrefab(root, "Crate");
        }

        /// <summary>
        /// Подвижная часть входа в Бункер-12: створка, щиток, лампа и логика конца уровня.
        /// Стены здания — статичная часть уровня (BuildBunker12), чтобы попасть в навигационную сетку.
        /// </summary>
        private static GameObject BuildDoorPrefab()
        {
            var root = new GameObject("Bunker12 Door");
            root.AddComponent<NetworkObject>();

            var panel = AddModel(root, PrimitiveType.Cube, new Vector3(0f, 1.3f, 0f), new Vector3(3f, 2.6f, 0.3f),
                SaveMaterial("Door", new Color(0.3f, 0.32f, 0.3f)), withCollider: true);
            panel.name = "Panel";
            // Слепыш ходит по сетке и сквозь закрытую створку не пойдёт.
            var obstacle = panel.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true;

            var socket = AddModel(root, PrimitiveType.Cube, new Vector3(2.4f, 1.2f, -0.35f), new Vector3(0.5f, 0.7f, 0.2f),
                SaveMaterial("Socket", new Color(0.45f, 0.42f, 0.2f)), withCollider: false);
            socket.name = "Socket";

            var inserted = AddModel(root, PrimitiveType.Cube, new Vector3(2.4f, 0.75f, -0.55f), new Vector3(0.4f, 0.28f, 0.25f),
                SaveMaterial("Battery", new Color(0.18f, 0.2f, 0.24f)), withCollider: false);
            inserted.name = "Inserted Battery";
            inserted.SetActive(false);

            var lamp = AddModel(root, PrimitiveType.Sphere, new Vector3(0f, 3.75f, -0.3f), Vector3.one * 0.25f,
                SaveMaterial("Lamp", Color.red), withCollider: false);
            lamp.name = "Lamp";

            root.AddComponent<BunkerDoor>();
            root.AddComponent<LevelOutcome>();

            return SavePrefab(root, "Bunker12Door");
        }

        private static GameObject BuildBlindOnePrefab()
        {
            var root = new GameObject("BlindOne");

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.15f, 0f);
            capsule.height = 2.3f;
            capsule.radius = 0.4f;

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;

            // Высокая сутулая фигура с длинными руками и головой без глаз.
            var skin = SaveMaterial("BlindOne Skin", new Color(0.75f, 0.7f, 0.65f));
            AddModel(root, PrimitiveType.Capsule, new Vector3(0f, 1.05f, 0f), new Vector3(0.7f, 1f, 0.55f), skin, withCollider: false);
            var head = AddModel(root, PrimitiveType.Sphere, new Vector3(0f, 2.15f, 0.12f), new Vector3(0.42f, 0.5f, 0.45f), skin, withCollider: false);
            head.name = "Head";
            foreach (float side in new[] { -1f, 1f })
            {
                var arm = AddModel(root, PrimitiveType.Cube, new Vector3(0.45f * side, 1.05f, 0.15f), new Vector3(0.12f, 1.2f, 0.12f), skin, withCollider: false);
                arm.transform.localRotation = Quaternion.Euler(-15f, 0f, 8f * side);
            }

            root.AddComponent<BlindOne>();

            return SavePrefab(root, "BlindOne");
        }

        private static GameObject BuildFilterPrefab()
        {
            var root = CreateItemRoot("Filter", mass: 0.4f, continuous: true);
            AddModel(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.12f, 0.05f, 0.12f),
                SaveMaterial("Filter", new Color(0.35f, 0.4f, 0.25f)), withCollider: true);
            AddModel(root, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.015f, 0.05f),
                SaveMaterial("Terminal", new Color(0.7f, 0.6f, 0.35f)), withCollider: false);

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = "Фильтр",
                ["holdDistance"] = 1f,
                ["throwImpulse"] = 3.5f,
                ["impactNoiseRadius"] = 8f,
            });
            root.AddComponent<FilterPickup>();

            return SavePrefab(root, "Filter");
        }

        private static GameObject BuildCanPrefab()
        {
            var root = CreateItemRoot("Can", mass: 0.3f, continuous: true);
            AddModel(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.09f, 0.06f, 0.09f),
                SaveMaterial("Can", new Color(0.6f, 0.25f, 0.2f)), withCollider: true);

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = "Консерва",
                ["holdDistance"] = 1f,
                ["throwImpulse"] = 3.5f,
                ["impactNoiseRadius"] = 12f,
            });

            return SavePrefab(root, "Can");
        }

        /// <summary>Пустой корень предмета с физикой и сетью; модели и коллайдеры — дочерние.</summary>
        private static GameObject CreateItemRoot(string name, float mass, bool continuous)
        {
            var root = new GameObject(name);

            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (continuous)
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;
            root.AddComponent<NetworkRigidbody>();

            return root;
        }

        private static GameObject AddModel(GameObject root, PrimitiveType shape, Vector3 position, Vector3 size, Material material, bool withCollider)
        {
            var model = GameObject.CreatePrimitive(shape);
            model.name = "Model";
            if (!withCollider)
                Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = position;
            model.transform.localScale = size;
            model.GetComponent<Renderer>().sharedMaterial = material;
            return model;
        }

        private static void AddSparks(GameObject root, Vector3 position)
        {
            var sparksObject = new GameObject("Sparks");
            sparksObject.transform.SetParent(root.transform, false);
            sparksObject.transform.localPosition = position;

            var sparks = sparksObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = 0.04f;
            main.startColor = new Color(1f, 0.85f, 0.4f);
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            sparksObject.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        }

        /// <summary>Задаёт приватные [SerializeField] поля компонента по именам.</summary>
        private static void Configure(Object target, Dictionary<string, object> values)
        {
            var serialized = new SerializedObject(target);
            foreach (var pair in values)
            {
                var property = serialized.FindProperty(pair.Key);
                switch (pair.Value)
                {
                    case float f: property.floatValue = f; break;
                    case int i: property.intValue = i; break;
                    case bool b: property.boolValue = b; break;
                    case string text: property.stringValue = text; break;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material SaveMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Список сетевых префабов, которые может создавать сервер. Игрок регистрируется сам.</summary>
        private static NetworkPrefabsList SaveNetworkPrefabs(params GameObject[] prefabs)
        {
            AssetDatabase.DeleteAsset(NetworkPrefabsPath);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            foreach (var prefab in prefabs)
                list.Add(new NetworkPrefab { Prefab = prefab });
            AssetDatabase.CreateAsset(list, NetworkPrefabsPath);
            return list;
        }

        private static void BuildScene(GameObject playerPrefab, ItemPrefabs items)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.82f, 0.78f);
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.37f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.55f, 0.55f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 70f;

            // Камера-заглушка, пока игрок не заспавнился.
            var menuCamera = new GameObject("Menu Camera").AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = RenderSettings.fogColor;
            menuCamera.transform.SetPositionAndRotation(new Vector3(0f, 8f, -14f), Quaternion.Euler(25f, 0f, 0f));
            menuCamera.depth = -10f;

            var networkObject = new GameObject("NetworkManager");
            var manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            var transport = networkObject.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(SaveNetworkPrefabs(items.Battery, items.Crate, items.Can, items.Filter, items.BlindOne, items.Door));
            networkObject.AddComponent<ConnectionMenu>();

            // Аккумулятор один, в случайной из трёх точек; ящики и консервы рядом со стартом.
            var spawner = new GameObject("Item Spawner").AddComponent<ItemSpawner>();
            spawner.SetEntries(new[]
            {
                new ItemSpawner.Entry
                {
                    prefab = items.Battery,
                    positions = new[] { new Vector3(8f, 0.3f, -6f), new Vector3(-8f, 0.3f, 12f), new Vector3(12f, 0.3f, 10f) },
                    count = 1,
                },
                new ItemSpawner.Entry
                {
                    prefab = items.Crate,
                    positions = new[] { new Vector3(2f, 0.4f, 4f), new Vector3(3f, 0.4f, 4.5f), new Vector3(-3f, 0.4f, 3f) },
                    count = 3,
                },
                new ItemSpawner.Entry
                {
                    prefab = items.Can,
                    positions = new[]
                    {
                        new Vector3(1f, 0.2f, 2f), new Vector3(1.4f, 0.2f, 2.3f), new Vector3(-1f, 0.2f, 2.5f),
                        new Vector3(-1.5f, 0.2f, 1.8f), new Vector3(0f, 0.2f, 3f),
                    },
                    count = 5,
                },
                new ItemSpawner.Entry
                {
                    // Один из вариантов — в центре воронки: фильтр за дозу.
                    prefab = items.Filter,
                    positions = new[]
                    {
                        new Vector3(6f, 0.2f, 10f), new Vector3(-10f, 0.2f, 5f), new Vector3(10f, 0.2f, -2f),
                        new Vector3(-4f, 0.2f, -12f), new Vector3(16f, 0.2f, 4f), new Vector3(14f, 0.2f, -14f),
                    },
                    count = 3,
                },
                new ItemSpawner.Entry
                {
                    // Слепыш начинает в дальней части карты, подальше от шлюза.
                    prefab = items.BlindOne,
                    positions = new[] { new Vector3(-8f, 0f, 20f), new Vector3(-18f, 0f, 16f), new Vector3(20f, 0f, 14f) },
                    count = 1,
                },
                new ItemSpawner.Entry
                {
                    prefab = items.Door,
                    positions = new[] { DoorPosition },
                    count = 1,
                    randomYaw = false,
                },
            });

            BuildGrayLevel();
            BuildHazards();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Серая площадка из кубов: ящики, стена, пандус и ступени для проверки движения.</summary>
        private static void BuildGrayLevel()
        {
            var level = new GameObject("Level").transform;

            Block(level, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));

            Block(level, "Crate A", new Vector3(5f, 0.5f, 6f), Vector3.one);
            Block(level, "Crate B", new Vector3(6.2f, 0.5f, 6.3f), Vector3.one);
            Block(level, "Crate C", new Vector3(5.6f, 1.5f, 6.1f), Vector3.one);

            Block(level, "Wall", new Vector3(-6f, 1.5f, 8f), new Vector3(8f, 3f, 0.5f));

            var ramp = Block(level, "Ramp", new Vector3(0f, 1f, 14f), new Vector3(4f, 0.3f, 8f));
            ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);

            // Ступени по 25 см: CharacterController перешагивает их без прыжка.
            for (int i = 0; i < 8; i++)
            {
                float height = 0.25f * (i + 1);
                Block(level, $"Step {i}", new Vector3(-10f, height / 2f, -4f + i * 0.6f), new Vector3(3f, height, 0.6f));
            }

            // Сарай без двери: укрытие, чтобы проверить, как стены глушат звук для Слепыша.
            Block(level, "Shed Back", new Vector3(8f, 1.25f, 20f), new Vector3(5f, 2.5f, 0.3f));
            Block(level, "Shed Left", new Vector3(5.65f, 1.25f, 18f), new Vector3(0.3f, 2.5f, 4f));
            Block(level, "Shed Right", new Vector3(10.35f, 1.25f, 18f), new Vector3(0.3f, 2.5f, 4f));
            Block(level, "Shed Roof", new Vector3(8f, 2.6f, 18f), new Vector3(5f, 0.2f, 4.3f));

            BuildBunker12(level);

            // Навигационная сетка для Слепыша строится по коллайдерам уровня при загрузке.
            var surface = level.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            level.gameObject.AddComponent<RuntimeNavMesh>();
        }

        /// <summary>
        /// Здание Бункера-12 в дальнем конце карты: фасад с проёмом под дверь и комната за ним.
        /// Внутри чистый воздух и зона победы.
        /// </summary>
        private static void BuildBunker12(Transform level)
        {
            var bunker = new GameObject("Bunker-12").transform;
            bunker.SetParent(level, false);
            bunker.localPosition = DoorPosition;

            Block(bunker, "Facade Left", new Vector3(-3.75f, 1.75f, 0f), new Vector3(4.5f, 3.5f, 0.5f));
            Block(bunker, "Facade Right", new Vector3(3.75f, 1.75f, 0f), new Vector3(4.5f, 3.5f, 0.5f));
            Block(bunker, "Lintel", new Vector3(0f, 3.05f, 0f), new Vector3(3f, 0.9f, 0.5f));
            Block(bunker, "Room Left", new Vector3(-3f, 1.75f, 3f), new Vector3(0.3f, 3.5f, 5.5f));
            Block(bunker, "Room Right", new Vector3(3f, 1.75f, 3f), new Vector3(0.3f, 3.5f, 5.5f));
            Block(bunker, "Room Back", new Vector3(0f, 1.75f, 5.75f), new Vector3(6f, 3.5f, 0.3f));
            Block(bunker, "Room Roof", new Vector3(0f, 3.6f, 3f), new Vector3(6.4f, 0.2f, 6f));

            var goal = new GameObject("Goal (inside Bunker-12)");
            goal.transform.SetParent(bunker, false);
            goal.transform.localPosition = new Vector3(0f, 1.5f, 3.2f);
            goal.AddComponent<GoalZone>().Configure(new Vector3(5.4f, 3f, 4.5f));

            var air = new GameObject("Bunker-12 (safe air)");
            air.transform.SetParent(bunker, false);
            air.transform.localPosition = new Vector3(0f, 1.75f, 3f);
            air.AddComponent<SafeAirZone>().Configure(new Vector3(5.6f, 3.5f, 5f));
        }

        /// <summary>Шлюз бункера с чистым воздухом у старта и два радиоактивных пятна.</summary>
        private static void BuildHazards()
        {
            var hazards = new GameObject("Hazards").transform;

            var airlock = new GameObject("Airlock (safe air)");
            airlock.transform.SetParent(hazards, false);
            airlock.transform.localPosition = new Vector3(0f, 2f, 0f);
            var airlockSize = new Vector3(10f, 5f, 10f);
            airlock.AddComponent<SafeAirZone>().Configure(airlockSize);
            Marker(hazards, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(airlockSize.x, 0.02f, airlockSize.z),
                SaveMaterial("Airlock Floor", new Color(0.3f, 0.36f, 0.42f)));

            var radiationMaterial = SaveMaterial("Radiation", new Color(0.55f, 0.6f, 0.2f));
            RadiationSpot(hazards, "Crater", new Vector3(14f, 0f, -14f), radius: 9f, intensity: 4f, radiationMaterial);
            RadiationSpot(hazards, "Weak Spot", new Vector3(-14f, 0f, -10f), radius: 6f, intensity: 1.5f, radiationMaterial);
        }

        private static void RadiationSpot(Transform parent, string name, Vector3 position, float radius, float intensity, Material material)
        {
            var zone = new GameObject($"{name} (radiation)");
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = position + Vector3.up;
            zone.AddComponent<RadiationZone>().Configure(radius, intensity);

            // Пятно на земле на всю зону, чтобы её было видно до появления настоящей графики.
            Marker(parent, PrimitiveType.Cylinder, position + Vector3.up * 0.015f, new Vector3(radius * 2f, 0.01f, radius * 2f), material);
        }

        /// <summary>Плоская цветная метка на земле без коллайдера.</summary>
        private static void Marker(Transform parent, PrimitiveType shape, Vector3 position, Vector3 size, Material material)
        {
            var marker = GameObject.CreatePrimitive(shape);
            marker.name = "Marker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
            marker.transform.localScale = size;
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.isStatic = true;
            return block;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
