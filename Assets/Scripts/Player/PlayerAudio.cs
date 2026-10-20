using Nuclear.Audio;
using Nuclear.Level;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Звуки игрока. Шаги, приземления и смену фильтра слышат все рядом:
    /// движение считается по позиции, поэтому работает и для чужих игроков.
    /// Только свои: дыхание в противогазе, сердцебиение при низком здоровье, подбор предметов и итог уровня.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAudio : MonoBehaviour
    {
        [Header("Шаги")]
        [SerializeField] private float walkStepInterval = 0.5f;
        [SerializeField] private float sprintStepInterval = 0.32f;
        [SerializeField] private float minMoveSpeed = 0.5f;
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float sprintVolume = 0.6f;
        [Tooltip("Скорость падения (м/с), после которой приземление слышно.")]
        [SerializeField] private float landingSpeed = 4f;

        [Header("Дыхание и сердце")]
        [SerializeField, Range(0f, 1f)] private float breathVolume = 0.22f;
        [SerializeField] private float calmBreathInterval = 3.6f;
        [SerializeField] private float heavyBreathInterval = 1.7f;
        [Tooltip("Доля здоровья, ниже которой слышно сердце.")]
        [SerializeField, Range(0f, 1f)] private float heartbeatHealth = 0.35f;

        private PlayerController controller;
        private PlayerSurvival survival;
        private PlayerCarry carry;
        private AudioSource feet;
        private AudioSource body;
        private AudioSource personal;

        private Vector3 lastPosition;
        private float verticalSpeed;
        private float nextStepTime;
        private float nextBreathTime;
        private float nextHeartbeatTime;
        private int stepVariant;
        // Предмет в руках сравниваем по id, чтобы не держать ссылку на уничтоженный объект.
        private ulong lastHeldId = ulong.MaxValue;
        private LevelOutcome.Result lastOutcome;
        private bool subscribed;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            survival = GetComponent<PlayerSurvival>();
            carry = GetComponent<PlayerCarry>();

            feet = CreateSource(spatial: true, maxDistance: 20f);
            body = CreateSource(spatial: true, maxDistance: 15f);
            personal = CreateSource(spatial: false, maxDistance: 1f);
            lastPosition = transform.position;
        }

        private void Start()
        {
            if (survival == null)
                return;

            survival.IsSwappingFilter.OnValueChanged += OnSwappingChanged;
            survival.SpareFilters.OnValueChanged += OnSpareFiltersChanged;
            subscribed = true;
        }

        private void OnDestroy()
        {
            if (!subscribed)
                return;

            survival.IsSwappingFilter.OnValueChanged -= OnSwappingChanged;
            survival.SpareFilters.OnValueChanged -= OnSpareFiltersChanged;
        }

        private void Update()
        {
            if (!controller.IsSpawned)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;

            float previousVertical = verticalSpeed;
            verticalSpeed = delta.y / deltaTime;
            delta.y = 0f;
            float horizontalSpeed = delta.magnitude / deltaTime;

            bool dead = survival != null && survival.IsDead;
            bool grounded = Mathf.Abs(verticalSpeed) < 1.5f;

            if (!dead && grounded && previousVertical < -landingSpeed)
                feet.PlayOneShot(ProceduralAudio.Land, 0.8f);

            if (!dead && grounded && horizontalSpeed >= minMoveSpeed && Time.time >= nextStepTime)
                PlayStep();

            if (controller.IsOwner)
                UpdatePersonal(dead);
        }

        private void PlayStep()
        {
            bool sprinting = controller.IsSprinting.Value;
            nextStepTime = Time.time + (sprinting ? sprintStepInterval : walkStepInterval);
            stepVariant = (stepVariant + 1 + Random.Range(0, ProceduralAudio.FootstepVariants - 1)) % ProceduralAudio.FootstepVariants;
            feet.pitch = Random.Range(0.9f, 1.1f);
            feet.PlayOneShot(ProceduralAudio.Footstep(stepVariant), sprinting ? sprintVolume : walkVolume);
        }

        private void UpdatePersonal(bool dead)
        {
            UpdateOutcomeSting();
            if (dead || survival == null)
                return;

            UpdatePickupSound();

            // Дыхание слышно, только пока маска с фильтром на лице и вокруг грязный воздух.
            bool breathingThroughFilter = !survival.InSafeAir && !survival.IsSwappingFilter.Value && survival.FilterTime.Value > 0f;
            if (breathingThroughFilter && Time.time >= nextBreathTime)
            {
                float exertion = Mathf.Clamp01(1f - controller.Stamina.Normalized + (controller.IsSprinting.Value ? 0.3f : 0f));
                nextBreathTime = Time.time + Mathf.Lerp(calmBreathInterval, heavyBreathInterval, exertion);
                personal.pitch = Mathf.Lerp(1f, 1.25f, exertion);
                personal.PlayOneShot(ProceduralAudio.MaskBreath, breathVolume);
            }

            float healthShare = survival.Health.Value / 100f;
            if (healthShare < heartbeatHealth && Time.time >= nextHeartbeatTime)
            {
                nextHeartbeatTime = Time.time + Mathf.Lerp(0.55f, 1.1f, healthShare / heartbeatHealth);
                personal.PlayOneShot(ProceduralAudio.Heartbeat, 0.6f);
            }
        }

        private void UpdatePickupSound()
        {
            if (carry == null)
                return;

            var held = carry.HeldItem;
            ulong heldId = held != null ? held.NetworkObjectId : ulong.MaxValue;
            if (held != null && heldId != lastHeldId)
                personal.PlayOneShot(ProceduralAudio.Pickup, 0.7f);
            lastHeldId = heldId;
        }

        private void UpdateOutcomeSting()
        {
            var outcome = LevelOutcome.Current;
            var result = outcome != null ? outcome.Value.Value : LevelOutcome.Result.Playing;
            if (result == lastOutcome)
                return;

            lastOutcome = result;
            if (result == LevelOutcome.Result.Won)
                personal.PlayOneShot(ProceduralAudio.Victory, 0.8f);
            else if (result == LevelOutcome.Result.Lost)
                personal.PlayOneShot(ProceduralAudio.Defeat, 0.8f);
        }

        private void OnSwappingChanged(bool previous, bool current)
        {
            if (survival.IsDead)
                return;

            body.PlayOneShot(current ? ProceduralAudio.FilterRemove : ProceduralAudio.FilterAttach, 0.8f);
        }

        private void OnSpareFiltersChanged(int previous, int current)
        {
            if (controller.IsOwner && current > previous)
                personal.PlayOneShot(ProceduralAudio.Pickup, 0.7f);
        }

        private AudioSource CreateSource(bool spatial, float maxDistance)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.playOnAwake = false;
            return source;
        }
    }
}
