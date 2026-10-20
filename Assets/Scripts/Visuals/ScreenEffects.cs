using Nuclear.Player;
using UnityEngine;

namespace Nuclear.Visuals
{
    /// <summary>
    /// Экранные эффекты своего игрока, без постобработки (работают в любом конвейере рендера):
    /// виньетка по краям, сжимающаяся темнота при удушье, красная вспышка при уроне,
    /// в радиации — зелёный оттенок, зелёная виньетка и «зерно» помех, сильнее с ростом фона.
    /// Рисуется под HUD.
    /// </summary>
    public class ScreenEffects : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float baseVignette = 0.2f;
        [SerializeField, Range(0f, 1f)] private float suffocatingVignette = 0.9f;
        [SerializeField] private float damageFlashTime = 0.6f;

        private static Texture2D vignette;
        private static Texture2D grain;

        private PlayerController player;
        private PlayerSurvival survival;
        private Dosimeter dosimeter;
        private float radiation;
        private float suffocation;
        private float lastHealth = -1f;
        private float flash;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            survival = GetComponent<PlayerSurvival>();
            dosimeter = GetComponent<Dosimeter>();
        }

        private void Update()
        {
            if (player == null || !player.IsSpawned || !player.IsOwner || survival == null)
                return;

            bool choking = !survival.IsDead && !survival.InSafeAir
                && (survival.IsSwappingFilter.Value || survival.FilterTime.Value <= 0f);
            suffocation = Mathf.MoveTowards(suffocation, choking ? 1f : 0f, Time.deltaTime * (choking ? 0.5f : 1.5f));

            float health = survival.Health.Value;
            if (lastHealth >= 0f && health < lastHealth - 5f)
                flash = 1f;
            lastHealth = health;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime / damageFlashTime);

            // Фон 0..4 в секунду → сила эффекта 0..1; нарастает и спадает плавно.
            float target = survival.IsDead || dosimeter == null ? 0f : Mathf.Clamp01(dosimeter.Intensity / 3f);
            radiation = Mathf.MoveTowards(radiation, target, Time.deltaTime * 1.5f);
        }

        private void OnGUI()
        {
            if (player == null || !player.IsSpawned || !player.IsOwner || Event.current.type != EventType.Repaint)
                return;

            // Больше depth — рисуется раньше, то есть под HUD.
            GUI.depth = 10;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);

            float pulse = suffocation > 0f ? 0.06f * Mathf.Sin(Time.time * 5f) : 0f;
            float strength = Mathf.Lerp(baseVignette, suffocatingVignette, suffocation) + pulse;
            GUI.color = new Color(0.02f, 0.03f, 0.02f, Mathf.Clamp01(strength));
            GUI.DrawTexture(screen, Vignette, ScaleMode.StretchToFill);

            // Призрак видит мир холодным и выцветшим.
            if (survival != null && survival.IsDead)
            {
                GUI.color = new Color(0.55f, 0.75f, 0.95f, 0.10f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = new Color(0.25f, 0.4f, 0.6f, 0.5f);
                GUI.DrawTexture(screen, Vignette, ScaleMode.StretchToFill);
            }

            if (radiation > 0.01f)
                DrawRadiation(screen);

            if (flash > 0f)
            {
                GUI.color = new Color(0.6f, 0.05f, 0.03f, flash * 0.6f);
                GUI.DrawTexture(screen, Vignette, ScaleMode.StretchToFill);
                GUI.color = new Color(0.6f, 0.05f, 0.03f, flash * 0.15f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        /// <summary>Радиальный градиент: прозрачный центр, плотные края.</summary>
        /// <summary>Зелёный оттенок, зелёная виньетка с пульсом и мерцающее «зерно», как помехи на плёнке.</summary>
        private void DrawRadiation(Rect screen)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3.1f);
            GUI.color = new Color(0.35f, 1f, 0.25f, 0.08f * radiation);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.85f, 0.15f, 0.55f * radiation * pulse);
            GUI.DrawTexture(screen, Vignette, ScaleMode.StretchToFill);

            // Зерно каждый кадр сдвигается случайно — мерцает.
            var offset = new Vector2(Random.value, Random.value);
            float tile = 160f;
            GUI.color = new Color(0.8f, 1f, 0.7f, 0.22f * radiation);
            GUI.DrawTextureWithTexCoords(screen, Grain, new Rect(offset.x, offset.y, screen.width / tile, screen.height / tile), true);
        }

        private static Texture2D Grain
        {
            get
            {
                if (grain != null)
                    return grain;
                const int size = 128;
                grain = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color32[size * size];
                var random = new System.Random(7);
                for (int i = 0; i < pixels.Length; i++)
                {
                    // Редкие яркие точки — «попадания частиц» — на фоне слабого шума.
                    double r = random.NextDouble();
                    byte alpha = (byte)(r > 0.985 ? 255 : r > 0.6 ? 60 : 0);
                    pixels[i] = new Color32(255, 255, 255, alpha);
                }
                grain.SetPixels32(pixels);
                grain.Apply(false);
                return grain;
            }
        }

        private static Texture2D Vignette
        {
            get
            {
                if (vignette != null)
                    return vignette;

                const int size = 128;
                vignette = new Texture2D(size, size, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);
                        float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, distance));
                        vignette.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                vignette.Apply(false, true);
                return vignette;
            }
        }
    }
}
