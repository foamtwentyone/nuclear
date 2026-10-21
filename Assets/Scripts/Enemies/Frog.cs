using Nuclear.Audio;
using Nuclear.Noise;
using Nuclear.Player;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Радиационная лягушка — ловушка, а не охотник. Сидит у пруда и квакает. Если игрок подходит ближе
    /// 4 м — приседает и прыгает в его сторону, но криво (±40°) и далеко (до 10 м). Приземление сбивает:
    /// урон и груз вылетает из рук. Потом лягушка короткими прыжками возвращается к пруду.
    /// </summary>
    public class Frog : NetworkBehaviour
    {
        public enum State : byte
        {
            Sit,
            Windup,
            Jump,
            Recover,
        }

        [SerializeField] private float triggerRadius = 4f;
        [SerializeField] private float windup = 0.4f;
        [SerializeField] private float maxJump = 10f;
        [SerializeField] private float spread = 40f;
        [SerializeField] private float jumpTime = 0.75f;
        [SerializeField] private float arcHeight = 2.2f;
        [SerializeField] private float landRadius = 1.4f;
        [SerializeField] private float landDamage = 15f;
        [SerializeField] private float landNoiseRadius = 8f;
        [SerializeField] private float recoverTime = 2.5f;

        public readonly NetworkVariable<State> CurrentState = new();

        private Vector3 home;
        private Quaternion homeRotation;
        private Vector3 jumpFrom, jumpTo;
        private float jumpHeight, jumpDuration;
        private float stateUntil, stateStart;
        private bool homing;
        private float nextCroak;
        private AudioSource voice;

        /// <summary>Доля прыжка 0..1 — для анимации у всех (считается по позиции, сервер её не шлёт).</summary>
        public float JumpProgress => CurrentState.Value == State.Jump ? Mathf.Clamp01((Time.time - stateStart) / Mathf.Max(0.01f, jumpDuration)) : 0f;

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
            nextCroak = Time.time + Random.Range(2f, 8f);
        }

        public override void OnNetworkDespawn()
        {
            CurrentState.OnValueChanged -= OnStateChanged;
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            // Кваканье слышат все сами по себе — это подсказка, где сидит лягушка.
            if (CurrentState.Value == State.Sit && Time.time >= nextCroak)
            {
                nextCroak = Time.time + Random.Range(5f, 11f);
                voice.pitch = Random.Range(0.9f, 1.1f);
                voice.PlayOneShot(ProceduralAudio.Croak, 0.8f);
            }

            if (!IsServer)
                return;

            switch (CurrentState.Value)
            {
                case State.Sit:
                    if (Time.time < stateUntil)
                        break;
                    if (FindVictim(out var victim))
                    {
                        Face(victim.transform.position, 1000f);
                        AimAt(victim.transform.position);
                        homing = false;
                        Set(State.Windup);
                        stateUntil = Time.time + windup;
                    }
                    else if (Vector3.Distance(Flat(transform.position), Flat(home)) > 2f)
                    {
                        // Домой — короткими прыжками.
                        var step = Vector3.ClampMagnitude(Flat(home) - Flat(transform.position), 1.8f);
                        BeginJump(transform.position + step, 0.8f, 0.45f);
                        homing = true;
                    }
                    else
                    {
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, 90f * Time.deltaTime);
                    }
                    break;

                case State.Windup:
                    if (Time.time >= stateUntil)
                        BeginJump(jumpTo, arcHeight, jumpTime);
                    break;

                case State.Jump:
                    float progress = JumpProgress;
                    var position = Vector3.Lerp(jumpFrom, jumpTo, progress);
                    position.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                    transform.position = position;
                    if (progress >= 1f)
                        Land();
                    break;

                case State.Recover:
                    if (Time.time >= stateUntil)
                    {
                        Set(State.Sit);
                        stateUntil = Time.time + 0.6f;
                    }
                    break;
            }
        }

        private bool FindVictim(out PlayerSurvival victim)
        {
            victim = null;
            float best = triggerRadius;
            foreach (var player in Mobs.AlivePlayers(NetworkManager))
            {
                float distance = Vector3.Distance(player.transform.position, transform.position);
                if (distance <= best)
                {
                    best = distance;
                    victim = player;
                }
            }
            return victim != null;
        }

        /// <summary>Цель прыжка: в сторону игрока, но с разбросом, и чуть дальше него.</summary>
        private void AimAt(Vector3 player)
        {
            var direction = Flat(player) - Flat(transform.position);
            float distance = Mathf.Clamp(direction.magnitude + 1.5f, 3f, maxJump);
            direction = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * (direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward);
            jumpTo = transform.position + direction * distance;
        }

        private void BeginJump(Vector3 target, float height, float duration)
        {
            jumpFrom = transform.position;
            // Не прыгаем сквозь стены: обрезаем прыжок о первую преграду.
            var flat = Flat(target) - Flat(jumpFrom);
            if (flat.sqrMagnitude > 0.01f && Mobs.Blocked(jumpFrom + Vector3.up * 0.5f, flat.normalized, 0.4f, flat.magnitude, transform, out var wall))
                target = jumpFrom + flat.normalized * Mathf.Max(0f, wall.distance - 0.3f);
            target.y = Mobs.GroundY(target, transform, jumpFrom.y);
            jumpTo = target;
            jumpHeight = height;
            jumpDuration = duration;
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(flat.normalized);
            stateStart = Time.time;
            Set(State.Jump);
            JumpRpc(duration);
        }

        private void Land()
        {
            transform.position = jumpTo;
            if (homing)
            {
                Set(State.Sit);
                stateUntil = Time.time + 0.7f;
                return;
            }

            NoiseEvents.Emit(transform.position, landNoiseRadius);
            bool hit = false;
            foreach (var player in Mobs.AlivePlayers(NetworkManager))
            {
                if (Vector3.Distance(player.transform.position, transform.position) > landRadius)
                    continue;
                player.TakeDamage(landDamage);
                if (player.TryGetComponent(out PlayerCarry carry) && carry.HeldItem != null)
                    carry.HeldItem.ForceDrop();
                hit = true;
            }
            LandRpc(hit);
            Set(State.Recover);
            stateUntil = Time.time + recoverTime;
        }

        private void Face(Vector3 point, float degreesPerSecond)
        {
            var direction = Flat(point) - Flat(transform.position);
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Time.deltaTime);
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private void Set(State state)
        {
            if (CurrentState.Value != state)
                CurrentState.Value = state;
        }

        private void OnStateChanged(State previous, State current)
        {
            if (current == State.Windup)
            {
                voice.pitch = 1.15f;
                voice.PlayOneShot(ProceduralAudio.Croak, 1f);
            }
        }

        /// <summary>Клиентам — когда начался прыжок и сколько длится (для растяжки в анимации).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void JumpRpc(float duration)
        {
            stateStart = Time.time;
            jumpDuration = duration;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void LandRpc(bool hit)
        {
            voice.pitch = Random.Range(0.85f, 1f);
            voice.PlayOneShot(ProceduralAudio.Land, 1f);
            if (hit)
                voice.PlayOneShot(ProceduralAudio.HitFlesh, 0.9f);
        }
    }
}
