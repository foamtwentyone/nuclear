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
        private float nextClickTime;
        private Renderer head;
        private MaterialPropertyBlock headBlock;

        private Vector3 HearingPoint => transform.position + Vector3.up * 1.2f;

        /// <summary>Откуда кусает: морда спереди длинного тела, а не его центр.</summary>
        private Vector3 MouthPoint => transform.position + transform.forward * 1.3f;

        private void Awake()
        {
            voice = SoundSources.Create3D(gameObject, maxDistance: 30f);
            breathing = SoundSources.Create3D(gameObject, maxDistance: 14f);
            breathing.clip = ProceduralAudio.BlindPurr;
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
            // Разная фаза дыхания, чтобы несколько Слепышей не дышали хором.
            breathing.time = Random.Range(0f, breathing.clip.length);
            breathing.Play();

            if (!IsServer)
                return;

            home = transform.position;
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;
            agent.height = 1.6f;
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
            if (!IsSpawned)
                return;

            UpdateEchoClicks();

            if (!IsServer || !agent.isOnNavMesh)
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
            float height = Mathf.Clamp(point.y, feet.y, feet.y + 1.6f);
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
                if (Vector3.Distance(survival.transform.position, MouthPoint) <= bumpRange)
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

            bool hit = false;
            foreach (var survival in AlivePlayers())
            {
                if (Vector3.Distance(survival.transform.position, MouthPoint) <= swipeRange)
                {
                    survival.TakeDamage(swipeDamage);
                    hit = true;
                }
            }
            SwipeSoundRpc(hit);
        }

        /// <summary>
        /// Пока слушает или проверяет шум, Слепыш принюхивается звёздчатым носом сериями шмыганий.
        /// Играется у всех локально: по этому звуку его можно засечь в тумане.
        /// </summary>
        private void UpdateEchoClicks()
        {
            var state = CurrentState.Value;
            if (state != State.Listen && state != State.Investigate)
                return;

            if (Time.time < nextClickTime)
                return;

            voice.pitch = Random.Range(0.9f, 1.1f);
            voice.PlayOneShot(ProceduralAudio.Sniff, 0.8f);
            nextClickTime = Time.time + Random.Range(1.4f, 2.6f);
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
            {
                voice.pitch = Random.Range(0.95f, 1.05f);
                voice.PlayOneShot(ProceduralAudio.Scream);
            }
            else if (current == State.Listen && previous == State.Rush)
            {
                // Добежал и никого не нашёл — злобно стучит зубами.
                voice.pitch = Random.Range(0.95f, 1.05f);
                voice.PlayOneShot(ProceduralAudio.Chatter, 0.9f);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SwipeSoundRpc(bool hit)
        {
            voice.pitch = 1f;
            voice.PlayOneShot(ProceduralAudio.Bite);
            if (hit)
                voice.PlayOneShot(ProceduralAudio.HitFlesh);
        }

        /// <summary>
        /// Временная подсказка для тестов: оттенок головы показывает, что Слепыш делает.
        /// Цвет умножается на палитру модели, поэтому «бродит» — без оттенка.
        /// </summary>
        private void UpdateHeadColor()
        {
            if (head == null)
                return;

            Color color = CurrentState.Value switch
            {
                State.Investigate => new Color(1f, 0.9f, 0.55f),
                State.Rush => new Color(1f, 0.5f, 0.45f),
                State.Listen => new Color(0.7f, 0.82f, 1f),
                _ => Color.white,
            };
            head.GetPropertyBlock(headBlock);
            headBlock.SetColor(ColorId, color);
            headBlock.SetColor(BaseColorId, color);
            head.SetPropertyBlock(headBlock);
        }
    }
}
