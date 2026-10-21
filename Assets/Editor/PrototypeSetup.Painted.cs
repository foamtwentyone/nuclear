using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nuclear.EditorTools
{
    /// <summary>
    /// «Расписанные» модели: роспись с картинки-референса (три вида — спереди, сбоку справа, сзади)
    /// переносится на вылепленную модель в цвета вершин. Каждая вершина берёт цвет из того вида,
    /// который смотрит на неё прямее всего. Белый фон и нарисованные глаза не переносятся —
    /// у модели свои глаза (отдельный меш, моргают). Рисуется шейдером Nuclear/Toon с _VertexColors.
    /// </summary>
    public static partial class PrototypeSetup
    {
        private const string FrogReference = "Assets/Art/Reference/Frog Views.png";
        // Рамки фигур на картинке (пиксели, y сверху): спереди, сбоку справа (морда вправо), сзади.
        private static readonly RectInt FrogFront = new(19, 282, 473, 444);
        private static readonly RectInt FrogSide = new(547, 279, 448, 450);
        private static readonly RectInt FrogBack = new(1043, 279, 474, 447);

        /// <summary>Материал для расписанных моделей: цвет из вершин, без своих пятен «краски».</summary>
        private static Material PaintedMaterial(string name)
        {
            var shader = UsingUrp ? Shader.Find("Nuclear/Toon") : null;
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogWarning($"{name}: роспись референса видна только с URP и шейдером Nuclear/Toon — модель будет одноцветной.");
                return SaveMaterial($"Painted/{name}", new Color(0.37f, 0.36f, 0.16f));
            }
            EnsureFolder(MaterialFolder + "/Painted");
            string path = $"{MaterialFolder}/Painted/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_VertexColors", 1f);
            // Свет и тени уже нарисованы на референсе: свои пятна выключаем, подчёркивание формы слабее.
            material.SetFloat("_Mottle", 0f);
            material.SetFloat("_EdgeDarken", 0.15f);
            material.SetFloat("_Banding", 0.3f);
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Переносит роспись референса на меш (координаты меша — как у модели в префабе).</summary>
        private static void ProjectReference(Mesh mesh, string imagePath, RectInt front, RectInt side, RectInt back, Color fallback)
        {
            if (!File.Exists(imagePath))
            {
                Debug.LogWarning($"Нет картинки-референса {imagePath} — модель останется без росписи.");
                return;
            }
            var image = new Texture2D(2, 2);
            image.LoadImage(File.ReadAllBytes(imagePath));
            int width = image.width, height = image.height;
            var pixels = image.GetPixels32();
            Object.DestroyImmediate(image);

            bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;
            if (linear)
                fallback = fallback.linear;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var bounds = mesh.bounds;
            var colors = new Color32[vertices.Length];

            Color32 Sample(RectInt frame, float u, float v, out bool valid)
            {
                int px = Mathf.Clamp(Mathf.RoundToInt(frame.x + u * frame.width), 0, width - 1);
                int py = Mathf.Clamp(Mathf.RoundToInt(frame.y + frame.height - v * frame.height), 0, height - 1);
                var c = pixels[(height - 1 - py) * width + px];
                bool white = c.r > 215 && c.g > 215 && c.b > 205;
                bool paintedEye = c.r > 165 && c.g > 150 && c.b < 120 && (c.r + c.g) / 2f - c.b > 75f;
                valid = !white && !paintedEye;
                return c;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                var n = normals[i];
                float u = Mathf.InverseLerp(bounds.min.x, bounds.max.x, p.x);
                float v = Mathf.InverseLerp(bounds.min.y, bounds.max.y, p.y);
                float s = Mathf.InverseLerp(bounds.min.z, bounds.max.z, p.z);

                var f = Sample(front, u, v, out bool fValid);
                var b = Sample(back, 1f - u, v, out bool bValid);
                var d = Sample(side, s, v, out bool dValid);
                float wf = fValid ? Mathf.Pow(Mathf.Max(0f, n.z), 2f) : 0f;
                float wb = bValid ? Mathf.Pow(Mathf.Max(0f, -n.z), 2f) : 0f;
                float wd = dValid ? n.x * n.x : 0f;
                float total = wf + wb + wd;
                if (total < 1e-3f)
                {
                    colors[i] = fallback;
                    continue;
                }
                var color = ((Color)f * wf + (Color)b * wb + (Color)d * wd) / total;
                color.a = 1f;
                // Цвета вершин шейдер берёт как есть: в линейном пространстве их нужно перевести из sRGB картинки.
                colors[i] = linear ? color.linear : color;
            }
            mesh.colors32 = colors;
            EditorUtility.SetDirty(mesh);
        }
    }
}
