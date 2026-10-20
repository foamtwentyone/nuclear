using Nuclear.Network;
using Nuclear.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nuclear.EditorTools
{
    /// <summary>
    /// Собирает префаб игрока и серую тестовую сцену одной командой меню,
    /// чтобы не настраивать их руками и не хранить сгенерированный YAML.
    /// </summary>
    public static class PrototypeSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SceneFolder = "Assets/Scenes";
        private const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        private const string ScenePath = SceneFolder + "/Prototype.unity";

        [MenuItem("Nuclear/Собрать сцену прототипа")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);

            var playerPrefab = BuildPlayerPrefab();
            BuildScene(playerPrefab);

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
            root.AddComponent<StaminaHud>();

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

        private static void BuildScene(GameObject playerPrefab)
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
            networkObject.AddComponent<ConnectionMenu>();

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
