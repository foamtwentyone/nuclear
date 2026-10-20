using Nuclear.Audio;
using Nuclear.Items;
using Nuclear.Noise;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Nuclear.Level
{
    /// <summary>
    /// Дверь Бункера-12. Аккумулятор, поднесённый к щитку, встаёт на место сам.
    /// Дверь открывается медленно (чем меньше заряд, тем дольше) и громко гудит,
    /// приманивая Слепыша, — это время нужно продержаться. Разряженный аккумулятор щиток не принимает.
    /// </summary>
    public class BunkerDoor : NetworkBehaviour
    {
        public enum DoorState : byte
        {
            Locked,
            Opening,
            Open,
        }

        [SerializeField] private float insertRadius = 1f;
        [Tooltip("Время открытия при полном заряде.")]
        [SerializeField] private float fastestOpenTime = 30f;
        [Tooltip("Время открытия при почти нулевом заряде.")]
        [SerializeField] private float slowestOpenTime = 60f;
        [SerializeField] private float panelTravel = 2.6f;
        [SerializeField] private float noiseRadius = 25f;
        [SerializeField] private float noiseInterval = 3f;

        /// <summary>Дверь текущего уровня, для HUD и условий победы.</summary>
        public static BunkerDoor Current { get; private set; }

        public readonly NetworkVariable<DoorState> State = new();
        public readonly NetworkVariable<float> Progress = new();

        public bool IsOpen => State.Value == DoorState.Open;

        /// <summary>Когда щиток последний раз отверг разряженный аккумулятор (локальное время).</summary>
        public float LastRejectTime { get; private set; } = float.NegativeInfinity;

        public Vector3 SocketPosition => socket != null ? socket.position : transform.position;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Transform panel;
        private NavMeshObstacle panelObstacle;
        private Transform socket;
        private GameObject insertedBattery;
        private Renderer lamp;
        private MaterialPropertyBlock lampBlock;
        private AudioSource rumble;
        private Vector3 panelClosedPosition;
        private float openDuration;
        private float nextNoiseTime;
        private float nextRejectTime;

        private void Awake()
        {
            panel = transform.Find("Panel");
            socket = transform.Find("Socket");
            insertedBattery = transform.Find("Inserted Battery")?.gameObject;
            lamp = transform.Find("Lamp")?.GetComponent<Renderer>();
            lampBlock = new MaterialPropertyBlock();
            if (panel != null)
            {
                panelClosedPosition = panel.localPosition;
                panelObstacle = panel.GetComponent<NavMeshObstacle>();
            }

            rumble = gameObject.AddComponent<AudioSource>();
            rumble.clip = ProceduralAudio.Rumble;
            rumble.loop = true;
            rumble.spatialBlend = 1f;
            rumble.rolloffMode = AudioRolloffMode.Linear;
            rumble.minDistance = 2f;
            rumble.maxDistance = 40f;
            rumble.playOnAwake = false;
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            State.OnValueChanged += OnStateChanged;
            ApplyVisuals();
        }

        public override void OnNetworkDespawn()
        {
            State.OnValueChanged -= OnStateChanged;
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (IsServer)
                ServerTick();

            if (panel != null)
                panel.localPosition = panelClosedPosition + Vector3.up * (Progress.Value * panelTravel);
        }

        private void ServerTick()
        {
            switch (State.Value)
            {
                case DoorState.Locked:
                    TryInsertBattery();
                    break;

                case DoorState.Opening:
                    Progress.Value = Mathf.Min(1f, Progress.Value + Time.deltaTime / openDuration);
                    if (Time.time >= nextNoiseTime)
                    {
                        nextNoiseTime = Time.time + noiseInterval;
                        NoiseEvents.Emit(transform.position + Vector3.up * 1.5f, noiseRadius);
                    }
                    if (Progress.Value >= 1f)
                        State.Value = DoorState.Open;
                    break;
            }
        }

        private void TryInsertBattery()
        {
            foreach (var hit in Physics.OverlapSphere(SocketPosition, insertRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var battery = hit.GetComponentInParent<Battery>();
                if (battery == null)
                    continue;

                if (battery.IsDead)
                {
                    if (Time.time >= nextRejectTime)
                    {
                        nextRejectTime = Time.time + 3f;
                        RejectRpc();
                    }
                    continue;
                }

                // Аккумулятор исчезает из рук и появляется в щитке как часть двери.
                openDuration = Mathf.Lerp(slowestOpenTime, fastestOpenTime, battery.Charge.Value);
                battery.NetworkObject.Despawn();
                nextNoiseTime = Time.time;
                State.Value = DoorState.Opening;
                return;
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RejectRpc()
        {
            LastRejectTime = Time.time;
        }

        private void OnStateChanged(DoorState previous, DoorState current) => ApplyVisuals();

        private void ApplyVisuals()
        {
            if (insertedBattery != null)
                insertedBattery.SetActive(State.Value != DoorState.Locked);

            if (panelObstacle != null)
                panelObstacle.enabled = State.Value != DoorState.Open;

            if (State.Value == DoorState.Opening)
                rumble.Play();
            else
                rumble.Stop();

            if (lamp == null)
                return;

            Color color = State.Value switch
            {
                DoorState.Opening => new Color(1f, 0.75f, 0.1f),
                DoorState.Open => new Color(0.2f, 1f, 0.3f),
                _ => new Color(1f, 0.15f, 0.1f),
            };
            lamp.GetPropertyBlock(lampBlock);
            lampBlock.SetColor(ColorId, color);
            lampBlock.SetColor(BaseColorId, color);
            lamp.SetPropertyBlock(lampBlock);
        }
    }
}
