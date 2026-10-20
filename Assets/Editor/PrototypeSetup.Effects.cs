using System.Collections.Generic;
using Nuclear.Hazards;
using Nuclear.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nuclear.EditorTools
{
    /// <summary>
    /// Эффекты: огонь в бочках (языки пламени, раскалённое ядро, искры, дым) и радиоактивные поля
    /// (светящийся купол-«аврора», поднимающиеся искорки, вспышки у земли, пульсирующий свет).
    /// Текстуры частиц рисуются кодом, шейдеры — Assets/Shaders/Particle.shader и RadiationField.shader.
    /// </summary>
    public static partial class PrototypeSetup
    {
        private const string ParticleFolder = MaterialFolder + "/Particles";

        private static Texture2D softTexture, flameTexture, smokeTexture;
        private static Mesh domeMesh;

        // ---------- Огонь ----------

        /// <summary>Огонь над бочкой: языки пламени, раскалённое ядро, летящие искры и дым.</summary>
        private static void AddFire(Transform parent, Vector3 position)
        {
            PrepareParticleTextures();

            // Языки пламени: вытянутые «капли», тают и краснеют к верху, дрожат шумом.
            var flames = NewParticles(parent, "Flames", position, ParticleMaterialFor("Flames", flameTexture, new Color(2.2f, 1.5f, 0.9f), additive: true));
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            main.maxParticles = 200;
            Rate(flames, 55f);
            Cone(flames, 6f, 0.2f);
            SizeOverLife(flames, new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.2f));
            ColorOverLife(flames,
                new[] { Key(1f, 0.95f, 0.75f, 0f), Key(1f, 0.65f, 0.2f, 0.35f), Key(0.9f, 0.25f, 0.05f, 0.75f), Key(0.4f, 0.08f, 0.02f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            Noise(flames, 0.35f, 1.2f, 1.5f);
            var velocity = flames.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            // Раскалённое ядро у самых углей — почти белое.
            var core = NewParticles(parent, "Fire Core", position - Vector3.up * 0.05f, ParticleMaterialFor("Fire Core", softTexture, new Color(2.6f, 2f, 1.3f), additive: true));
            var coreMain = core.main;
            coreMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            coreMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            coreMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            Rate(core, 25f);
            Cone(core, 4f, 0.15f);
            ColorOverLife(core,
                new[] { Key(1f, 0.9f, 0.6f, 0f), Key(1f, 0.6f, 0.2f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0f, 1f) });

            // Искры: мелкие, вытянутые по скорости, разлетаются и петляют.
            var embers = NewParticles(parent, "Embers", position, ParticleMaterialFor("Embers", softTexture, new Color(3f, 1.6f, 0.6f), additive: true));
            var emberMain = embers.main;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            emberMain.gravityModifier = 0.1f;
            Rate(embers, 8f);
            Cone(embers, 25f, 0.18f);
            Noise(embers, 0.8f, 2f, 1f);
            ColorOverLife(embers,
                new[] { Key(1f, 0.85f, 0.4f, 0f), Key(1f, 0.4f, 0.1f, 0.6f), Key(0.5f, 0.1f, 0.02f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            var emberRenderer = embers.GetComponent<ParticleSystemRenderer>();
            emberRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            emberRenderer.lengthScale = 3f;
            emberRenderer.velocityScale = 0.05f;

            // Дым: тёмные клубы, растут, вращаются и тают.
            var smoke = NewParticles(parent, "Smoke", position + Vector3.up * 0.5f, ParticleMaterialFor("Smoke", smokeTexture, new Color(0.22f, 0.21f, 0.2f, 0.55f), additive: false));
            var smokeMain = smoke.main;
            smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Rate(smoke, 6f);
            Cone(smoke, 12f, 0.2f);
            SizeOverLife(smoke, new Keyframe(0f, 0.6f), new Keyframe(1f, 3f));
            ColorOverLife(smoke, new[] { Key(1f, 1f, 1f, 0f), Key(1f, 1f, 1f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            var rotation = smoke.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            Noise(smoke, 0.3f, 0.5f, 0.3f);
        }

        // ---------- Радиация ----------

        /// <summary>
        /// Видимая часть радиоактивного поля: купол с «авророй», поднимающиеся зелёные искорки,
        /// редкие вспышки у земли и пульсирующий зелёный свет. Сила — по интенсивности зоны.
        /// </summary>
        private static void AddRadiationVisuals(Transform parent, Vector2 center, float radius, float intensity)
        {
            PrepareParticleTextures();
            float strength = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(intensity / 4f));

            // Основание купола — средняя высота земли у края зоны: в воронке купол накрывает её целиком.
            float baseY = 0f;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                baseY += GroundHeight(center.x + Mathf.Cos(a) * radius * 0.85f, center.y + Mathf.Sin(a) * radius * 0.85f);
            }
            baseY = baseY / 12f - 0.4f;
            var basePoint = new Vector3(center.x, baseY, center.y);

            var root = new GameObject("Radiation Field");
            root.transform.SetParent(parent, false);
            root.transform.position = basePoint;

            var dome = new GameObject("Dome");
            dome.transform.SetParent(root.transform, false);
            dome.transform.localScale = new Vector3(radius, radius * 0.55f, radius);
            dome.AddComponent<MeshFilter>().sharedMesh = DomeMesh();
            var domeRenderer = dome.AddComponent<MeshRenderer>();
            domeRenderer.sharedMaterial = RadiationMaterial($"Radiation Field {radius:0}", strength);
            domeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            domeRenderer.receiveShadows = false;

            // Искорки поднимаются со всей площади и петляют.
            var motes = NewParticles(root.transform, "Motes", Vector3.up * 0.2f, ParticleMaterialFor("Radiation Motes", softTexture, new Color(0.7f, 2.2f, 0.5f), additive: true));
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.maxParticles = 400;
            Rate(motes, radius * 5f * strength);
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.9f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Noise(motes, 0.3f, 0.6f, 0.4f);
            ColorOverLife(motes, new[] { Key(0.8f, 1f, 0.6f, 0f), Key(0.4f, 1f, 0.3f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });

            // Мягкие вспышки у земли — поле «дышит».
            var puffs = NewParticles(root.transform, "Glow Puffs", Vector3.up * 0.4f, ParticleMaterialFor("Radiation Puffs", softTexture, new Color(0.35f, 1.1f, 0.25f, 0.35f), additive: true));
            var puffMain = puffs.main;
            puffMain.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            puffMain.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            puffMain.startSize = new ParticleSystem.MinMaxCurve(radius * 0.25f, radius * 0.5f);
            Rate(puffs, 1.5f * strength);
            var puffShape = puffs.shape;
            puffShape.shapeType = ParticleSystemShapeType.Circle;
            puffShape.radius = radius * 0.6f;
            puffShape.rotation = new Vector3(-90f, 0f, 0f);
            ColorOverLife(puffs, new[] { Key(1f, 1f, 1f, 0f), Key(1f, 1f, 1f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });

            var light = new GameObject("Radiation Glow").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = Vector3.up * 1.6f;
            light.type = LightType.Point;
            light.color = new Color(0.5f, 1f, 0.3f);
            light.intensity = 1.4f * strength;
            light.range = radius * 1.1f;
            Configure(light.gameObject.AddComponent<LightFlicker>(), new Dictionary<string, object>
            {
                ["depth"] = 0.35f,
                ["speed"] = 1.3f,
            });
        }

        private static Material RadiationMaterial(string name, float strength)
        {
            var shader = Shader.Find("Nuclear/RadiationField");
            if (shader == null)
            {
                Debug.LogWarning("Шейдер Nuclear/RadiationField не найден — купол радиации будет без эффекта.");
                return glowMaterial;
            }
            var material = LoadOrCreateMaterial($"{ParticleFolder}/{name}.mat", shader);
            material.SetColor("_Color", new Color(0.45f, 1.4f, 0.3f));
            material.SetFloat("_Strength", strength);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Полусфера радиусом 1 с основанием на y = 0 (нормали наружу).</summary>
        private static Mesh DomeMesh()
        {
            if (domeMesh != null)
                return domeMesh;

            const int rings = 14, segments = 48;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI / 2f * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float theta = Mathf.PI * 2f * s / segments;
                    vertices.Add(new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(phi), Mathf.Sin(theta) * Mathf.Cos(phi)));
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }

            domeMesh = new Mesh { name = "Radiation Dome" };
            domeMesh.SetVertices(vertices);
            domeMesh.SetTriangles(triangles, 0);
            domeMesh.SetNormals(vertices);
            domeMesh.RecalculateBounds();
            AssetDatabase.CreateAsset(domeMesh, AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/Radiation Dome.asset"));
            return domeMesh;
        }

        // ---------- Частицы: материалы, текстуры, настройка ----------

        private static ParticleSystem NewParticles(Transform parent, string name, Vector3 position, Material material = null)
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
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material != null ? material : ParticleMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return particles;
        }

        /// <summary>Материал частиц Nuclear/Particle: аддитивное свечение или полупрозрачный дым.</summary>
        private static Material ParticleMaterialFor(string name, Texture2D texture, Color color, bool additive)
        {
            var shader = Shader.Find("Nuclear/Particle");
            if (shader == null)
            {
                Debug.LogWarning("Шейдер Nuclear/Particle не найден — частицы будут стандартными.");
                return ParticleMaterial();
            }
            var material = LoadOrCreateMaterial($"{ParticleFolder}/{name}.mat", shader);
            material.mainTexture = texture;
            material.SetColor("_Color", color);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            EnsureFolder(ParticleFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        private static void PrepareParticleTextures()
        {
            if (softTexture != null)
                return;
            softTexture = ParticleTexture("Particle Soft", 64, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                return Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
            });
            // Язык пламени: круглый низ, острый верх, рваные края.
            flameTexture = ParticleTexture("Particle Flame", 128, (x, y) =>
            {
                float v = (y + 1f) / 2f;
                float width = 0.62f * Mathf.Pow(1f - v, 0.55f) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v * 4f));
                float wobble = 0.08f * Mathf.Sin(v * 17f + x * 3f) * v;
                float edge = Mathf.Abs(x + wobble);
                if (width <= 0.001f)
                    return 0f;
                float body = Mathf.Clamp01(1f - edge / width);
                return Mathf.Pow(body, 0.8f) * Mathf.Clamp01(1.15f - v);
            });
            // Клуб дыма: несколько мягких пятен с шумом.
            smokeTexture = ParticleTexture("Particle Smoke", 128, (x, y) =>
            {
                float puffs = 0f;
                puffs = Mathf.Max(puffs, Blob(x, y, 0f, 0f, 0.62f));
                puffs = Mathf.Max(puffs, Blob(x, y, -0.32f, 0.18f, 0.42f));
                puffs = Mathf.Max(puffs, Blob(x, y, 0.3f, 0.22f, 0.4f));
                puffs = Mathf.Max(puffs, Blob(x, y, 0.05f, -0.3f, 0.45f));
                float noise = Mathf.PerlinNoise(x * 3f + 10f, y * 3f + 10f);
                return Mathf.Clamp01(puffs * (0.65f + 0.5f * noise));
            });
        }

        private static float Blob(float x, float y, float cx, float cy, float radius)
        {
            float r = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
            return Mathf.Clamp01(1f - r * r);
        }

        /// <summary>Белая текстура с альфой из функции координат в квадрате [-1, 1].</summary>
        private static Texture2D ParticleTexture(string name, int size, System.Func<float, float, float> alpha)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(u, v));
                }
            }
            var texture = SaveTexture($"{TextureFolder}/{name}.asset", pixels, size, linear: false);
            texture.wrapMode = TextureWrapMode.Clamp;
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static void Rate(ParticleSystem particles, float perSecond)
        {
            var emission = particles.emission;
            emission.rateOverTime = perSecond;
        }

        private static void Cone(ParticleSystem particles, float angle, float radius)
        {
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        private static void SizeOverLife(ParticleSystem particles, params Keyframe[] keys)
        {
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        private static GradientColorKey Key(float r, float g, float b, float time) => new(new Color(r, g, b), time);

        private static void ColorOverLife(ParticleSystem particles, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            color.color = gradient;
        }

        private static void Noise(ParticleSystem particles, float strength, float frequency, float scroll)
        {
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = scroll;
        }
    }
}
