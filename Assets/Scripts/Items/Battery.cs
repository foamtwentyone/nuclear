using Nuclear.Audio;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Items
{
    /// <summary>
    /// Аккумулятор для двери бункера. При сильном падении искрит и теряет заряд;
    /// разрядился полностью — нужно искать другой. Заряд виден по индикатору на корпусе.
    /// </summary>
    [RequireComponent(typeof(PhysicsItem))]
    public class Battery : NetworkBehaviour
    {
        [Tooltip("Удары медленнее этого (м/с) заряд не тратят.")]
        [SerializeField] private float safeImpactSpeed = 2f;
        [SerializeField] private float baseLoss = 0.10f;
        [Tooltip("Дополнительная потеря за каждый м/с сверх безопасной скорости.")]
        [SerializeField] private float lossPerExtraSpeed = 0.05f;
        [SerializeField] private float maxLossPerImpact = 0.35f;

        public readonly NetworkVariable<float> Charge = new(1f);

        public bool IsDead => Charge.Value <= 0f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private PhysicsItem item;
        private Renderer indicator;
        private ParticleSystem sparks;
        private MaterialPropertyBlock indicatorBlock;
        private AudioSource sparkSource;

        private void Awake()
        {
            item = GetComponent<PhysicsItem>();
            sparks = GetComponentInChildren<ParticleSystem>();
            var indicatorTransform = transform.Find("Indicator");
            if (indicatorTransform != null)
                indicator = indicatorTransform.GetComponent<Renderer>();
            indicatorBlock = new MaterialPropertyBlock();

            sparkSource = SoundSources.Create3D(gameObject, maxDistance: 20f);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                item.Impacted += OnImpacted;

            Charge.OnValueChanged += OnChargeChanged;
            UpdateIndicator();
        }

        public override void OnNetworkDespawn()
        {
            item.Impacted -= OnImpacted;
            Charge.OnValueChanged -= OnChargeChanged;
        }

        private void OnImpacted(float speed)
        {
            if (speed < safeImpactSpeed || IsDead)
                return;

            float loss = Mathf.Min(maxLossPerImpact, baseLoss + (speed - safeImpactSpeed) * lossPerExtraSpeed);
            Charge.Value = Mathf.Max(0f, Charge.Value - loss);
            PlaySparksRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlaySparksRpc()
        {
            if (sparks != null)
                sparks.Play();
            sparkSource.PlayOneShot(ProceduralAudio.Sparks, 0.8f);
        }

        private void OnChargeChanged(float previous, float current) => UpdateIndicator();

        private void UpdateIndicator()
        {
            if (indicator == null)
                return;

            Color color = IsDead ? new Color(0.1f, 0.1f, 0.1f) : Color.Lerp(Color.red, Color.green, Charge.Value);
            indicator.GetPropertyBlock(indicatorBlock);
            indicatorBlock.SetColor(ColorId, color);
            indicatorBlock.SetColor(BaseColorId, color);
            indicatorBlock.SetColor(EmissionId, color * 1.5f);
            indicator.SetPropertyBlock(indicatorBlock);
        }
    }
}
