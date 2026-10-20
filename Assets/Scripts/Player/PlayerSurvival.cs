using Nuclear.Audio;
using Nuclear.Hazards;
using Nuclear.Noise;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Противогаз, здоровье и радиация. Всё считает сервер, игроки видят значения через NetworkVariable.
    /// Снаружи бункера фильтр тратится; без фильтра игрок задыхается и кашляет, и кашель слышит Слепыш.
    /// Накопленная доза радиации снижает максимум здоровья.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerSurvival : NetworkBehaviour
    {
        [Header("Противогаз")]
        [SerializeField] private float filterDuration = 240f;
        [Tooltip("Сколько секунд игрок без фильтра, пока меняет его.")]
        [SerializeField] private float filterSwapTime = 3f;
        [SerializeField] private float suffocationDamagePerSecond = 4f;
        [SerializeField] private float coughInterval = 2.5f;
        [SerializeField] private float coughNoiseRadius = 12f;

        [Header("Здоровье и радиация")]
        [SerializeField] private float baseMaxHealth = 100f;
        [Tooltip("Ниже этого радиация максимум здоровья не опускает.")]
        [SerializeField] private float minMaxHealth = 25f;
        [Tooltip("На сколько очков падает максимум здоровья за единицу дозы.")]
        [SerializeField] private float maxHealthPerDose = 1f;

        public readonly NetworkVariable<float> FilterTime = new();
        public readonly NetworkVariable<bool> IsSwappingFilter = new();
        public readonly NetworkVariable<float> Health = new();
        public readonly NetworkVariable<float> Dose = new();

        public float FilterDuration => filterDuration;
        public float MaxHealth => Mathf.Max(minMaxHealth, baseMaxHealth - Dose.Value * maxHealthPerDose);
        public bool IsDead => IsSpawned && Health.Value <= 0f;

        /// <summary>Чистый ли воздух вокруг. Зоны есть у всех, поэтому считается локально.</summary>
        public bool InSafeAir => SafeAirZone.Contains(BreathPoint);

        private Vector3 BreathPoint => transform.position + Vector3.up * 1.6f;

        private PlayerCarry carry;
        private AudioSource voice;
        private float swapEndTime;
        private float nextCoughTime;

        private void Awake()
        {
            carry = GetComponent<PlayerCarry>();

            voice = SoundSources.Create3D(gameObject, maxDistance: 25f);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                FilterTime.Value = filterDuration;
                Health.Value = baseMaxHealth;
            }

            Health.OnValueChanged += OnHealthChanged;
            ApplyPose();
        }

        public override void OnNetworkDespawn()
        {
            Health.OnValueChanged -= OnHealthChanged;
        }

        /// <summary>Сервер: лечит, но не выше максимума, срезанного радиацией. Мёртвых не поднимает.</summary>
        public void Heal(float amount)
        {
            if (IsDead)
                return;

            Health.Value = Mathf.Min(MaxHealth, Health.Value + amount);
        }

        /// <summary>Сервер: отнимает здоровье. При нуле игрок погибает.</summary>
        public void TakeDamage(float amount)
        {
            if (IsDead)
                return;

            Health.Value = Mathf.Max(0f, Health.Value - amount);
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (IsServer && !IsDead)
                ServerTick(Time.deltaTime);
        }

        private void ServerTick(float deltaTime)
        {
            float radiation = RadiationZone.IntensityAt(BreathPoint);
            if (radiation > 0f)
            {
                Dose.Value += radiation * deltaTime;
                if (Health.Value > MaxHealth)
                    Health.Value = MaxHealth;
            }

            if (IsSwappingFilter.Value && Time.time >= swapEndTime)
            {
                IsSwappingFilter.Value = false;
                FilterTime.Value = filterDuration;
            }

            if (InSafeAir)
                return;

            if (!IsSwappingFilter.Value && FilterTime.Value > 0f)
            {
                FilterTime.Value = Mathf.Max(0f, FilterTime.Value - deltaTime);
                return;
            }

            TakeDamage(suffocationDamagePerSecond * deltaTime);

            if (Time.time >= nextCoughTime)
            {
                nextCoughTime = Time.time + coughInterval * Random.Range(0.7f, 1.3f);
                NoiseEvents.Emit(BreathPoint, coughNoiseRadius);
                CoughRpc();
            }
        }

        /// <summary>
        /// Сервер: начать смену фильтра (новый фильтр берёт PlayerGear из инвентаря).
        /// Старый выкидывается сразу, новый встаёт через несколько секунд.
        /// </summary>
        public bool BeginFilterSwap()
        {
            if (IsDead || IsSwappingFilter.Value)
                return false;

            FilterTime.Value = 0f;
            IsSwappingFilter.Value = true;
            swapEndTime = Time.time + filterSwapTime;
            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CoughRpc()
        {
            voice.PlayOneShot(ProceduralAudio.Cough);
        }

        private void OnHealthChanged(float previous, float current)
        {
            if (current <= 0f && IsServer && carry != null && carry.HeldItem is { } item)
                item.RemoveCarrier(carry, false);

            ApplyPose();
        }

        /// <summary>
        /// Мёртвый игрок лежит: тело заваливается, камеру забирает призрак (PlayerGhost).
        /// Поза обратима, на случай если клиент увидел нулевое здоровье до первой синхронизации.
        /// </summary>
        private void ApplyPose()
        {
            bool dead = IsDead;

            var body = transform.Find("Body");
            if (body != null)
            {
                body.localPosition = new Vector3(0f, dead ? 0.35f : 0.9f, 0f);
                body.localRotation = dead ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            }
        }
    }
}
