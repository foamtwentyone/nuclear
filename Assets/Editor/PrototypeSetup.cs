using System;
using System.Collections.Generic;
using Nuclear.EditorTools.LowPoly;
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
        private static readonly Color FogColor = new(0.53f, 0.51f, 0.48f);

        private static Material lowPolyMaterial;
        private static Material glowMaterial;

        [MenuItem("Nuclear/Собрать сцену прототипа")]
        public static void Build()
        {
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
        }

        // ---------- Материалы ----------

        private static bool UsingUrp => GraphicsSettings.defaultRenderPipeline != null;

        private static Shader LitShader => Shader.Find(UsingUrp ? "Universal Render Pipeline/Lit" : "Standard");

        private static void CreateSharedMaterials()
        {
            var palette = Palette.SaveTexture($"{GeneratedFolder}/Palette.asset");
            lowPolyMaterial = SaveMaterial("LowPoly", Color.white, texture: palette, smoothness: 0.05f);
            glowMaterial = SaveMaterial("Toxic Glow", new Color(0.55f, 1f, 0.35f), emission: new Color(0.6f, 1.6f, 0.3f));
        }

        /// <summary>
        /// Материал под текущий конвейер рендера (встроенный или URP). Если ассет уже есть,
        /// он обновляется — так материалы переключаются на URP после его включения.
        /// </summary>
        private static Material SaveMaterial(string name, Color color, Color? emission = null, Texture texture = null, float smoothness = 0.1f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(LitShader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != LitShader)
            {
                material.shader = LitShader;
            }

            material.color = color;
            material.mainTexture = texture;
            foreach (string property in new[] { "_Glossiness", "_Smoothness" })
            {
                if (material.HasProperty(property))
                    material.SetFloat(property, smoothness);
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
        private static Mesh SaveMesh(MeshKit kit, string name)
        {
            var mesh = kit.ToMesh(name);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/{name}.asset");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Дочерний объект с low-poly моделью и её коллайдерами-коробками.</summary>
        private static GameObject AddModel(Transform parent, string name, MeshKit kit, Vector3 position, Quaternion rotation, Material material = null, float scale = 1f)
        {
            var model = new GameObject(name);
            model.transform.SetParent(parent, false);
            model.transform.localPosition = position;
            model.transform.localRotation = rotation;
            model.transform.localScale = Vector3.one * scale;
            model.AddComponent<MeshFilter>().sharedMesh = SaveMesh(kit, name);
            model.AddComponent<MeshRenderer>().sharedMaterial = material != null ? material : lowPolyMaterial;
            AddBoxColliders(model, kit.Colliders);
            return model;
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
