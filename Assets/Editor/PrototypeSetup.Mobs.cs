using System.Collections.Generic;
using Nuclear.EditorTools.LowPoly;
using Nuclear.Enemies;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Nuclear.EditorTools
{
    /// <summary>Префабы мобов перехода 1: Сом, Лягушка, Рот (поведение — в Assets/Scripts/Enemies).</summary>
    public static partial class PrototypeSetup
    {
        /// <summary>Общая основа моба: сетевой объект, позиция и поворот по вертикали синхронизируются, кинематическое тело.</summary>
        private static GameObject MobRoot(string name)
        {
            var root = new GameObject(name);
            // Кинематическое тело: игроки упираются в моба, а Слепыш не считает его стеной.
            root.AddComponent<Rigidbody>().isKinematic = true;
            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;
            return root;
        }

        private static GameObject BuildCatfishPrefab()
        {
            var root = MobRoot("Catfish");
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0.38f, -0.2f);
            capsule.height = 3.8f;
            capsule.radius = 0.5f;

            var kit = new MeshKit();
            Models.CatfishBody(kit);
            AddModel(root.transform, "Body", kit, Vector3.zero, Quaternion.identity);

            // Усы — цепочки отрезков; один меш на все, расставляет их CatfishAnimator.
            var segmentKit = new MeshKit();
            Models.WhiskerSegment(segmentKit);
            var segmentMesh = SaveMesh(segmentKit, "Whisker Segment", out var swatches);
            var materials = System.Array.ConvertAll(swatches, swatch => surfaceMaterials[swatch]);
            var whiskers = new GameObject("Whiskers").transform;
            whiskers.SetParent(root.transform, false);
            for (int w = 0; w < Catfish.WhiskerCount; w++)
            {
                var whisker = new GameObject($"Whisker {w}").transform;
                whisker.SetParent(whiskers, false);
                for (int i = 0; i < Catfish.WhiskerPoints - 1; i++)
                {
                    var segment = new GameObject($"Segment {i}");
                    segment.transform.SetParent(whisker, false);
                    segment.AddComponent<MeshFilter>().sharedMesh = segmentMesh;
                    var renderer = segment.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            var catfish = root.AddComponent<Catfish>();
            var serialized = new SerializedObject(catfish);
            var roots = serialized.FindProperty("whiskerRoots");
            roots.arraySize = Models.CatfishWhiskerRoots.Length;
            for (int i = 0; i < Models.CatfishWhiskerRoots.Length; i++)
                roots.GetArrayElementAtIndex(i).vector3Value = Models.CatfishWhiskerRoots[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<CatfishAnimator>();

            return SavePrefab(root, "Catfish");
        }

        private static GameObject BuildFrogPrefab()
        {
            var root = MobRoot("Frog");
            var sphere = root.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 0.5f, 0f);
            sphere.radius = 0.55f;

            var bodyKit = new MeshKit();
            Models.FrogBody(bodyKit);
            var body = AddModel(root.transform, "Body", bodyKit, Vector3.zero, Quaternion.identity);
            var eyesKit = new MeshKit();
            Models.FrogEyeBalls(eyesKit);
            AddModel(body.transform, "Eyes", eyesKit, Models.FrogEyes, Quaternion.identity);

            root.AddComponent<Frog>();
            root.AddComponent<FrogAnimator>();
            return SavePrefab(root, "Frog");
        }

        private static GameObject BuildMouthPrefab()
        {
            var root = MobRoot("Mouth");
            var sphere = root.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 0.35f, 0f);
            sphere.radius = 0.55f;

            // Тело — пустышка для раздувания; внутри нижняя половина и челюсть на петле.
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var lowerKit = new MeshKit();
            Models.MouthLower(lowerKit);
            AddModel(body, "Lower", lowerKit, Vector3.zero, Quaternion.identity);
            var jaw = new GameObject("Jaw").transform;
            jaw.SetParent(body, false);
            jaw.localPosition = Models.MouthHinge;
            var upperKit = new MeshKit();
            Models.MouthUpper(upperKit);
            AddModel(jaw, "Upper", upperKit, Vector3.zero, Quaternion.identity);

            root.AddComponent<Mouth>();
            root.AddComponent<MouthAnimator>();
            return SavePrefab(root, "Mouth");
        }
    }
}
