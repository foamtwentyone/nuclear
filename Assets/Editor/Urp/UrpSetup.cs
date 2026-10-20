using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nuclear.EditorTools.Urp
{
    /// <summary>
    /// Настройка URP. Эта сборка компилируется, только когда пакет URP установлен
    /// (см. Nuclear.Editor.Urp.asmdef), поэтому основное меню вызывает её через отражение.
    /// </summary>
    public static class UrpSetup
    {
        private const string Folder = "Assets/Settings";

        /// <summary>Создаёт конвейер URP с затенением углов (SSAO) и сглаживанием и делает его основным.</summary>
        public static void Install()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            string rendererPath = Folder + "/Nuclear Renderer.asset";
            string pipelinePath = Folder + "/Nuclear URP.asset";
            AssetDatabase.DeleteAsset(pipelinePath);
            AssetDatabase.DeleteAsset(rendererPath);

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
            PrepareRenderer(renderer);
            AddRendererFeature<ScreenSpaceAmbientOcclusion>(renderer, "Ambient Occlusion");

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 60f;
            pipeline.shadowCascadeCount = 2;
            pipeline.supportsHDR = true;
            AssetDatabase.CreateAsset(pipeline, pipelinePath);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            // У уровней качества может быть свой конвейер: сбрасываем, чтобы везде работал наш.
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
        }

        /// <summary>Настраивает рендерер, созданный раньше (вызывается при каждой сборке сцены под URP).</summary>
        public static void PrepareRenderer()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Folder + "/Nuclear Renderer.asset");
            if (renderer != null)
                PrepareRenderer(renderer);
        }

        /// <summary>
        /// Отключает Depth Priming. С ним (а SSAO его включает в режиме Auto) URP рисует непрозрачное
        /// только там, где глубина совпала с предварительным проходом. Оболочки контуров в этот проход
        /// не попадают и целиком отсекаются — обводки пропадают.
        /// Режим — обычный Forward: под него написан цикл дополнительных источников в шейдере Nuclear/Toon.
        /// </summary>
        private static void PrepareRenderer(UniversalRendererData renderer)
        {
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            renderer.renderingMode = RenderingMode.Forward;
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Камере нужна постобработка, иначе объём Volume на неё не действует.</summary>
        public static void ConfigureCamera(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
        }

        /// <summary>
        /// Глобальная постобработка в мультяшном духе: мягкая тональная кривая, чуть сочнее цвета,
        /// тёплые света и холодные тени, мягкое свечение огня и радиации, лёгкая виньетка.
        /// </summary>
        public static void AddPostProcessing()
        {
            string path = Folder + "/Post Processing.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemapping = AddOverride<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = AddOverride<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(8f);
            color.saturation.Override(0f);

            AddOverride<WhiteBalance>(profile).temperature.Override(6f);

            var tones = AddOverride<ShadowsMidtonesHighlights>(profile);
            tones.shadows.Override(new Vector4(0.95f, 0.98f, 1.06f, 0f));
            tones.highlights.Override(new Vector4(1.04f, 1f, 0.95f, 0f));

            var bloom = AddOverride<Bloom>(profile);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(1.2f);
            bloom.scatter.Override(0.65f);

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.5f);

            AssetDatabase.SaveAssets();

            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        /// <summary>Компонент профиля нужно сохранить внутри ассета профиля, иначе он пропадёт после перезапуска.</summary>
        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>
        /// Добавляет эффект к рендереру так же, как это делает инспектор: сам эффект — вложенный ассет,
        /// а в рендерере — ссылка на него и его локальный идентификатор.
        /// </summary>
        private static void AddRendererFeature<T>(UniversalRendererData renderer, string name) where T : ScriptableRendererFeature
        {
            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var serialized = new SerializedObject(renderer);
            var features = serialized.FindProperty("m_RendererFeatures");
            var map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }
    }
}
