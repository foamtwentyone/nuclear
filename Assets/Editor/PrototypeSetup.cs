using System;
using System.Collections.Generic;
using Nuclear.EditorTools.LowPoly;
using Nuclear.Items;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nuclear.EditorTools
{
    /// <summary>
    /// Собирает префабы, материалы, low-poly модели и сцену прототипа одной командой меню,
    /// чтобы не настраивать их руками и не хранить сгенерированный YAML.
    /// Здесь общее: меню, материалы, сохранение ассетов. Префабы — в PrototypeSetup.Prefabs.cs,
    /// мир — в PrototypeSetup.World.cs.
    /// </summary>
    public static partial class PrototypeSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SceneFolder = "Assets/Scenes";
        private const string MaterialFolder = "Assets/Materials";
        private const string GeneratedFolder = "Assets/Generated";
        private const string MeshFolder = GeneratedFolder + "/Meshes";
        private const string ScenePath = SceneFolder + "/Prototype.unity";
        private const string NetworkPrefabsPath = PrefabFolder + "/NetworkPrefabs.asset";

        /// <summary>Цвет пепельной дымки: им же заливается фон камер, чтобы горизонт растворялся в тумане.</summary>
        private static readonly Color FogColor = new(0.66f, 0.62f, 0.55f);

        private const string SurfaceMaterialFolder = MaterialFolder + "/Surfaces";
        private const string TextureFolder = GeneratedFolder + "/Textures";

        /// <summary>Материал с процедурной текстурой для каждого цвета палитры.</summary>
        private static readonly Dictionary<Swatch, Material> surfaceMaterials = new();
        private static Material glowMaterial;
        private static Material ghostMaterial;
        private static Material outlineMaterial;

        [MenuItem("Nuclear/Собрать сцену прототипа")]
        public static void Build()
        {
            // Во время игры Unity не даёт сохранять сцены и ассеты — сборка упадёт на полпути.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Nuclear", "Сначала остановите игру (кнопка Play), потом соберите сцену.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(GeneratedFolder);
            // Меши пересоздаются целиком: старые ассеты удаляем, чтобы не копились.
            AssetDatabase.DeleteAsset(MeshFolder);
            EnsureFolder(MeshFolder);

            CreateSharedMaterials();

            var gear = new GearPrefabs
            {
                Bandage = BuildGearPrefab("Bandage", "Бинт", GearKind.Bandage, Models.Bandage, new Vector3(0.09f, 0.08f, 0.08f), 0.1f),
                Flashlight = BuildGearPrefab("Flashlight", "Фонарь", GearKind.Flashlight, Models.Flashlight, new Vector3(0.07f, 0.07f, 0.2f), 0.4f),
                Crowbar = BuildGearPrefab("Crowbar", "Лом", GearKind.Crowbar, Models.Crowbar, new Vector3(0.8f, 0.1f, 0.04f), 1.5f),
                Battery = BuildGearPrefab("Flashlight Battery", "Батарейка", GearKind.FlashlightBattery, Models.FlashlightBattery, new Vector3(0.065f, 0.034f, 0.034f), 0.05f),
            };
            var playerPrefab = BuildPlayerPrefab(gear);
            var items = new ItemPrefabs
            {
                Gear = gear,
                Battery = BuildBatteryPrefab(),
                Crate = BuildCratePrefab(),
                Can = BuildCanPrefab(),
                Filter = BuildFilterPrefab(),
                BlindOne = BuildBlindOnePrefab(),
                Door = BuildDoorPrefab(),
            };
            items.Hoods = new GameObject[HoodCars.Length];
            for (int i = 0; i < HoodCars.Length; i++)
                items.Hoods[i] = BuildCarHoodPrefab(HoodCars[i].Paint, HoodCars[i].Seed, items, gear);
            BuildScene(playerPrefab, items);
            AssetDatabase.SaveAssets();

            Debug.Log($"Готово: {ScenePath}. Нажми Play и выбери «Создать игру (хост)».");
        }

        private struct ItemPrefabs
        {
            public GameObject Battery;
            public GameObject Crate;
            public GameObject Can;
            public GameObject Filter;
            public GameObject BlindOne;
            public GameObject Door;
            public GearPrefabs Gear;
            public GameObject[] Hoods;
        }

        private struct GearPrefabs
        {
            public GameObject Bandage;
            public GameObject Flashlight;
            public GameObject Crowbar;
            public GameObject Battery;
        }

        // ---------- Материалы ----------

        /// <summary>Активен ли URP: смотрим и на уровень качества, и на общий конвейер.</summary>
        private static bool UsingUrp
        {
            get
            {
                var pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline : GraphicsSettings.defaultRenderPipeline;
                return pipeline != null && pipeline.GetType().Name.Contains("Universal");
            }
        }

        private static Shader LitShader
        {
            get
            {
                var shader = Shader.Find(UsingUrp ? "Universal Render Pipeline/Lit" : "Standard");
                if (shader == null)
                    Debug.LogError("Не найден шейдер для текущего конвейера рендера — материалы будут розовыми.");
                return shader;
            }
        }

        /// <summary>
        /// Мультяшный шейдер (Assets/Shaders/Toon.shader) — только под URP. Если его нет или он не
        /// скомпилировался, поверхности остаются на URP Lit, чтобы не было розовых материалов.
        /// </summary>
        private static Shader SurfaceShader(bool emissive)
        {
            if (!UsingUrp || emissive)
                return LitShader;
            var toon = Shader.Find("Nuclear/Toon");
            if (toon == null || ShaderUtil.ShaderHasError(toon))
            {
                Debug.LogWarning("Шейдер Nuclear/Toon недоступен — поверхности будут на URP Lit.");
                return LitShader;
            }
            return toon;
        }

        /// <summary>
        /// Текстуры и материалы поверхностей: карта нормалей одна на тип поверхности,
        /// цветная текстура и материал — на каждый цвет палитры.
        /// </summary>
        private static void CreateSharedMaterials()
        {
            EnsureFolder(SurfaceMaterialFolder);
            EnsureFolder(TextureFolder);

            var normals = new Dictionary<Surface, Texture2D>();
            foreach (Surface surface in Enum.GetValues(typeof(Surface)))
                normals[surface] = SaveTexture($"{TextureFolder}/{surface} Normal.asset", Surfaces.NormalPixels(surface), Surfaces.Resolution(surface), linear: true);

            surfaceMaterials.Clear();
            foreach (Swatch swatch in Enum.GetValues(typeof(Swatch)))
            {
                var surface = Surfaces.Of(swatch);
                var albedo = SaveTexture($"{TextureFolder}/{swatch}.asset", Surfaces.AlbedoPixels(swatch), Surfaces.Resolution(surface), linear: false);
                surfaceMaterials[swatch] = SaveMaterial($"Surfaces/{swatch}", Color.white, texture: albedo, smoothness: Surfaces.Smoothness(swatch), normalMap: normals[surface]);
            }

            glowMaterial = SaveMaterial("Toxic Glow", new Color(0.55f, 1f, 0.35f), emission: new Color(0.6f, 1.6f, 0.3f));
            ghostMaterial = SaveMaterial("Ghost", new Color(0.75f, 0.88f, 1f), emission: new Color(0.55f, 0.8f, 1.3f));
            outlineMaterial = SaveOutlineMaterial();
        }

        /// <summary>Материал мультяшного контура (Assets/Shaders/Outline.shader). Без шейдера контуров просто не будет.</summary>
        private static Material SaveOutlineMaterial()
        {
            var shader = Shader.Find("Nuclear/Outline");
            if (shader == null)
            {
                Debug.LogWarning("Шейдер Nuclear/Outline не найден — контуры не добавлены.");
                return null;
            }

            string path = $"{MaterialFolder}/Outline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            // Толщина задаётся здесь, а не в инспекторе: материал пересоздаётся при каждой сборке.
            material.SetFloat("_Width", 0.005f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D SaveTexture(string path, Color[] pixels, int size, bool linear)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            texture.SetPixels(pixels);
            texture.Apply(true);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        /// <summary>
        /// Материал под текущий конвейер рендера (встроенный или URP). Если ассет уже есть,
        /// он обновляется — так материалы переключаются на URP после его включения.
        /// </summary>
        private static Material SaveMaterial(string name, Color color, Color? emission = null, Texture texture = null, float smoothness = 0.1f, Texture normalMap = null)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = SurfaceShader(emission.HasValue);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;
            material.mainTexture = texture;
            foreach (string property in new[] { "_Glossiness", "_Smoothness" })
            {
                if (material.HasProperty(property))
                    material.SetFloat(property, smoothness);
            }

            // Рельеф: карта нормалей. Свойство и ключевое слово одинаковые в Standard и URP Lit.
            material.SetTexture("_BumpMap", normalMap);
            if (normalMap != null)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");

            // Матовые поверхности — без «пластиковых» бликов и отражений неба; металл, стекло и краска блестят.
            // Свойства и ключевые слова одинаковые в Standard и URP Lit, кроме отражений.
            bool matte = smoothness < 0.2f;
            material.SetFloat("_SpecularHighlights", matte ? 0f : 1f);
            material.SetFloat("_GlossyReflections", matte ? 0f : 1f);
            material.SetFloat("_EnvironmentReflections", matte ? 0f : 1f);
            foreach (string keyword in new[] { "_SPECULARHIGHLIGHTS_OFF", "_GLOSSYREFLECTIONS_OFF", "_ENVIRONMENTREFLECTIONS_OFF" })
            {
                if (matte)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Стандартный материал частиц текущего конвейера (мягкий кружок, цвет задаёт система частиц).</summary>
        private static Material ParticleMaterial()
        {
            if (UsingUrp)
            {
                var urp = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
                if (urp != null)
                    return urp;
            }
            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        }

        // ---------- Меши и объекты ----------

        /// <summary>Сохраняет собранный меш ассетом: префабы и сцена ссылаются на него, а не хранят копию.</summary>
        private static Mesh SaveMesh(MeshKit kit, string name, out Swatch[] swatches)
        {
            var mesh = kit.ToMesh(name, out swatches);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/{name}.asset");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Дочерний объект с low-poly моделью и её коллайдерами-коробками.
        /// outline — добавить мультяшный контур (не нужен земле, дороге, траве и светящимся деталям).
        /// </summary>
        private static GameObject AddModel(Transform parent, string name, MeshKit kit, Vector3 position, Quaternion rotation, Material material = null, float scale = 1f, bool outline = true)
        {
            var model = new GameObject(name);
            model.transform.SetParent(parent, false);
            model.transform.localPosition = position;
            model.transform.localRotation = rotation;
            model.transform.localScale = Vector3.one * scale;
            model.AddComponent<MeshFilter>().sharedMesh = SaveMesh(kit, name, out var swatches);
            // По материалу на подсетку; переданный материал (свечение) заменяет все.
            var materials = new Material[swatches.Length];
            for (int i = 0; i < swatches.Length; i++)
                materials[i] = material != null ? material : surfaceMaterials[swatches[i]];
            model.AddComponent<MeshRenderer>().sharedMaterials = materials;
            AddBoxColliders(model, kit.Colliders);

            if (outline && material == null && outlineMaterial != null)
                AddOutline(model, kit, name);
            return model;
        }

        /// <summary>
        /// Контур — отдельный дочерний меш (оболочка с усреднёнными нормалями) с шейдером Nuclear/Outline.
        /// Дочерний, чтобы двигаться вместе с моделью и прятаться вместе с ней (тело своего игрока).
        /// </summary>
        private static void AddOutline(GameObject model, MeshKit kit, string name)
        {
            var mesh = kit.ToOutlineMesh(name + " Outline");
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/{name} Outline.asset"));

            var outline = new GameObject("Outline");
            outline.transform.SetParent(model.transform, false);
            outline.isStatic = model.isStatic;
            outline.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = outline.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = outlineMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void AddBoxColliders(GameObject owner, List<BoxShape> shapes)
        {
            foreach (var shape in shapes)
            {
                var holder = new GameObject("Collider");
                holder.transform.SetParent(owner.transform, false);
                holder.transform.localPosition = shape.Center;
                holder.transform.localRotation = shape.Rotation;
                holder.AddComponent<BoxCollider>().size = shape.Size;
                holder.isStatic = owner.isStatic;
            }
        }

        private static void MarkStatic(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.isStatic = true;
        }

        // ---------- Ассеты ----------

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
                    case Enum e: property.enumValueIndex = Convert.ToInt32(e); break;
                    case Object reference: property.objectReferenceValue = reference; break;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
