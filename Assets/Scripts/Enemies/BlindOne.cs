using System.Collections.Generic;
using Nuclear.Audio;
using Nuclear.Noise;
using Nuclear.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Слепыш: слепой мутант, который ориентируется только на звук. Убить нельзя.
    /// Бродит; далёкий шум идёт проверять, близкий или громкий — бросается к источнику.
    /// Придя на место, бьёт всех в досягаемости и слушает. Замерший игрок для него не существует.
    /// Решения принимает только сервер, клиенты видят движение через NetworkTransform.
    /// </summary>
    public class BlindOne : NetworkBehaviour
    {
        public enum State : byte
        {
            Wander,
            Investigate,
            Rush,
            Listen,
        }

        [Header("Скорость")]
        [SerializeField] private float wanderSpeed = 1.5f;
        [SerializeField] private float investigateSpeed = 3f;
        [Tooltip("Быстрее бега игрока: от броска не убежать, можно только не шуметь.")]
        [SerializeField] private float rushSpeed = 7.5f;

        [Header("Слух")]
        [Tooltip("Доля радиуса шума (1 — у самого источника), с которой Слепыш бросается, а не идёт проверять.")]
        [SerializeField, Range(0f, 1f)] private float rushCloseness = 0.5f;
        [Tooltip("Ближе этого расстояния (м) любой услышанный шум вызывает бросок.")]
        [SerializeField] private float rushDistance = 5f;
        [Tooltip("Во сколько раз стены сокращают радиус слышимости.")]
        [SerializeField] private float wallMuffling = 0.5f;

        [Header("Поведение")]
        [SerializeField] private float wanderRadius = 12f;
        [SerializeField] private Vector2 wanderPause = new(1f, 4f);
        [SerializeField] private float listenTime = 4f;

        [Header("Атака")]
        [SerializeField] private float swipeRange = 1.8f;
        [SerializeField] private float swipeDamage = 45f;
        [SerializeField] private float swipeCooldown = 1.5f;
        [Tooltip("Во время броска бьёт любого, в кого врезался.")]
        [SerializeField] private float bumpRange = 1.1f;

        public readonly NetworkVariable<State> CurrentState = new();

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private NavMeshAgent agent;
        private Vector3 home;
        private float waitUntil;
        private float nextSwipeTime;
        private AudioSource voice;
        private AudioSource breathing;
        private Renderer head;
        private MaterialPropertyBlock headBlock;

        private Vector3 HearingPoint => transform.position + Vector3.up * 2f;

        private void Awake()
        {
            voice = CreateAudioSource(30f);
            breathing = CreateAudioSource(12f);
            breathing.clip = ProceduralAudio.Breath;
            breathing.loop = true;

            var headTransform = transform.Find("Head");
            if (headTransform != null)
                head = headTransform.GetComponent<Renderer>();
            headBlock = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            CurrentState.OnValueChanged += OnStateChanged;
            UpdateHeadColor();
            breathing.Play();

            if (!IsServer)
                return;

            home = transform.position;
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.3f;
            agent.acceleration = 20f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.6f;
            agent.autoBraking = true;

            NoiseEvents.Emitted += OnNoise;
            EnterWander();
        }

        public override void OnNetworkDespawn()
        {
            CurrentState.OnValueChanged -= OnStateChanged;
            NoiseEvents.Emitted -= OnNoise;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !agent.isOnNavMesh)
                return;

            switch (CurrentState.Value)
            {
                case State.Wander:
                    if ((!agent.hasPath || Arrived()) && Time.time >= waitUntil)
                        PickWanderPoint();
                    break;

                case State.Investigate:
                case State.Rush:
                    if (CurrentState.Value == State.Rush)
                        SwipeIfTouching();
                    if (Arrived())
                    {
                        Swipe();
                        Set(State.Listen);
                        waitUntil = Time.time + listenTime;
                    }
                    break;

                case State.Listen:
                    if (Time.time >= waitUntil)
                        EnterWander();
                    break;
            }
        }

        private void OnNoise(Vector3 position, float radius)
        {
            if (!agent.isOnNavMesh)
                return;

            if (IsBehindWall(position))
                radius *= wallMuffling;

            float distance = DistanceToBody(position);
            if (distance > radius)
                return;

            float closeness = 1f - distance / radius;
            bool rush = closeness >= rushCloseness || distance <= rushDistance;

            // Во время броска тихие звуки не отвлекают.
            if (!rush && CurrentState.Value == State.Rush)
                return;

            if (!NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas))
                return;

            agent.speed = rush ? rushSpeed : investigateSpeed;
            agent.SetDestination(hit.position);
            Set(rush ? State.Rush : State.Investigate);
        }

        /// <summary>Расстояние до ближайшей точки тела по высоте: шаги у ног слышны так же, как голос у головы.</summary>
        private float DistanceToBody(Vector3 point)
        {
            Vector3 feet = transform.position;
            float height = Mathf.Clamp(point.y, feet.y, feet.y + 2.3f);
            return Vector3.Distance(new Vector3(feet.x, height, feet.z), point);
        }

        private bool IsBehindWall(Vector3 noisePosition)
        {
            Vector3 from = noisePosition + Vector3.up * 0.2f;
            Vector3 to = HearingPoint;
            foreach (var hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // Стенами считаем неподвижную геометрию: не предметы, не игроков и не самого Слепыша.
                if (hit.rigidbody == null && hit.collider is not CharacterController && !hit.transform.IsChildOf(transform))
                    return true;
            }
            return false;
        }

        private void EnterWander()
        {
            agent.speed = wanderSpeed;
            agent.ResetPath();
            waitUntil = Time.time + Random.Range(wanderPause.x, wanderPause.y);
            Set(State.Wander);
        }

        private void PickWanderPoint()
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = home + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);

            waitUntil = Time.time + Random.Range(wanderPause.x, wanderPause.y);
        }

        private bool Arrived() =>
            !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;

        private void SwipeIfTouching()
        {
            foreach (var survival in AlivePlayers())
            {
                if (Vector3.Distance(survival.transform.position, transform.position) <= bumpRange)
                {
                    Swipe();
                    return;
                }
            }
        }

        private void Swipe()
        {
            if (Time.time < nextSwipeTime)
                return;

            nextSwipeTime = Time.time + swipeCooldown;
            SwipeSoundRpc();

            foreach (var survival in AlivePlayers())
            {
                if (Vector3.Distance(survival.transform.position, transform.position) <= swipeRange)
                    survival.TakeDamage(swipeDamage);
            }
        }

        private IEnumerable<PlayerSurvival> AlivePlayers()
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject != null
                    && client.PlayerObject.TryGetComponent(out PlayerSurvival survival)
                    && !survival.IsDead)
                    yield return survival;
            }
        }

        private void Set(State state)
        {
            if (CurrentState.Value != state)
                CurrentState.Value = state;
        }

        private void OnStateChanged(State previous, State current)
        {
            UpdateHeadColor();
            if (current == State.Rush)
                voice.PlayOneShot(ProceduralAudio.Growl);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SwipeSoundRpc()
        {
            voice.PlayOneShot(ProceduralAudio.Swipe);
        }

        /// <summary>Временная подсказка для тестов: цвет головы показывает, что Слепыш делает.</summary>
        private void UpdateHeadColor()
        {
            if (head == null)
                return;

            Color color = CurrentState.Value switch
            {
                State.Investigate => new Color(0.9f, 0.75f, 0.2f),
                State.Rush => new Color(0.9f, 0.15f, 0.1f),
                State.Listen => new Color(0.4f, 0.6f, 0.9f),
                _ => new Color(0.75f, 0.7f, 0.65f),
            };
            head.GetPropertyBlock(headBlock);
            headBlock.SetColor(ColorId, color);
            headBlock.SetColor(BaseColorId, color);
            head.SetPropertyBlock(headBlock);
        }

        private AudioSource CreateAudioSource(float maxDistance)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.playOnAwake = false;
            return source;
        }
    }
}
