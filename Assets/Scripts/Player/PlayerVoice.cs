using Nuclear.Noise;
using Nuclear.Voice;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Голос как шум для Слепыша. Владелец измеряет громкость своего микрофона и, пока говорит,
    /// сообщает серверу только уровень (речь или крик). Шёпот не слышен. Мёртвых Слепыш не слышит.
    /// </summary>
    public class PlayerVoice : NetworkBehaviour
    {
        [SerializeField] private float speechNoiseRadius = 8f;
        [SerializeField] private float shoutNoiseRadius = 20f;
        [Tooltip("Как часто (с) повторять шум, пока игрок говорит.")]
        [SerializeField] private float noiseInterval = 0.4f;

        private PlayerSurvival survival;
        private float nextNoiseTime;
        private Loudness lastSent;

        /// <summary>Текущая громкость своего голоса — для подсказки в HUD.</summary>
        public Loudness Current { get; private set; }

        private void Awake()
        {
            survival = GetComponent<PlayerSurvival>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            bool dead = survival != null && survival.IsDead;
            Current = dead || !VoiceSettings.Enabled ? Loudness.Quiet : VoiceSettings.Classify(MicrophoneLevel.LevelDb);
            if (Current == Loudness.Quiet)
                return;

            // Переход на крик сообщаем сразу, не дожидаясь интервала.
            if (Time.time < nextNoiseTime && !(Current == Loudness.Shout && lastSent != Loudness.Shout))
                return;
            nextNoiseTime = Time.time + noiseInterval;
            lastSent = Current;
            VoiceNoiseRpc(Current);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void VoiceNoiseRpc(Loudness loudness)
        {
            if (survival != null && survival.IsDead)
                return;
            float radius = loudness == Loudness.Shout ? shoutNoiseRadius : speechNoiseRadius;
            if (loudness != Loudness.Quiet)
                NoiseEvents.Emit(transform.position + Vector3.up * 1.6f, radius);
        }
    }
}
