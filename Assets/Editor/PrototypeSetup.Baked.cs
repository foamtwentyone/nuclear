using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nuclear.EditorTools
{
    /// <summary>
    /// Готовые модели из нейросети (Meshy и т. п.): исходный .glb упрощается и перепекается скриптом
    /// Tools/bake_model.py в пару «.mesh.bytes + .png» рядом с ним. Здесь из них собираются меш-ассет,
    /// материал Nuclear/Toon с этой текстурой и мультяшный контур.
    /// </summary>
    public static partial class PrototypeSetup
    {
        private const string BakedModelFolder = "Assets/Art/Models";

        /// <summary>Читает сетку, подготовленную bake_model.py, и сохраняет её меш-ассетом.</summary>
        private static Mesh LoadBakedMesh(string name)
        {
            string path = $"{BakedModelFolder}/{name}.mesh.bytes";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Нет модели {path} — запусти Tools/bake_model.py.");
                return null;
            }
            using var reader = new BinaryReader(File.OpenRead(path));
            if (new string(reader.ReadChars(4)) != "NMSH" || reader.ReadInt32() != 1)
            {
                Debug.LogWarning($"{path}: неизвестный формат.");
                return null;
            }
            int vertexCount = reader.ReadInt32();
            int indexCount = reader.ReadInt32();
            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var indices = new int[indexCount];
            for (int i = 0; i < vertexCount; i++)
                positions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < vertexCount; i++)
                normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < vertexCount; i++)
                uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < indexCount; i++)
                indices[i] = reader.ReadInt32();

            var mesh = new Mesh { name = name };
            if (vertexCount > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/{name}.asset"));
            return mesh;
        }

        /// <summary>Материал с перепечённой текстурой: свет и тень уже нарисованы, свои пятна «краски» почти не нужны.</summary>
        private static Material BakedMaterial(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BakedModelFolder}/{name}.png");
            EnsureFolder(MaterialFolder + "/Baked");
            var material = SaveMaterial($"Baked/{name}", Color.white, texture: texture, smoothness: 0.15f);
            if (material.HasProperty("_Mottle"))
            {
                material.SetFloat("_Mottle", 0.04f);
                material.SetFloat("_EdgeDarken", 0.15f);
                material.SetFloat("_Banding", 0.3f);
            }
            return material;
        }

        /// <summary>Объект с готовой моделью и её контуром; null, если модель не подготовлена.</summary>
        private static GameObject AddBakedModel(Transform parent, string objectName, string modelName)
        {
            var mesh = LoadBakedMesh(modelName);
            if (mesh == null)
                return null;
            var model = new GameObject(objectName);
            model.transform.SetParent(parent, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterial = BakedMaterial(modelName);
            if (outlineMaterial != null)
                AddOutline(model, OutlineMesh(mesh, modelName + " Outline"));
            return model;
        }

        /// <summary>Оболочка для контура: вершины сварены по положению (швы развёртки), нормали усреднены.</summary>
        private static Mesh OutlineMesh(Mesh source, string name)
        {
            var vertices = source.vertices;
            var triangles = source.triangles;
            var index = new Dictionary<Vector3Int, int>();
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var outlineTriangles = new int[triangles.Length];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                var faceNormal = Vector3.Cross(b - a, c - a); // длина = удвоенная площадь, крупные грани весят больше
                for (int corner = 0; corner < 3; corner++)
                {
                    var position = vertices[triangles[t + corner]];
                    var key = Vector3Int.RoundToInt(position * 1000f);
                    if (!index.TryGetValue(key, out int welded))
                    {
                        welded = positions.Count;
                        index.Add(key, welded);
                        positions.Add(position);
                        normals.Add(Vector3.zero);
                    }
                    normals[welded] += faceNormal;
                    outlineTriangles[t + corner] = welded;
                }
            }
            for (int i = 0; i < normals.Count; i++)
                normals[i] = normals[i].normalized;

            var mesh = new Mesh { name = name };
            if (positions.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTriangles(outlineTriangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/{name}.asset"));
            return mesh;
        }
    }
}
