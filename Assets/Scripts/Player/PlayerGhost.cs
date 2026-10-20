using Nuclear.Audio;
using Nuclear.Noise;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nuclear.Player
{
    /// <summary>
    /// Призрак погибшего игрока. Тело остаётся лежать, а камера улетает вместе с призраком:
    /// он свободно летает, но не дальше 25 м от ближайшего живого. Ничего не берёт, Слепыш его не слышит.
    /// Раз в 2 минуты может издать «шорох» там, куда смотрит: для Слепыша это как брошенный предмет.
    /// Остальные видят призрака бледным огоньком.
    /// </summary>
    [RequireComponent(typeof(PlayerSurvival))]
    public class PlayerGhost : NetworkBehaviour
    {
        [SerializeField] private float flySpeed = 5f;
        [SerializeField] private float fastFlySpeed = 10f;
        [SerializeField] private float leashRadius = 25f;
        [SerializeField] private float maxHeight = 12f;
        [SerializeField] private float mouseSensitivity = 0.1f;

        [Header("Шорох")]
        [SerializeField] private float distractCooldown = 120f;
        [SerializeField] private float distractRange = 15f;
        [SerializeField] private float distractNoiseRadius = 12f;

        /// <summary>Где призрак. Пишет владелец; нужно остальным (огонёк) и серверу (проверка шороха).</summary>
        public readonly NetworkVariable<Vector3> Position = new(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Серверное время, с которого снова можно шуршать.</summary>
        public readonly NetworkVariable<double> DistractReadyAt = new();

        public float DistractCooldownLeft => IsSpawned
            ? Mathf.Max(0f, (float)(DistractReadyAt.Value - NetworkManager.ServerTime.Time))
            : 0f;

        /// <summary>Только для HUD: шорох ни до чего не достал.</summary>
        public float LastMissTime { get; private set; } = -10f;

        private PlayerSurvival survival;
        private Transform wisp;
        private Transform cameraTransform;
        private float yaw, pitch;
        private bool active;
        private PlayerSurvival[] players = new PlayerSurvival[0];
        private float nextPlayerScan;

        public bool IsActive => active;

        private void Awake()
        {
            survival = GetComponent<PlayerSurvival>();
            wisp = transform.Find("Ghost");
            cameraTransform = GetComponentInChildren<Camera>(true).transform;
            if (wisp != null)
            {
                // Огонёк живёт в мире сам по себе, а не на трупе.
                wisp.gameObject.SetActive(false);
            }
        }

        public override void OnNetworkSpawn()
        {
            survival.Health.OnValueChanged += OnHealthChanged;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            survival.Health.OnValueChanged -= OnHealthChanged;
            if (wisp != null && wisp.parent != transform)
                Destroy(wisp.gameObject);
        }

        private void OnHealthChanged(float previous, float current) => Refresh();

        private void Refresh()
        {
            bool dead = survival.IsDead;
            if (dead == active)
                return;
            active = dead;

            if (wisp != null)
            {
                if (dead)
                    wisp.SetParent(null, true);
                // Свой огонёк не рисуем: камера внутри него.
                wisp.gameObject.SetActive(dead && !IsOwner);
            }

            if (!IsOwner)
                return;

            // Своё тело живой игрок не видит (мешает обзору), а призрак — видит.
            var body = transform.Find("Body");
            if (body != null)
            {
                foreach (var bodyRenderer in body.GetComponentsInChildren<Renderer>())
                    bodyRenderer.enabled = dead;
            }

            if (dead)
            {
                // Призрак поднимается из тела и смотрит туда же, куда смотрел игрок.
                Vector3 start = transform.position + Vector3.up * 1.8f;
                yaw = transform.eulerAngles.y;
                pitch = 10f;
                cameraTransform.SetParent(null, true);
                cameraTransform.SetPositionAndRotation(start, Quaternion.Euler(pitch, yaw, 0f));
                Position.Value = start;
            }
            else
            {
                cameraTransform.SetParent(transform, false);
                cameraTransform.localPosition = new Vector3(0f, 1.6f, 0f);
                cameraTransform.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !active)
                return;

            if (IsOwner)
                Fly();
            else if (wisp != null)
                FollowWisp();
        }

        private void Fly()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            float deltaTime = Time.deltaTime;
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            if (cursorLocked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -85f, 85f);
            }
            var rotation = Quaternion.Euler(pitch, yaw, 0f);

            var input = new Vector3(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.spaceKey.isPressed ? 1f : 0f) - (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            input = Vector3.ClampMagnitude(input, 1f);
            float speed = keyboard.leftShiftKey.isPressed ? fastFlySpeed : flySpeed;
            Vector3 move = rotation * new Vector3(input.x, 0f, input.z) + Vector3.up * input.y;

            Vector3 position = Leash(cameraTransform.position + move * speed * deltaTime);
            cameraTransform.SetPositionAndRotation(position, rotation);

            if ((Position.Value - position).sqrMagnitude > 0.0025f)
                Position.Value = position;

            if (cursorLocked && mouse.leftButton.wasPressedThisFrame && DistractCooldownLeft <= 0f)
                TryDistract(rotation * Vector3.forward);
        }

        /// <summary>Держит призрака над землёй, не выше потолка и рядом с живыми.</summary>
        private Vector3 Leash(Vector3 position)
        {
            Vector3 anchor = NearestAnchor(position);
            Vector3 offset = position - anchor;
            if (offset.magnitude > leashRadius)
                position = anchor + offset.normalized * leashRadius;

            float ground = GroundHeight(position);
            position.y = Mathf.Clamp(position.y, ground + 0.4f, ground + maxHeight);
            return position;
        }

        /// <summary>Ближайший живой игрок; если живых нет — своё тело.</summary>
        private Vector3 NearestAnchor(Vector3 position)
        {
            if (Time.time >= nextPlayerScan)
            {
                nextPlayerScan = Time.time + 1f;
                players = FindObjectsByType<PlayerSurvival>(FindObjectsSortMode.None);
            }

            Vector3 best = transform.position + Vector3.up * 1.6f;
            float bestDistance = float.MaxValue;
            foreach (var player in players)
            {
                if (player == null || player.IsDead)
                    continue;
                Vector3 head = player.transform.position + Vector3.up * 1.6f;
                float distance = (head - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = head;
                }
            }
            return best;
        }

        private static float GroundHeight(Vector3 position)
        {
            var from = new Vector3(position.x, position.y + 40f, position.z);
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // Только неподвижная геометрия: не предметы и не игроки.
                if (hit.rigidbody == null && hit.collider is not CharacterController && hit.point.y <= position.y + 0.5f)
                    return hit.point.y;
            }
            return 0f;
        }

        private void TryDistract(Vector3 direction)
        {
            if (Physics.Raycast(cameraTransform.position, direction, out var hit, distractRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                DistractRpc(hit.point);
            else
                LastMissTime = Time.time;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void DistractRpc(Vector3 point)
        {
            if (!survival.IsDead || NetworkManager.ServerTime.Time < DistractReadyAt.Value)
                return;
            if (Vector3.Distance(point, Position.Value) > distractRange + 1f)
                return;

            DistractReadyAt.Value = NetworkManager.ServerTime.Time + distractCooldown;
            NoiseEvents.Emit(point, distractNoiseRadius);
            RustleRpc(point);
        }

        /// <summary>Шорох слышат и живые: так видно, куда призрак увёл Слепыша.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void RustleRpc(Vector3 point)
        {
            var sound = new GameObject("Ghost Rustle");
            sound.transform.position = point;
            var source = SoundSources.Create3D(sound, maxDistance: 30f);
            source.pitch = Random.Range(0.8f, 0.95f);
            source.PlayOneShot(ProceduralAudio.ImpactTin);
            source.PlayOneShot(ProceduralAudio.ImpactWood, 0.6f);
            Destroy(sound, 3f);
        }

        private void FollowWisp()
        {
            float bob = Mathf.Sin(Time.time * 2f + OwnerClientId) * 0.08f;
            Vector3 target = Position.Value + Vector3.up * bob;
            wisp.position = Vector3.Lerp(wisp.position, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        public override void OnDestroy()
        {
            if (wisp != null && wisp.parent != transform)
                Destroy(wisp.gameObject);
            if (cameraTransform != null && cameraTransform.parent != transform)
                Destroy(cameraTransform.gameObject);
            base.OnDestroy();
        }
    }
}
