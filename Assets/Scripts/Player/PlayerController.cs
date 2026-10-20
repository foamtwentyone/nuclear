using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nuclear.Player
{
    /// <summary>
    /// Игрок от первого лица: ходьба, бег, прыжок, обзор мышью.
    /// С тяжёлым грузом ходит медленнее и не может бегать и прыгать.
    /// Движение считает только владелец; позицию раздаёт OwnerNetworkTransform.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerStamina))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Движение")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float jumpStaminaCost = 15f;
        [SerializeField] private float gravity = -20f;

        [Header("Обзор")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float maxPitch = 85f;

        /// <summary>Бежит ли игрок. Видно всем: пригодится для анимаций и шума для Слепыша.</summary>
        public readonly NetworkVariable<bool> IsSprinting = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Наклон взгляда вверх-вниз. Нужен серверу, чтобы знать, куда игрок держит предмет.</summary>
        public readonly NetworkVariable<float> LookPitch = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public PlayerStamina Stamina { get; private set; }

        public Vector3 LookOrigin => cameraRoot.position;
        public Vector3 LookDirection => transform.rotation * Quaternion.Euler(LookPitch.Value, 0f, 0f) * Vector3.forward;

        private CharacterController controller;
        private PlayerCarry carry;
        private PlayerSurvival survival;
        private Transform cameraRoot;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float pitch;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            Stamina = GetComponent<PlayerStamina>();
            carry = GetComponent<PlayerCarry>();
            survival = GetComponent<PlayerSurvival>();

            var playerCamera = GetComponentInChildren<Camera>(true);
            cameraRoot = playerCamera.transform;
        }

        public override void OnNetworkSpawn()
        {
            var playerCamera = cameraRoot.GetComponent<Camera>();
            var listener = cameraRoot.GetComponent<AudioListener>();

            // Камера и слух только у своего игрока, иначе в сцене будет несколько камер.
            playerCamera.enabled = IsOwner;
            if (listener != null)
                listener.enabled = IsOwner;

            // Пепел падает вокруг своего игрока; у чужих он не нужен.
            var ash = transform.Find("Ash");
            if (ash != null)
                ash.gameObject.SetActive(IsOwner);

            if (!IsOwner)
                return;

            // Своё тело не рисуем, чтобы модель не мешала обзору.
            var body = transform.Find("Body");
            if (body != null)
            {
                foreach (var bodyRenderer in body.GetComponentsInChildren<Renderer>())
                    bodyRenderer.enabled = false;
            }

            // Разводим игроков по кругу, чтобы они не появлялись друг в друге.
            float angle = OwnerClientId * 90f * Mathf.Deg2Rad;
            Teleport(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f + Vector3.up * 0.1f);

            SetCursorLocked(true);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
                SetCursorLocked(false);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);

            if (Cursor.lockState == CursorLockMode.Locked)
                Look(mouse.delta.ReadValue());

            // Мёртвый может только смотреть по сторонам.
            if (survival != null && survival.IsDead)
                return;

            Move(keyboard, Time.deltaTime);
        }

        private void Look(Vector2 delta)
        {
            transform.Rotate(Vector3.up, delta.x * mouseSensitivity);
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -maxPitch, maxPitch);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (Mathf.Abs(LookPitch.Value - pitch) > 0.5f)
                LookPitch.Value = pitch;
        }

        private void Move(Keyboard keyboard, float deltaTime)
        {
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);

            bool grounded = controller.isGrounded;
            bool wantsSprint = keyboard.leftShiftKey.isPressed && input.y > 0f;
            bool heavyLoad = carry != null && carry.IsHeavyLoad;
            bool sprinting = wantsSprint && grounded && !heavyLoad && Stamina.CanSprint;

            if (sprinting)
                Stamina.DrainSprint(deltaTime);
            else
                Stamina.Tick(deltaTime);

            if (IsSprinting.Value != sprinting)
                IsSprinting.Value = sprinting;

            float speed = (sprinting ? sprintSpeed : walkSpeed) * (carry != null ? carry.SpeedMultiplier : 1f);
            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, acceleration * speed * deltaTime);

            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // Прижимаем к земле, чтобы isGrounded не мигал на склонах.

            if (grounded && !heavyLoad && keyboard.spaceKey.wasPressedThisFrame && Stamina.TrySpend(jumpStaminaCost))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalVelocity += gravity * deltaTime;

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime);
        }

        private void Teleport(Vector3 position)
        {
            // CharacterController перезаписывает позицию, пока включён.
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
