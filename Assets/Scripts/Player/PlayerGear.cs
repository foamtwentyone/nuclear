using Nuclear.Audio;
using Nuclear.Enemies;
using Nuclear.Items;
using Nuclear.Noise;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nuclear.Player
{
    /// <summary>
    /// Карман снаряжения: бинт, фонарь с батарейками и лом, по одному каждого вида.
    /// 1/2/3 выбирают предмет, ПКМ применяет выбранный, F включает фонарь.
    /// Всё решает сервер; при смерти снаряжение выпадает рядом с телом.
    /// </summary>
    [RequireComponent(typeof(PlayerSurvival), typeof(PlayerController))]
    public class PlayerGear : NetworkBehaviour
    {
        public enum Slot : byte
        {
            Bandage,
            Flashlight,
            Crowbar,
        }

        [Header("Бинт")]
        [SerializeField] private float bandageHeal = 40f;
        [SerializeField] private float bandageTime = 4f;
        [SerializeField] private float bandageReach = 2f;
        [Tooltip("Если перевязывающий сдвинулся дальше этого (м), перевязка прерывается.")]
        [SerializeField] private float bandageMoveTolerance = 0.3f;

        [Header("Фонарь")]
        [SerializeField] private float flashlightDuration = 180f;
        [SerializeField] private int maxSpareBatteries = 2;
        [SerializeField] private float clickNoiseRadius = 3f;

        [Header("Лом")]
        [SerializeField] private float crowbarReach = 2.2f;
        [SerializeField] private float stunTime = 3f;

        [Header("Префабы, которые выпадают при смерти")]
        [SerializeField] private GameObject bandagePrefab;
        [SerializeField] private GameObject flashlightPrefab;
        [SerializeField] private GameObject crowbarPrefab;
        [SerializeField] private GameObject batteryPrefab;

        public readonly NetworkVariable<int> Bandages = new();
        public readonly NetworkVariable<bool> HasFlashlight = new();
        public readonly NetworkVariable<bool> HasCrowbar = new();
        public readonly NetworkVariable<int> SpareBatteries = new();
        public readonly NetworkVariable<float> FlashlightCharge = new();
        public readonly NetworkVariable<bool> FlashlightOn = new();
        /// <summary>Перевязка: 0 — не идёт, иначе доля 0..1.</summary>
        public readonly NetworkVariable<float> BandageProgress = new();

        /// <summary>Выбранный предмет — только у владельца.</summary>
        public Slot Selected { get; private set; }

        /// <summary>Свой игрок смотрит на товарища, которого можно перевязать (для подсказки).</summary>
        public bool LookingAtTeammate { get; private set; }

        public float FlashlightDuration => flashlightDuration;

        private PlayerSurvival survival;
        private PlayerController controller;
        private Light flashlight;
        private Transform cameraTransform;
        private AudioSource hands;

        private PlayerSurvival bandageTarget;
        private float bandageStart;
        private Vector3 bandagePosition;

        private void Awake()
        {
            survival = GetComponent<PlayerSurvival>();
            controller = GetComponent<PlayerController>();
            cameraTransform = GetComponentInChildren<Camera>(true).transform;
            var lightTransform = cameraTransform.Find("Flashlight");
            if (lightTransform != null)
                flashlight = lightTransform.GetComponent<Light>();
            hands = SoundSources.Create3D(gameObject, maxDistance: 15f);
        }

        public override void OnNetworkSpawn()
        {
            FlashlightOn.OnValueChanged += OnFlashlightChanged;
            survival.Health.OnValueChanged += OnHealthChanged;
            OnFlashlightChanged(false, FlashlightOn.Value);
        }

        public override void OnNetworkDespawn()
        {
            FlashlightOn.OnValueChanged -= OnFlashlightChanged;
            survival.Health.OnValueChanged -= OnHealthChanged;
        }

        /// <summary>Есть ли в кармане место под такой предмет (видно всем, для подсказок).</summary>
        public bool HasSpaceFor(GearKind kind) => kind switch
        {
            GearKind.Bandage => Bandages.Value < 1,
            GearKind.Flashlight => !HasFlashlight.Value,
            GearKind.Crowbar => !HasCrowbar.Value,
            GearKind.FlashlightBattery => SpareBatteries.Value < maxSpareBatteries,
            _ => false,
        };

        /// <summary>Сервер: кладёт снаряжение в карман, если есть место.</summary>
        public bool TryStash(GearPickup pickup)
        {
            if (survival.IsDead || !HasSpaceFor(pickup.Kind))
                return false;

            switch (pickup.Kind)
            {
                case GearKind.Bandage:
                    Bandages.Value++;
                    break;
                case GearKind.Flashlight:
                    HasFlashlight.Value = true;
                    FlashlightCharge.Value = pickup.Charge < 0f ? flashlightDuration : pickup.Charge;
                    break;
                case GearKind.Crowbar:
                    HasCrowbar.Value = true;
                    break;
                case GearKind.FlashlightBattery:
                    SpareBatteries.Value++;
                    break;
            }
            return true;
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (IsServer)
            {
                TickFlashlight(Time.deltaTime);
                TickBandage();
            }

            if (IsOwner)
                ReadInput();
            else if (FlashlightOn.Value)
                // У чужих камера выключена; поворачиваем её по взгляду, чтобы луч светил туда, куда игрок смотрит.
                cameraTransform.localRotation = Quaternion.Euler(controller.LookPitch.Value, 0f, 0f);
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            LookingAtTeammate = false;
            if (keyboard == null || mouse == null || survival.IsDead)
                return;

            LookingAtTeammate = FindTeammateInView() != null;
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                Selected = Slot.Bandage;
            else if (keyboard.digit2Key.wasPressedThisFrame)
                Selected = Slot.Flashlight;
            else if (keyboard.digit3Key.wasPressedThisFrame)
                Selected = Slot.Crowbar;

            if (keyboard.fKey.wasPressedThisFrame && HasFlashlight.Value)
                ToggleFlashlightRpc();

            if (!mouse.rightButton.wasPressedThisFrame)
                return;

            switch (Selected)
            {
                case Slot.Bandage when Bandages.Value > 0 && BandageProgress.Value <= 0f:
                    var teammate = FindTeammateInView();
                    BandageRpc(teammate != null ? teammate.NetworkObject : NetworkObject);
                    break;
                case Slot.Flashlight when HasFlashlight.Value:
                    ToggleFlashlightRpc();
                    break;
                case Slot.Crowbar when HasCrowbar.Value:
                    SwingRpc();
                    break;
            }
        }

        private PlayerSurvival FindTeammateInView()
        {
            if (!Physics.Raycast(controller.LookOrigin, cameraTransform.forward, out var hit, bandageReach + 0.4f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;
            var other = hit.collider.GetComponentInParent<PlayerSurvival>();
            return other != null && other != survival && !other.IsDead ? other : null;
        }

        // ---------- Бинт ----------

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void BandageRpc(NetworkObjectReference target)
        {
            if (survival.IsDead || Bandages.Value <= 0 || bandageTarget != null)
                return;
            if (!target.TryGet(out var targetObject) || !targetObject.TryGetComponent(out PlayerSurvival patient) || patient.IsDead)
                return;
            if (patient != survival && Vector3.Distance(patient.transform.position, transform.position) > bandageReach + 1f)
                return;

            bandageTarget = patient;
            bandageStart = Time.time;
            bandagePosition = transform.position;
            BandageProgress.Value = 0.001f;
            HandsSoundRpc(0);
        }

        private void TickBandage()
        {
            if (bandageTarget == null)
                return;

            bool interrupted = survival.IsDead || bandageTarget.IsDead
                || Vector3.Distance(transform.position, bandagePosition) > bandageMoveTolerance
                || Vector3.Distance(transform.position, bandageTarget.transform.position) > bandageReach + 1f;
            if (interrupted)
            {
                bandageTarget = null;
                BandageProgress.Value = 0f;
                return;
            }

            float progress = (Time.time - bandageStart) / bandageTime;
            if (progress < 1f)
            {
                BandageProgress.Value = Mathf.Max(0.001f, progress);
                return;
            }

            bandageTarget.Heal(bandageHeal);
            Bandages.Value--;
            bandageTarget = null;
            BandageProgress.Value = 0f;
            HandsSoundRpc(1);
        }

        // ---------- Фонарь ----------

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ToggleFlashlightRpc()
        {
            if (survival.IsDead || !HasFlashlight.Value)
                return;
            if (!FlashlightOn.Value && FlashlightCharge.Value <= 0f && !TryReload())
            {
                HandsSoundRpc(2);
                return;
            }

            FlashlightOn.Value = !FlashlightOn.Value;
            NoiseEvents.Emit(controller.LookOrigin, clickNoiseRadius);
            HandsSoundRpc(2);
        }

        private void TickFlashlight(float deltaTime)
        {
            if (!FlashlightOn.Value)
                return;

            FlashlightCharge.Value = Mathf.Max(0f, FlashlightCharge.Value - deltaTime);
            if (FlashlightCharge.Value > 0f)
                return;

            // Села батарейка — сразу ставим запасную, если есть.
            if (TryReload())
                HandsSoundRpc(2);
            else
                FlashlightOn.Value = false;
        }

        private bool TryReload()
        {
            if (SpareBatteries.Value <= 0)
                return false;
            SpareBatteries.Value--;
            FlashlightCharge.Value = flashlightDuration;
            return true;
        }

        private void OnFlashlightChanged(bool previous, bool current)
        {
            if (flashlight != null)
                flashlight.enabled = current;
        }

        // ---------- Лом ----------

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void SwingRpc()
        {
            if (survival.IsDead || !HasCrowbar.Value)
                return;

            Vector3 origin = controller.LookOrigin;
            Vector3 direction = controller.LookDirection;
            foreach (var blindOne in FindObjectsByType<BlindOne>(FindObjectsSortMode.None))
            {
                if (blindOne.CurrentState.Value == BlindOne.State.Stunned)
                    continue;

                // Туша длинная: ищем ближайшую точку на её оси от хвоста до морды.
                Vector3 tail = blindOne.BodyCenter - blindOne.transform.forward * 1.2f;
                Vector3 snout = blindOne.BodyCenter + blindOne.transform.forward * 1.6f;
                Vector3 point = ClosestOnSegment(tail, snout, origin);
                Vector3 toPoint = point - origin;
                if (toPoint.magnitude > crowbarReach + 0.8f || Vector3.Dot(direction, toPoint.normalized) < 0.3f)
                    continue;

                blindOne.Stun(stunTime);
                HasCrowbar.Value = false;
                CrowbarHitRpc(point);
                return;
            }
        }

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CrowbarHitRpc(Vector3 point)
        {
            var sound = new GameObject("Crowbar Hit");
            sound.transform.position = point;
            var source = SoundSources.Create3D(sound, maxDistance: 30f);
            source.PlayOneShot(ProceduralAudio.HitFlesh);
            source.PlayOneShot(ProceduralAudio.ImpactTin, 0.8f);
            Destroy(sound, 3f);
        }

        // ---------- Звуки и смерть ----------

        /// <summary>0 — начал перевязку, 1 — закончил, 2 — щелчок фонаря.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void HandsSoundRpc(byte kind)
        {
            switch (kind)
            {
                case 0:
                    hands.PlayOneShot(ProceduralAudio.Pickup, 0.6f);
                    break;
                case 1:
                    hands.PlayOneShot(ProceduralAudio.FilterAttach, 0.5f);
                    break;
                default:
                    hands.PlayOneShot(ProceduralAudio.Click, 0.7f);
                    break;
            }
        }

        private void OnHealthChanged(float previous, float current)
        {
            if (!IsServer || current > 0f || previous <= 0f)
                return;

            bandageTarget = null;
            BandageProgress.Value = 0f;
            FlashlightOn.Value = false;

            // Снаряжение погибшего выпадает рядом с телом — его могут подобрать живые.
            for (int i = 0; i < Bandages.Value; i++)
                Drop(bandagePrefab);
            if (HasFlashlight.Value)
            {
                var dropped = Drop(flashlightPrefab);
                if (dropped != null && dropped.TryGetComponent(out GearPickup pickup))
                    pickup.Charge = FlashlightCharge.Value;
            }
            if (HasCrowbar.Value)
                Drop(crowbarPrefab);
            for (int i = 0; i < SpareBatteries.Value; i++)
                Drop(batteryPrefab);

            Bandages.Value = 0;
            HasFlashlight.Value = false;
            HasCrowbar.Value = false;
            SpareBatteries.Value = 0;
            FlashlightCharge.Value = 0f;
        }

        private GameObject Drop(GameObject prefab)
        {
            if (prefab == null)
                return null;
            Vector2 offset = Random.insideUnitCircle * 0.6f;
            var instance = Instantiate(prefab, transform.position + new Vector3(offset.x, 0.4f, offset.y), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            instance.GetComponent<NetworkObject>().Spawn(true);
            return instance;
        }
    }
}
