using Nuclear.Audio;
using Nuclear.Items;
using Nuclear.Noise;
using Nuclear.Player;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// «Рот» — круглое существо из одной пасти, прикидывается камнем у лута. Ворует не игрока, а предмет:
    /// если рядом проходят с чем-то в руках (в инвентаре или в охапке) — раскрывается и «ХАП».
    /// Через минуту рыгает и выплёвывает добычу; удар ломом заставляет выплюнуть сразу.
    /// Чавканье громкое — его слышит Слепыш.
    /// </summary>
    public class Mouth : NetworkBehaviour
    {
        public enum State : byte
        {
            Lurk,
            Opening,
            Full,
            Spitting,
        }

        [SerializeField] private float reach = 2.5f;
        [SerializeField] private float windup = 0.35f;
        [SerializeField] private float digestTime = 60f;
        [SerializeField] private float cooldown = 6f;
        [SerializeField] private float chompNoiseRadius = 10f;

        public readonly NetworkVariable<State> CurrentState = new();

        private PhysicsItem swallowed;
        private PlayerSurvival target;
        private float stateUntil;
        private AudioSource voice;

        private Vector3 Center => transform.position + Vector3.up * 0.4f;
        private Vector3 HiddenPoint => transform.position + Vector3.down * 3f;

        private void Awake()
        {
            voice = SoundSources.Create3D(gameObject, maxDistance: 25f);
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
        }

        public override void OnNetworkDespawn()
        {
            CurrentState.OnValueChanged -= OnStateChanged;
            if (IsServer && swallowed != null)
                Spit();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
                return;

            switch (CurrentState.Value)
            {
                case State.Lurk:
                    if (Time.time >= stateUntil && FindVictim(out target))
                    {
                        Set(State.Opening);
                        stateUntil = Time.time + windup;
                    }
                    break;

                case State.Opening:
                    if (target != null)
                        Face(target.transform.position, 720f);
                    if (Time.time >= stateUntil)
                    {
                        if (target != null && !target.IsDead && Vector3.Distance(target.transform.position, transform.position) <= reach + 0.8f && Steal(target))
                        {
                            Set(State.Full);
                            stateUntil = Time.time + digestTime;
                        }
                        else
                        {
                            Set(State.Lurk);
                            stateUntil = Time.time + 1f;
                        }
                    }
                    break;

                case State.Full:
                    if (Time.time >= stateUntil || swallowed == null)
                        Spit();
                    break;

                case State.Spitting:
                    if (Time.time >= stateUntil)
                        Set(State.Lurk);
                    break;
            }
        }

        private bool FindVictim(out PlayerSurvival victim)
        {
            victim = null;
            float best = reach;
            foreach (var player in Mobs.AlivePlayers(NetworkManager))
            {
                float distance = Vector3.Distance(player.transform.position, transform.position);
                if (distance <= best && HoldsSomething(player))
                {
                    best = distance;
                    victim = player;
                }
            }
            return victim != null;
        }

        private static bool HoldsSomething(PlayerSurvival player) =>
            (player.TryGetComponent(out PlayerCarry carry) && carry.HeldItem != null)
            || (player.TryGetComponent(out PlayerGear gear) && gear.HoldsSomething);

        private bool Steal(PlayerSurvival victim)
        {
            PhysicsItem item = null;
            if (victim.TryGetComponent(out PlayerCarry carry) && carry.HeldItem != null)
                item = carry.HeldItem;
            else if (victim.TryGetComponent(out PlayerGear gear) && gear.HoldsSomething)
            {
                var taken = gear.TakeHeld(HiddenPoint);
                if (taken != null)
                    item = taken.GetComponent<PhysicsItem>();
            }
            if (item == null)
                return false;

            item.Swallow(HiddenPoint);
            swallowed = item;
            NoiseEvents.Emit(Center, chompNoiseRadius);
            return true;
        }

        /// <summary>Сервер: выплюнуть проглоченное (по таймеру, от удара ломом или если Рот исчез).</summary>
        public void Spit()
        {
            if (swallowed != null && swallowed.IsSpawned)
            {
                var forward = transform.forward;
                swallowed.SpitOut(Center + forward * 0.7f + Vector3.up * 0.4f, forward * 3.5f + Vector3.up * 3f);
                NoiseEvents.Emit(Center, chompNoiseRadius);
            }
            swallowed = null;
            if (IsSpawned && CurrentState.Value != State.Spitting)
            {
                Set(State.Spitting);
                stateUntil = Time.time + cooldown;
            }
        }

        /// <summary>Сервер: по Рту ударили ломом — если он полный, тут же выплёвывает.</summary>
        public void Bonk()
        {
            if (CurrentState.Value == State.Full)
                Spit();
            else
                BonkSoundRpc();
        }

        public Vector3 BodyCenter => Center;

        private void Face(Vector3 point, float degreesPerSecond)
        {
            var direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * Time.deltaTime);
        }

        private void Set(State state)
        {
            if (CurrentState.Value != state)
                CurrentState.Value = state;
        }

        private void OnStateChanged(State previous, State current)
        {
            voice.pitch = Random.Range(0.9f, 1.1f);
            switch (current)
            {
                case State.Full:
                    voice.PlayOneShot(ProceduralAudio.Bite);
                    voice.PlayOneShot(ProceduralAudio.Gulp, 0.9f);
                    break;
                case State.Spitting:
                    voice.PlayOneShot(ProceduralAudio.Burp);
                    break;
                case State.Lurk when previous == State.Opening:
                    // Промахнулся — щёлкнул зубами впустую.
                    voice.PlayOneShot(ProceduralAudio.Chatter, 0.7f);
                    break;
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BonkSoundRpc()
        {
            voice.PlayOneShot(ProceduralAudio.ImpactWood, 0.8f);
        }
    }
}
