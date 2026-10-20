using Nuclear.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nuclear.Player
{
    /// <summary>
    /// Подбор и переноска предметов. Владелец выбирает предмет взглядом и просит сервер;
    /// сервер решает, кто что держит, и сам двигает предмет.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCarry : NetworkBehaviour
    {
        private const ulong NoItem = ulong.MaxValue;

        [SerializeField] private float grabDistance = 2.5f;
        [Tooltip("Если предмет замедляет сильнее этого, бегать и прыгать с ним нельзя.")]
        [SerializeField, Range(0f, 1f)] private float heavyThreshold = 0.75f;

        private readonly NetworkVariable<ulong> heldItemId = new(NoItem);

        public PlayerController Controller { get; private set; }

        /// <summary>
        /// Предмет, на который смотрит свой игрок (для подсказок).
        /// Предмет могут уничтожить посреди кадра (например, фильтр ушёл в карман),
        /// поэтому уничтоженный возвращается как настоящий null.
        /// </summary>
        public PhysicsItem LookTarget
        {
            get => lookTarget != null ? lookTarget : null;
            private set => lookTarget = value;
        }

        private PhysicsItem lookTarget;

        public PhysicsItem HeldItem
        {
            get
            {
                if (heldItemId.Value == NoItem || NetworkManager == null
                    || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(heldItemId.Value, out var heldObject)
                    || heldObject == null)
                    return null;

                var item = heldObject.GetComponent<PhysicsItem>();
                return item != null ? item : null;
            }
        }

        public float SpeedMultiplier => HeldItem is { } item ? item.SpeedMultiplier : 1f;
        public bool IsHeavyLoad => SpeedMultiplier < heavyThreshold;

        private CharacterController body;
        private PlayerSurvival survival;
        private PhysicsItem ignoredItem;

        private void Awake()
        {
            Controller = GetComponent<PlayerController>();
            body = GetComponent<CharacterController>();
            survival = GetComponent<PlayerSurvival>();
        }

        public override void OnNetworkSpawn()
        {
            heldItemId.OnValueChanged += OnHeldItemChanged;
            UpdateIgnoredCollisions();
        }

        public override void OnNetworkDespawn()
        {
            heldItemId.OnValueChanged -= OnHeldItemChanged;

            if (IsServer && HeldItem is { } item)
                item.RemoveCarrier(this, false);
        }

        /// <summary>Сервер: точка перед игроком, куда тянется предмет.</summary>
        public Vector3 HoldPoint(PhysicsItem item) =>
            Controller.LookOrigin + Controller.LookDirection * item.HoldDistance;

        /// <summary>Сервер: предмет сообщает, что игрок его больше не держит.</summary>
        internal void OnReleasedByItem()
        {
            heldItemId.Value = NoItem;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            if (survival != null && survival.IsDead)
            {
                LookTarget = null;
                return;
            }

            LookTarget = FindItemInView();

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            var held = HeldItem;
            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (held != null)
                    ReleaseRpc(false);
                else if (LookTarget != null && !LookTarget.IsFull)
                    GrabRpc(LookTarget.NetworkObjectId, TryGetComponent(out PlayerGear gear) ? gear.Selected : -1);
            }
            else if (mouse.leftButton.wasPressedThisFrame && held != null && held.CanThrow)
            {
                ReleaseRpc(true);
            }
        }

        private PhysicsItem FindItemInView()
        {
            if (!Physics.Raycast(Controller.LookOrigin, Controller.LookDirection, out var hit, grabDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;

            return hit.collider.GetComponentInParent<PhysicsItem>();
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void GrabRpc(ulong itemId, int preferredSlot)
        {
            if (HeldItem != null || (survival != null && survival.IsDead))
                return;

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemId, out var itemObject)
                || !itemObject.TryGetComponent(out PhysicsItem item))
                return;

            // Запас на сетевую задержку: клиент видел предмет чуть в другом месте.
            float distance = Vector3.Distance(Controller.LookOrigin, item.ClosestPoint(Controller.LookOrigin));
            if (distance > grabDistance + 1f)
                return;

            // Мелкий предмет уходит в инвентарь (в выбранную ячейку, если она свободна);
            // если ячеек нет, его можно нести как обычный предмет.
            if (item.TryGetComponent(out GearPickup gearPickup) && TryGetComponent(out PlayerGear gear) && gear.TryStash(gearPickup, preferredSlot))
            {
                itemObject.Despawn();
                return;
            }

            if (item.TryAddCarrier(this))
                heldItemId.Value = itemId;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ReleaseRpc(bool throwItem)
        {
            if (HeldItem is { } item)
                item.RemoveCarrier(this, throwItem);
        }

        private void OnHeldItemChanged(ulong previous, ulong current) => UpdateIgnoredCollisions();

        /// <summary>Несущий не должен толкать свой же предмет, иначе тот застревает в теле.</summary>
        private void UpdateIgnoredCollisions()
        {
            var item = HeldItem;
            if (item == ignoredItem)
                return;

            if (ignoredItem != null)
                ignoredItem.SetCollisionIgnored(body, false);
            if (item != null)
                item.SetCollisionIgnored(body, true);

            ignoredItem = item;
        }
    }
}
