using Nuclear.Player;
using UnityEngine;

namespace Nuclear.Visuals
{
    /// <summary>
    /// Экранные эффекты своего игрока, без постобработки (работают в любом конвейере рендера):
    /// виньетка по краям, сжимающаяся темнота при удушье, красная вспышка при уроне.
    /// Рисуется под HUD.
    /// </summary>
    public class ScreenEffects : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float baseVignette = 0.45f;
        [SerializeField, Range(0f, 1f)] private float suffocatingVignette = 0.9f;
        [SerializeField] private float damageFlashTime = 0.6f;

        private static Texture2D vignette;

        private PlayerController player;
        private PlayerSurvival survival;
        private float suffocation;
        private float lastHealth = -1f;
        private float flash;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            survival = GetComponent<PlayerSurvival>();
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
