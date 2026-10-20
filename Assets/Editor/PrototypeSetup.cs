using System.Collections.Generic;
using Nuclear.Items;
using Nuclear.Network;
using Nuclear.Player;
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
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(SaveNetworkPrefabs(items.Battery, items.Crate, items.Can));
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
            });

            BuildGrayLevel();

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
