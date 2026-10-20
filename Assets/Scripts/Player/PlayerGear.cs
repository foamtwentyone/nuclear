using System;
using Nuclear.Audio;
using Nuclear.Enemies;
using Nuclear.Items;
using Nuclear.Noise;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Nuclear.Player
{
    /// <summary>Содержимое одной ячейки инвентаря.</summary>
    public struct InventorySlot : INetworkSerializeByMemcpy, IEquatable<InventorySlot>
    {
        public bool Filled;
        public GearKind Kind;
        /// <summary>Заряд фонаря в секундах (для остальных предметов не используется).</summary>
        public float Charge;

        public static InventorySlot Of(GearKind kind, float charge = 0f) => new() { Filled = true, Kind = kind, Charge = charge };

        public bool Is(GearKind kind) => Filled && Kind == kind;

        public bool Equals(InventorySlot other) => Filled == other.Filled && Kind == other.Kind && Charge.Equals(other.Charge);
    }

    /// <summary>
    /// Инвентарь на 4 ячейки: любой мелкий предмет (бинт, фонарь, лом, батарейка, фильтр, консерва)
    /// кладётся в любую свободную. 1–4 или колесо мыши выбирают ячейку, ПКМ применяет выбранный предмет,
    /// Q выбрасывает его вперёд, F включает фонарь, R меняет фильтр противогаза из инвентаря.
    /// Всё решает сервер; при смерти содержимое выпадает рядом с телом.
    /// </summary>
    [RequireComponent(typeof(PlayerSurvival), typeof(PlayerController))]
    public class PlayerGear : NetworkBehaviour
    {
        public const int SlotCount = 4;

        [Header("Бинт")]
        [SerializeField] private float bandageHeal = 40f;
        [SerializeField] private float bandageTime = 4f;
        [SerializeField] private float bandageReach = 2f;
        [Tooltip("Если перевязывающий сдвинулся дальше этого (м), перевязка прерывается.")]
        [SerializeField] private float bandageMoveTolerance = 0.3f;

        [Header("Фонарь")]
        [SerializeField] private float flashlightDuration = 180f;
        [SerializeField] private float clickNoiseRadius = 3f;

        [Header("Лом")]
        [SerializeField] private float crowbarReach = 2.2f;
        [SerializeField] private float stunTime = 3f;

        [Header("Выбрасывание")]
        [SerializeField] private float dropSpeed = 3.5f;
        [Tooltip("Консерву бросают далеко — чтобы отвлечь Слепыша стуком.")]
        [SerializeField] private float canThrowSpeed = 10f;

        [Header("Префабы предметов (выбрасывание и смерть)")]
        [SerializeField] private GameObject bandagePrefab;
        [SerializeField] private GameObject flashlightPrefab;
        [SerializeField] private GameObject crowbarPrefab;
        [SerializeField] private GameObject batteryPrefab;
        [SerializeField] private GameObject filterPrefab;
        [SerializeField] private GameObject canPrefab;

        // Ячейки — отдельные поля: Netcode находит NetworkVariable только среди полей, не в массивах.
        private readonly NetworkVariable<InventorySlot> slot0 = new();
        private readonly NetworkVariable<InventorySlot> slot1 = new();
        private readonly NetworkVariable<InventorySlot> slot2 = new();
        private readonly NetworkVariable<InventorySlot> slot3 = new();

        /// <summary>Какая ячейка светит (−1 — фонарь выключен).</summary>
        public readonly NetworkVariable<int> LightSlot = new(-1);
        /// <summary>Ячейка, предмет из которой в руке. Пишет владелец, видят все (модель в руке у других).</summary>
        public readonly NetworkVariable<int> HeldSlot = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Перевязка: 0 — не идёт, иначе доля 0..1.</summary>
        public readonly NetworkVariable<float> BandageProgress = new();

        /// <summary>Выбранная ячейка — только у владельца.</summary>
        public int Selected { get; private set; }

        /// <summary>Свой игрок смотрит на товарища, которого можно перевязать (для подсказки).</summary>
        public bool LookingAtTeammate { get; private set; }

        public float FlashlightDuration => flashlightDuration;
        public bool FlashlightOn => LightSlot.Value >= 0;
        public bool HasCrowbar => IndexOf(GearKind.Crowbar) >= 0;
        public bool HasSpace => IndexOf(null) >= 0;

        public InventorySlot this[int index] => Slot(index).Value;

        private PlayerSurvival survival;
        private PlayerController controller;
        private Light flashlight;
        private Transform cameraTransform;
        private AudioSource hands;
        private AudioSource personal;

        // Модели предметов в руке: от первого лица (у камеры) и для остальных (в руке тела).
        private Transform viewmodel, hand;
        private Vector3 viewmodelRest;
        private string shownHeld;
        private Vector3 lastPosition;
        private float bobPhase, moveAmount;
        private Vector2 sway;

        private PlayerSurvival bandageTarget;
        private int bandageSlot;
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
            viewmodel = cameraTransform.Find("Viewmodel");
            hand = transform.Find("Body/Hand");
            if (viewmodel != null)
                viewmodelRest = viewmodel.localPosition;
            hands = SoundSources.Create3D(gameObject, maxDistance: 15f);
            personal = SoundSources.Create2D(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            LightSlot.OnValueChanged += OnLightChanged;
            survival.Health.OnValueChanged += OnHealthChanged;
            for (int i = 0; i < SlotCount; i++)
                Slot(i).OnValueChanged += OnSlotChanged;
            OnLightChanged(-1, LightSlot.Value);
        }

        public override void OnNetworkDespawn()
        {
            LightSlot.OnValueChanged -= OnLightChanged;
            survival.Health.OnValueChanged -= OnHealthChanged;
            for (int i = 0; i < SlotCount; i++)
                Slot(i).OnValueChanged -= OnSlotChanged;
        }

        private NetworkVariable<InventorySlot> Slot(int index) => index switch
        {
            0 => slot0,
            1 => slot1,
            2 => slot2,
            _ => slot3,
        };

        /// <summary>Первая ячейка с таким предметом (null — первая пустая) или −1.</summary>
        public int IndexOf(GearKind? kind)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = Slot(i).Value;
                if (kind == null ? !slot.Filled : slot.Is(kind.Value))
                    return i;
            }
            return -1;
        }

        public int Count(GearKind kind)
        {
            int count = 0;
            for (int i = 0; i < SlotCount; i++)
                count += Slot(i).Value.Is(kind) ? 1 : 0;
            return count;
        }

        /// <summary>Сервер: кладёт предмет в предпочтительную ячейку, если она свободна, иначе в первую свободную.</summary>
        public bool TryStash(GearPickup pickup, int preferred)
        {
            if (survival.IsDead)
                return false;
            int index = preferred >= 0 && preferred < SlotCount && !Slot(preferred).Value.Filled ? preferred : IndexOf(null);
            if (index < 0)
                return false;

            float charge = pickup.Kind == GearKind.Flashlight ? (pickup.Charge < 0f ? flashlightDuration : pickup.Charge) : 0f;
            Slot(index).Value = InventorySlot.Of(pickup.Kind, charge);
            return true;
        }

        private void Clear(int index)
        {
            Slot(index).Value = default;
            if (LightSlot.Value == index)
                LightSlot.Value = -1;
            if (bandageTarget != null && bandageSlot == index)
                CancelBandage();
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
            {
                ReadInput();
                if (HeldSlot.Value != Selected)
                    HeldSlot.Value = Selected;
            }
            UpdateHeldModel();
            if (IsOwner)
                AnimateViewmodel();
            else if (FlashlightOn)
                // У чужих камера выключена; поворачиваем её по взгляду, чтобы луч светил туда, куда игрок смотрит.
                cameraTransform.localRotation = Quaternion.Euler(controller.LookPitch.Value, 0f, 0f);
        }

        // ---------- Предмет в руке ----------

        private void UpdateHeldModel()
        {
            var slot = Slot(Mathf.Clamp(HeldSlot.Value, 0, SlotCount - 1)).Value;
            string held = slot.Filled && !survival.IsDead ? slot.Kind.ToString() : null;
            if (held == shownHeld)
                return;
            shownHeld = held;
            // Свой предмет — у камеры, чужой — в руке модели.
            Show(viewmodel, IsOwner ? held : null);
            Show(hand, IsOwner ? null : held);
        }

        private static void Show(Transform holder, string name)
        {
            if (holder == null)
                return;
            foreach (Transform child in holder)
                child.gameObject.SetActive(child.name == name);
        }

        /// <summary>Предмет у камеры покачивается при ходьбе и чуть отстаёт от поворота мыши.</summary>
        private void AnimateViewmodel()
        {
            if (viewmodel == null)
                return;
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            float speed = delta.magnitude / deltaTime;
            moveAmount = Mathf.Lerp(moveAmount, Mathf.Clamp01(speed / 6f), 1f - Mathf.Exp(-8f * deltaTime));
            bobPhase += deltaTime * (4f + speed * 1.6f);

            var mouse = Mouse.current;
            Vector2 look = mouse != null && Cursor.lockState == CursorLockMode.Locked ? mouse.delta.ReadValue() : Vector2.zero;
            sway = Vector2.Lerp(sway, Vector2.ClampMagnitude(look * 0.02f, 1f), 1f - Mathf.Exp(-10f * deltaTime));

            var bob = new Vector3(Mathf.Sin(bobPhase) * 0.012f, -Mathf.Abs(Mathf.Cos(bobPhase)) * 0.014f, 0f) * moveAmount;
            viewmodel.localPosition = viewmodelRest + bob + new Vector3(-sway.x, -sway.y, 0f) * 0.03f;
            viewmodel.localRotation = Quaternion.Euler(sway.y * 4f, -sway.x * 4f, 0f);
        }

        // ---------- Ввод ----------

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
                Selected = 0;
            else if (keyboard.digit2Key.wasPressedThisFrame)
                Selected = 1;
            else if (keyboard.digit3Key.wasPressedThisFrame)
                Selected = 2;
            else if (keyboard.digit4Key.wasPressedThisFrame)
                Selected = 3;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                Selected = (Selected + (scroll < 0f ? 1 : SlotCount - 1)) % SlotCount;

            if (keyboard.fKey.wasPressedThisFrame)
            {
                int light = FlashlightOn ? LightSlot.Value : this[Selected].Is(GearKind.Flashlight) ? Selected : IndexOf(GearKind.Flashlight);
                if (light >= 0)
                    ToggleFlashlightRpc(light);
            }

            if (keyboard.rKey.wasPressedThisFrame && IndexOf(GearKind.Filter) >= 0 && !survival.IsSwappingFilter.Value)
                SwapFilterRpc(this[Selected].Is(GearKind.Filter) ? Selected : IndexOf(GearKind.Filter));

            if (keyboard.qKey.wasPressedThisFrame && this[Selected].Filled)
                DropRpc(Selected);

            if (mouse.rightButton.wasPressedThisFrame)
                UseSelected();
        }

        private void UseSelected()
        {
            var slot = this[Selected];
            if (!slot.Filled)
                return;
            switch (slot.Kind)
            {
                case GearKind.Bandage when BandageProgress.Value <= 0f:
                    var teammate = FindTeammateInView();
                    BandageRpc(Selected, teammate != null ? teammate.NetworkObject : NetworkObject);
                    break;
                case GearKind.Flashlight:
                    ToggleFlashlightRpc(Selected);
                    break;
                case GearKind.Crowbar:
                    SwingRpc(Selected);
                    break;
                case GearKind.Filter when !survival.IsSwappingFilter.Value:
                    SwapFilterRpc(Selected);
                    break;
                case GearKind.FlashlightBattery when IndexOf(GearKind.Flashlight) >= 0:
                    InsertBatteryRpc(Selected);
                    break;
            }
        }

        /// <summary>Что сделает ПКМ с выбранным предметом — для подсказки (null — ничего).</summary>
        public string UseHint()
        {
            var slot = this[Selected];
            if (!slot.Filled)
                return null;
            return slot.Kind switch
            {
                GearKind.Bandage => LookingAtTeammate ? "перевязать товарища" : "перевязать себя",
                GearKind.Flashlight => FlashlightOn && LightSlot.Value == Selected ? "выключить фонарь" : "включить фонарь",
                GearKind.Crowbar => "ударить Слепыша (оглушит, лом сломается)",
                GearKind.Filter => "сменить фильтр противогаза",
                GearKind.FlashlightBattery => IndexOf(GearKind.Flashlight) >= 0 ? "вставить в фонарь" : null,
                _ => null,
            };
        }

        private PlayerSurvival FindTeammateInView()
        {
            if (!Physics.Raycast(controller.LookOrigin, cameraTransform.forward, out var hit, bandageReach + 0.4f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;
            var other = hit.collider.GetComponentInParent<PlayerSurvival>();
            return other != null && other != survival && !other.IsDead ? other : null;
        }

        private bool Holds(int index, GearKind kind) => index >= 0 && index < SlotCount && Slot(index).Value.Is(kind);

        // ---------- Выбрасывание ----------

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void DropRpc(int index)
        {
            if (survival.IsDead || index < 0 || index >= SlotCount || !Slot(index).Value.Filled)
                return;

            var slot = Slot(index).Value;
            Clear(index);
            Vector3 origin = controller.LookOrigin + controller.LookDirection * 0.5f;
            var instance = Spawn(slot, origin);
            if (instance != null && instance.TryGetComponent(out Rigidbody body))
                body.AddForce(controller.LookDirection * (slot.Kind == GearKind.Can ? canThrowSpeed : dropSpeed) + Vector3.up * 1.5f, ForceMode.VelocityChange);
        }

        // ---------- Бинт ----------

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void BandageRpc(int index, NetworkObjectReference target)
        {
            if (survival.IsDead || !Holds(index, GearKind.Bandage) || bandageTarget != null)
                return;
            if (!target.TryGet(out var targetObject) || !targetObject.TryGetComponent(out PlayerSurvival patient) || patient.IsDead)
                return;
            if (patient != survival && Vector3.Distance(patient.transform.position, transform.position) > bandageReach + 1f)
                return;

            bandageTarget = patient;
            bandageSlot = index;
            bandageStart = Time.time;
            bandagePosition = transform.position;
            BandageProgress.Value = 0.001f;
            HandsSoundRpc(0);
        }

        private void TickBandage()
        {
            if (bandageTarget == null)
                return;

            bool interrupted = survival.IsDead || bandageTarget.IsDead || !Holds(bandageSlot, GearKind.Bandage)
                || Vector3.Distance(transform.position, bandagePosition) > bandageMoveTolerance
                || Vector3.Distance(transform.position, bandageTarget.transform.position) > bandageReach + 1f;
            if (interrupted)
            {
                CancelBandage();
                return;
            }

            float progress = (Time.time - bandageStart) / bandageTime;
            if (progress < 1f)
            {
                BandageProgress.Value = Mathf.Max(0.001f, progress);
                return;
            }

            var patient = bandageTarget;
            bandageTarget = null;
            BandageProgress.Value = 0f;
            Slot(bandageSlot).Value = default;
            patient.Heal(bandageHeal);
            HandsSoundRpc(1);
        }

        private void CancelBandage()
        {
            bandageTarget = null;
            BandageProgress.Value = 0f;
        }

        // ---------- Фильтр ----------

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void SwapFilterRpc(int index)
        {
            if (!Holds(index, GearKind.Filter) || !survival.BeginFilterSwap())
                return;
            Slot(index).Value = default;
        }

        // ---------- Фонарь ----------

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ToggleFlashlightRpc(int index)
        {
            if (survival.IsDead || !Holds(index, GearKind.Flashlight))
                return;

            if (LightSlot.Value == index)
                LightSlot.Value = -1;
            else if (Slot(index).Value.Charge > 0f || TryReload(index))
                LightSlot.Value = index;
            NoiseEvents.Emit(controller.LookOrigin, clickNoiseRadius);
            HandsSoundRpc(2);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void InsertBatteryRpc(int index)
        {
            if (survival.IsDead || !Holds(index, GearKind.FlashlightBattery))
                return;

            // Батарейка — в самый разряженный фонарь.
            int target = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (Slot(i).Value.Is(GearKind.Flashlight) && (target < 0 || Slot(i).Value.Charge < Slot(target).Value.Charge))
                    target = i;
            }
            if (target < 0)
                return;
            Slot(index).Value = default;
            SetCharge(target, flashlightDuration);
            HandsSoundRpc(2);
        }

        private void TickFlashlight(float deltaTime)
        {
            int index = LightSlot.Value;
            if (index < 0)
                return;
            if (!Holds(index, GearKind.Flashlight))
            {
                LightSlot.Value = -1;
                return;
            }

            // Netcode рассылает изменения раз в сетевой тик, так что запись каждый кадр не нагружает сеть.
            float charge = Mathf.Max(0f, Slot(index).Value.Charge - deltaTime);
            SetCharge(index, charge);
            if (charge > 0f)
                return;

            // Села батарейка — сразу ставим запасную из инвентаря, если есть.
            if (TryReload(index))
                HandsSoundRpc(2);
            else
                LightSlot.Value = -1;
        }

        private void SetCharge(int index, float charge)
        {
            var slot = Slot(index).Value;
            slot.Charge = charge;
            Slot(index).Value = slot;
        }

        private bool TryReload(int flashlightIndex)
        {
            int cell = IndexOf(GearKind.FlashlightBattery);
            if (cell < 0)
                return false;
            Slot(cell).Value = default;
            SetCharge(flashlightIndex, flashlightDuration);
            return true;
        }

        private void OnLightChanged(int previous, int current)
        {
            if (flashlight != null)
                flashlight.enabled = current >= 0;
        }

        // ---------- Лом ----------

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void SwingRpc(int index)
        {
            if (survival.IsDead || !Holds(index, GearKind.Crowbar))
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
                Clear(index);
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

        private void OnSlotChanged(InventorySlot previous, InventorySlot current)
        {
            // Свой игрок слышит, как предмет лёг в инвентарь.
            if (IsOwner && current.Filled && (!previous.Filled || previous.Kind != current.Kind))
                personal.PlayOneShot(ProceduralAudio.Pickup, 0.7f);
        }

        private void OnHealthChanged(float previous, float current)
        {
            if (!IsServer || current > 0f || previous <= 0f)
                return;

            CancelBandage();
            LightSlot.Value = -1;

            // Всё из инвентаря выпадает рядом с телом — его могут подобрать живые.
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = Slot(i).Value;
                if (!slot.Filled)
                    continue;
                Vector2 offset = Random.insideUnitCircle * 0.6f;
                Spawn(slot, transform.position + new Vector3(offset.x, 0.4f, offset.y));
                Slot(i).Value = default;
            }
        }

        private GameObject Spawn(InventorySlot slot, Vector3 position)
        {
            var prefab = slot.Kind switch
            {
                GearKind.Bandage => bandagePrefab,
                GearKind.Flashlight => flashlightPrefab,
                GearKind.Crowbar => crowbarPrefab,
                GearKind.FlashlightBattery => batteryPrefab,
                GearKind.Filter => filterPrefab,
                GearKind.Can => canPrefab,
                _ => null,
            };
            if (prefab == null)
                return null;

            var instance = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            if (slot.Kind == GearKind.Flashlight && instance.TryGetComponent(out GearPickup pickup))
                pickup.Charge = slot.Charge;
            instance.GetComponent<NetworkObject>().Spawn(true);
            return instance;
        }
    }
}
