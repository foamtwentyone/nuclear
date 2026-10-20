using System.Collections.Generic;
using Nuclear.Audio;
using Nuclear.Noise;
using Nuclear.Player;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Level
{
    /// <summary>
    /// Капот брошенной машины. Под одним из капотов (заранее неизвестно, каким) лежит аккумулятор,
    /// под остальными — иногда мелкий лут. Вскрыть: держать E, глядя на капот.
    /// С ломом — 2 секунды, но громко (шум на 15 м). Без лома — только вдвоём, 6 секунд, тихо (5 м).
    /// </summary>
    public class CarHood : NetworkBehaviour
    {
        [SerializeField] private float crowbarTime = 2f;
        [SerializeField] private float teamTime = 6f;
        [SerializeField] private float crowbarNoiseRadius = 15f;
        [SerializeField] private float teamNoiseRadius = 5f;
        [SerializeField] private float reach = 3f;
        [SerializeField] private float openAngle = -70f;

        [Header("Что внутри")]
        [SerializeField] private GameObject batteryPrefab;
        [SerializeField] private GameObject filterPrefab;
        [SerializeField] private GameObject bandagePrefab;
        [SerializeField] private GameObject cellPrefab;
        [Tooltip("Где лежит находка, в координатах машины.")]
        [SerializeField] private Vector3 lootPoint = new(0.48f, 0.92f, 1.65f);

        public readonly NetworkVariable<bool> Opened = new();
        public readonly NetworkVariable<float> Progress = new();
        /// <summary>Сколько игроков сейчас вскрывают и есть ли среди них лом — для подсказок.</summary>
        public readonly NetworkVariable<int> PryerCount = new();
        public readonly NetworkVariable<bool> WithCrowbar = new();

        private static readonly List<CarHood> Closed = new();
        private static bool batteryFound;

        private readonly HashSet<PlayerGear> pryers = new();
        private Transform hinge;
        private AudioSource sound;
        private float nextNoiseTime;
        private float angle;

        private void Awake()
        {
            hinge = transform.Find("Hinge");
            sound = SoundSources.Create3D(gameObject, maxDistance: 30f);
        }

        public override void OnNetworkSpawn()
        {
            Opened.OnValueChanged += OnOpenedChanged;
            angle = Opened.Value ? openAngle : 0f;
            ApplyAngle();
            if (!IsServer)
                return;
            if (Closed.Count == 0)
                batteryFound = false;
            Closed.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            Opened.OnValueChanged -= OnOpenedChanged;
            Closed.Remove(this);
        }

        /// <summary>Сервер: игрок начал или бросил вскрывать капот.</summary>
        public void SetPrying(PlayerGear player, bool prying)
        {
            if (prying && !Opened.Value && CanPry(player))
                pryers.Add(player);
            else
                pryers.Remove(player);
        }

        private bool CanPry(PlayerGear player)
        {
            if (player == null || !player.IsSpawned)
                return false;
            var survival = player.GetComponent<PlayerSurvival>();
            var carry = player.GetComponent<PlayerCarry>();
            return !survival.IsDead && carry.HeldItem == null
                && Vector3.Distance(player.transform.position, hinge.position) <= reach + 1f;
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            float target = Opened.Value ? openAngle : 0f;
            if (!Mathf.Approximately(angle, target))
            {
                angle = Mathf.MoveTowards(angle, target, 160f * Time.deltaTime);
                ApplyAngle();
            }

            if (IsServer && !Opened.Value)
                ServerTick(Time.deltaTime);
        }

        private void ServerTick(float deltaTime)
        {
            pryers.RemoveWhere(p => !CanPry(p));

            bool crowbar = false;
            foreach (var pryer in pryers)
                crowbar |= pryer.HasCrowbar.Value;

            if (PryerCount.Value != pryers.Count)
                PryerCount.Value = pryers.Count;
            if (WithCrowbar.Value != crowbar)
                WithCrowbar.Value = crowbar;

            // Один без лома не справится; с ломом — быстро, вдвоём руками — медленно.
            float time = crowbar ? crowbarTime : pryers.Count >= 2 ? teamTime : 0f;
            if (time <= 0f)
                return;

            Progress.Value = Mathf.Min(1f, Progress.Value + deltaTime / time);
            if (Time.time >= nextNoiseTime)
            {
                nextNoiseTime = Time.time + (crowbar ? 0.6f : 1.2f);
                NoiseEvents.Emit(hinge.position, crowbar ? crowbarNoiseRadius : teamNoiseRadius);
                PrySoundRpc(crowbar);
            }

            if (Progress.Value >= 1f)
                Open();
        }

        private void Open()
        {
            Opened.Value = true;
            pryers.Clear();
            PryerCount.Value = 0;
            Closed.Remove(this);

            // Аккумулятор под одним из капотов. Шанс — один к числу ещё закрытых (включая этот),
            // поэтому каждая машина равновероятна, а последняя закрытая наверняка его прячет.
            GameObject loot;
            if (!batteryFound && Random.value < 1f / (Closed.Count + 1))
            {
                batteryFound = true;
                loot = batteryPrefab;
            }
            else
            {
                float roll = Random.value;
                loot = roll < 0.25f ? filterPrefab : roll < 0.45f ? bandagePrefab : roll < 0.65f ? cellPrefab : null;
            }

            if (loot != null)
            {
                var instance = Instantiate(loot, transform.TransformPoint(lootPoint), transform.rotation);
                instance.GetComponent<NetworkObject>().Spawn(true);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PrySoundRpc(bool crowbar)
        {
            sound.pitch = Random.Range(0.8f, 1.1f);
            sound.PlayOneShot(crowbar ? ProceduralAudio.ImpactTin : ProceduralAudio.ImpactWood, crowbar ? 1f : 0.5f);
            if (crowbar)
                sound.PlayOneShot(ProceduralAudio.ImpactHeavy, 0.4f);
        }

        private void OnOpenedChanged(bool previous, bool current)
        {
            if (current)
            {
                sound.pitch = 1.3f;
                sound.PlayOneShot(ProceduralAudio.DoorClunk, 0.8f);
            }
        }

        private void ApplyAngle()
        {
            if (hinge != null)
                hinge.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
