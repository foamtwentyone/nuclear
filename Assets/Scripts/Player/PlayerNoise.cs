using Nuclear.Noise;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Сервер: шаги игрока как шум для Слепыша. Ходьбу слышно рядом, бег — издалека.
    /// Скорость берётся из движения на сервере, клиенту ничего слать не нужно.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerNoise : NetworkBehaviour
    {
        [SerializeField] private float walkNoiseRadius = 2f;
        [SerializeField] private float sprintNoiseRadius = 10f;
        [SerializeField] private float walkStepInterval = 0.5f;
        [SerializeField] private float sprintStepInterval = 0.3f;
        [Tooltip("Медленнее этого (м/с) игрок считается стоящим и не шумит.")]
        [SerializeField] private float minMoveSpeed = 0.5f;

        private PlayerController controller;
        private PlayerSurvival survival;
        private Vector3 lastPosition;
        private float nextStepTime;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            survival = GetComponent<PlayerSurvival>();
        }

        public override void OnNetworkSpawn()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
                return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;

            bool moving = Time.deltaTime > 0f && delta.magnitude / Time.deltaTime >= minMoveSpeed;
            if (!moving || (survival != null && survival.IsDead) || Time.time < nextStepTime)
                return;

            bool sprinting = controller.IsSprinting.Value;
            nextStepTime = Time.time + (sprinting ? sprintStepInterval : walkStepInterval);
            NoiseEvents.Emit(transform.position, sprinting ? sprintNoiseRadius : walkNoiseRadius);
        }
    }
}
