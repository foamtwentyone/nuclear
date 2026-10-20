using Nuclear.Audio;
using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Процедурная анимация Слепыша, у каждого игрока локально (только визуал и звук шагов).
    /// Скорость берётся из движения объекта, поэтому одинаково работает на сервере и у клиентов.
    /// Лапы шагают диагональными парами, тело переваливается, голова водит носом, звезда носа пульсирует.
    /// </summary>
    [RequireComponent(typeof(BlindOne))]
    public class BlindOneAnimator : MonoBehaviour
    {
        [SerializeField] private float legSwing = 32f;
        [SerializeField] private float bodyBob = 0.06f;
        [SerializeField] private float bodyRoll = 3.5f;
        [Tooltip("Скорость (м/с), при которой походка выходит на полный размах.")]
        [SerializeField] private float fullGaitSpeed = 3f;
        [SerializeField, Range(0f, 1f)] private float stepVolume = 0.55f;

        private BlindOne blindOne;
        private Transform body, head, nose;
        private readonly Transform[] legs = new Transform[4];
        private readonly Quaternion[] legRest = new Quaternion[4];
        private Vector3 bodyRest, noseRest;
        private Quaternion headRest;
        private AudioSource feet;

        private Vector3 lastPosition;
        private float speed;
        private float phase;
        private float headYaw, headPitch;

        private void Awake()
        {
            blindOne = GetComponent<BlindOne>();
            body = transform.Find("Body");
            head = transform.Find("Head");
            nose = head != null ? head.Find("Nose") : null;
            for (int i = 0; i < legs.Length; i++)
            {
                legs[i] = transform.Find($"Leg {i}");
                if (legs[i] != null)
                    legRest[i] = legs[i].localRotation;
            }
            if (body != null)
                bodyRest = body.localPosition;
            if (head != null)
                headRest = head.localRotation;
            if (nose != null)
                noseRest = nose.localScale;

            feet = SoundSources.Create3D(gameObject, maxDistance: 30f);
            lastPosition = transform.position;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            speed = Mathf.Lerp(speed, delta.magnitude / deltaTime, 1f - Mathf.Exp(-10f * deltaTime));
            float gait = Mathf.Clamp01(speed / fullGaitSpeed);

            // Полный цикл шага: две пары лап. Частота растёт со скоростью.
            float previousPhase = phase;
            phase += deltaTime * (1.2f + speed * 0.9f) * Mathf.PI * 2f;
            if (gait > 0.15f && Mathf.Floor(previousPhase / Mathf.PI) != Mathf.Floor(phase / Mathf.PI))
                PlayStep(gait);

            AnimateLegs(gait);
            AnimateBody(gait);
            AnimateHead(deltaTime);
        }

        private void AnimateLegs(float gait)
        {
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null)
                    continue;
                // 0 и 3 (передняя левая и задняя правая) — одна пара, 1 и 2 — другая.
                float offset = i == 0 || i == 3 ? 0f : Mathf.PI;
                float angle = Mathf.Sin(phase + offset) * legSwing * gait;
                legs[i].localRotation = legRest[i] * Quaternion.Euler(angle, 0f, 0f);
            }
        }

        private void AnimateBody(float gait)
        {
            if (body == null)
                return;
            float breathing = Mathf.Sin(Time.time * 2.2f) * 0.012f;
            float bob = Mathf.Abs(Mathf.Sin(phase)) * bodyBob * gait;
            body.localPosition = bodyRest + Vector3.up * (bob + breathing);
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * bodyRoll * gait);
        }

        private void AnimateHead(float deltaTime)
        {
            var state = blindOne.CurrentState.Value;
            float t = Time.time;
            float targetYaw, targetPitch, sniffRate;
            switch (state)
            {
                case BlindOne.State.Rush:
                    // Голова вниз и вперёд, нос трепещет.
                    targetYaw = Mathf.Sin(t * 3f) * 4f;
                    targetPitch = 14f;
                    sniffRate = 16f;
                    break;
                case BlindOne.State.Listen:
                case BlindOne.State.Investigate:
                    // Водит головой, вынюхивая направление.
                    targetYaw = Mathf.Sin(t * 0.9f) * 28f + Mathf.Sin(t * 2.3f) * 8f;
                    targetPitch = Mathf.Sin(t * 1.7f) * 9f - 4f;
                    sniffRate = 8f;
                    break;
                default:
                    targetYaw = Mathf.Sin(t * 0.6f) * 12f;
                    targetPitch = 4f + Mathf.Sin(t * 0.8f) * 3f;
                    sniffRate = 3f;
                    break;
            }

            float follow = 1f - Mathf.Exp(-4f * deltaTime);
            headYaw = Mathf.Lerp(headYaw, targetYaw, follow);
            headPitch = Mathf.Lerp(headPitch, targetPitch, follow);
            if (head != null)
                head.localRotation = headRest * Quaternion.Euler(headPitch, headYaw, 0f);

            if (nose != null)
            {
                float pulse = Mathf.Sin(t * sniffRate * Mathf.PI * 2f);
                nose.localScale = noseRest * (1f + 0.12f * pulse);
                nose.localRotation = Quaternion.Euler(0f, 0f, pulse * 6f);
            }
        }

        private void PlayStep(float gait)
        {
            feet.pitch = Random.Range(0.85f, 1f);
            feet.PlayOneShot(ProceduralAudio.HeavyStep, stepVolume * Mathf.Lerp(0.4f, 1f, gait));
        }
    }
}
