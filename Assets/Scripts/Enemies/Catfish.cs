using Nuclear.Audio;
using Nuclear.Noise;
using Nuclear.Player;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Подземный сом — толстая рыба, ползающая по земле. Почти слепой: ловит касанием. Длинные усы
    /// волочатся по полу далеко вперёд и в стороны; наступил на ус — сом разворачивается (медленно!)
    /// и несётся по прямой, пока не врежется во что-нибудь, шлёпаясь всем телом. Потом отходит
    /// и медленно уползает на своё место. Свет фонаря ему безразличен — усы в нём просто видно.
    /// </summary>
    public class Catfish : NetworkBehaviour
    {
        public enum State : byte
        {
            Rest,
            Turn,
            Charge,
            Dazed,
            Return,
        }

        public const int WhiskerCount = 4;
        public const int WhiskerPoints = 10;

        [Header("Усы")]
        [SerializeField] private float whiskerLength = 3.8f;
        [SerializeField] private float touchRadius = 0.32f;

        [Header("Бросок")]
        [SerializeField] private float turnSpeed = 70f;
        [SerializeField] private float turnTime = 0.8f;
        [SerializeField] private float chargeSpeed = 8f;
        [SerializeField] private float chargeDistance = 16f;
        [SerializeField] private float hitRadius = 1.1f;
        [SerializeField] private float hitDamage = 35f;
        [SerializeField] private float dazedTime = 2.5f;

        [Header("Возвращение")]
        [SerializeField] private float returnSpeed = 1.3f;
        [SerializeField] private float returnTurnSpeed = 45f;

        // Направления усов относительно морды: два длинных в стороны и два вперёд.
        private static readonly float[] WhiskerYaw = { -62f, -18f, 18f, 62f };
        private static readonly float[] WhiskerShare = { 0.85f, 1f, 1f, 0.85f };

        public readonly NetworkVariable<State> CurrentState = new();

        private Vector3 home;
        private Quaternion homeRotation;
        private PlayerSurvival target;
        private float stateUntil;
        private Vector3 chargeStart;
        private bool hitThisCharge;
        private float nextSlap;
        private AudioSource voice;

        private void Awake()
        {
            voice = SoundSources.Create3D(gameObject, maxDistance: 30f);
        }

        public override void OnNetworkSpawn()
        {
            CurrentState.OnValueChanged += OnStateChanged;
            // Точки появления заданы на глаз — прижимаемся к настоящей земле.
            if (IsServer)
            {
                var position = transform.position;
                position.y = Mobs.GroundY(position, transform, position.y);
                transform.position = position;
            }
            home = transform.position;
            homeRotation = transform.rotation;
        }

        public override void OnNetworkDespawn()
        {
            CurrentState.OnValueChanged -= OnStateChanged;
        }

        /// <summary>
        /// Точка уса (whisker 0..3, t 0..1 от морды к кончику) в мире. Одинаково считается у всех:
        /// сервер проверяет касания, клиенты рисуют. Усы лежат на полу и медленно колышутся.
        /// </summary>
        public Vector3 WhiskerPoint(int whisker, float t, Vector3 root)
        {
            float yaw = WhiskerYaw[whisker];
            float length = whiskerLength * WhiskerShare[whisker];
            // Чем дальше от морды, тем сильнее волна и тем ближе к полу.
            float wave = Mathf.Sin(Time.time * 1.3f + whisker * 1.7f + t * 4f) * 0.25f * t;
            var direction = Quaternion.Euler(0f, yaw + wave * 12f, 0f) * Vector3.forward;
            var side = Vector3.Cross(Vector3.up, direction);
            var local = direction * (t * length) + side * wave;
            var world = root + transform.rotation * local;
            world.y = Mathf.Lerp(root.y, transform.position.y + 0.03f, Mathf.Clamp01(t * 3f));
            return world;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
                return;

            switch (CurrentState.Value)
            {
                case State.Rest:
                case State.Return:
                    if (TouchedWhisker(out target))
                    {
                        Set(State.Turn);
                        stateUntil = Time.time + turnTime;
                        break;
                    }
                    if (CurrentState.Value == State.Return)
                        ReturnHome();
                    break;

                case State.Turn:
                    // Плохо поворачивает: за время разворота может не успеть навестись.
                    if (target != null)
                        Face(target.transform.position, turnSpeed);
                    if (Time.time >= stateUntil)
                    {
                        chargeStart = transform.position;
                        hitThisCharge = false;
                        Set(State.Charge);
                    }
                    break;

                case State.Charge:
                    Charge();
                    break;

                case State.Dazed:
                    if (Time.time >= stateUntil)
                        Set(State.Return);
                    break;
            }
        }

        private bool TouchedWhisker(out PlayerSurvival touched)
        {
            touched = null;
            var players = Mobs.AlivePlayers(NetworkManager);
            if (players.Count == 0)
                return false;

            for (int w = 0; w < WhiskerCount; w++)
            {
                var root = transform.TransformPoint(RootOf(w));
                var previous = WhiskerPoint(w, 0f, root);
                for (int i = 1; i < WhiskerPoints; i++)
                {
                    var point = WhiskerPoint(w, i / (float)(WhiskerPoints - 1), root);
                    foreach (var player in players)
                    {
                        var feet = player.transform.position;
                        if (feet.y - point.y < 0.6f && DistanceToSegment(feet, previous, point) < touchRadius)
                        {
                            touched = player;
                            return true;
                        }
                    }
                    previous = point;
                }
            }
            return false;
        }

        /// <summary>Основание уса у морды в локальных координатах (задаётся при сборке префаба).</summary>
        public Vector3 RootOf(int whisker) => whiskerRoots != null && whisker < whiskerRoots.Length ? whiskerRoots[whisker] : Vector3.forward;

        [SerializeField] private Vector3[] whiskerRoots;

        private void Charge()
        {
            float step = chargeSpeed * Time.deltaTime;
            var forward = transform.forward;
            if (Mobs.Blocked(transform.position + Vector3.up * 0.5f, forward, 0.55f, step + 1.4f, transform, out _)
                || Vector3.Distance(chargeStart, transform.position) > chargeDistance)
            {
                // Врезался или выдохся: шлёпается и лежит оглушённый.
                NoiseEvents.Emit(transform.position, 12f);
                SlapRpc(true);
                Set(State.Dazed);
                stateUntil = Time.time + dazedTime;
                return;
            }

            var next = transform.position + forward * step;
            next.y = Mobs.GroundY(next, transform, transform.position.y);
            transform.position = next;

            if (Time.time >= nextSlap)
            {
                nextSlap = Time.time + 0.22f;
                SlapRpc(false);
            }

            if (hitThisCharge)
                return;
            var nose = transform.position + forward * 1.2f;
            foreach (var player in Mobs.AlivePlayers(NetworkManager))
            {
                if (DistanceToSegment(player.transform.position, transform.position - forward * 1.2f, nose) <= hitRadius)
                {
                    player.TakeDamage(hitDamage);
                    hitThisCharge = true;
                    HitRpc();
                }
            }
        }

        private void ReturnHome()
        {
            var toHome = home - transform.position;
            toHome.y = 0f;
            if (toHome.magnitude > 0.3f)
            {
                Face(home, returnTurnSpeed);
                // Ползёт, только когда почти развернулся к дому.
                if (Vector3.Angle(transform.forward, toHome) < 25f)
                {
                    var next = transform.position + transform.forward * Mathf.Min(returnSpeed * Time.deltaTime, toHome.magnitude);
                    next.y = Mobs.GroundY(next, transform, transform.position.y);
                    transform.position = next;
                }
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, returnTurnSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, homeRotation) < 2f)
                Set(State.Rest);
        }

        private void Face(Vector3 point, float degreesPerSecond)
        {
            var direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Time.deltaTime);
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0f;
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-5f ? 0f : Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(point, a + ab * t);
        }

        private void Set(State state)
        {
            if (CurrentState.Value != state)
                CurrentState.Value = state;
        }

        private void OnStateChanged(State previous, State current)
        {
            if (current == State.Turn)
            {
                // Утробное бульканье — предупреждение.
                voice.pitch = 0.55f;
                voice.PlayOneShot(ProceduralAudio.Burp, 1f);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SlapRpc(bool wall)
        {
            voice.pitch = Random.Range(0.55f, 0.7f);
            voice.PlayOneShot(ProceduralAudio.HitFlesh, wall ? 1f : 0.55f);
            if (wall)
                voice.PlayOneShot(ProceduralAudio.ImpactHeavy, 1f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void HitRpc()
        {
            voice.pitch = 0.8f;
            voice.PlayOneShot(ProceduralAudio.Bite, 1f);
        }
    }
}
