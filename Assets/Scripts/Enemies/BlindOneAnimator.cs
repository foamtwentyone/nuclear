using Nuclear.Audio;
using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Процедурная анимация Слепыша, у каждого игрока локально (только визуал и звук шагов).
    /// Скорость берётся из движения объекта, поэтому одинаково работает на сервере и у клиентов.
    /// Туша мультяшно сжимается и растягивается в такт шагам и дыханию, перед броском приседает,
    /// вытягивается вперёд и пружинит; нос водит из стороны в сторону, звезда носа пульсирует.
    /// </summary>
    [RequireComponent(typeof(BlindOne))]
    public class BlindOneAnimator : MonoBehaviour
    {
        [SerializeField] private float legSwing = 32f;
        [SerializeField] private float bodyBob = 0.06f;
        [SerializeField] private float bodyRoll = 3.5f;
        [Tooltip("Сжатие туши на каждом шаге (доля высоты).")]
        [SerializeField] private float stepSquash = 0.06f;
        [Tooltip("Скорость (м/с), при которой походка выходит на полный размах.")]
        [SerializeField] private float fullGaitSpeed = 3f;
        [SerializeField, Range(0f, 1f)] private float stepVolume = 0.55f;

        private BlindOne blindOne;
        private Transform body, nose;
        private readonly Transform[] legs = new Transform[4];
        private readonly Quaternion[] legRest = new Quaternion[4];
        private Vector3 bodyRest, bodyScale, noseScale;
        private Quaternion noseRest;
        private AudioSource feet;

        private Vector3 lastPosition;
        private float speed;
        private float phase;
        private float noseYaw, nosePitch;
        private BlindOne.State lastState;

        // Пружина «сжатия»: >0 — туша вытянута вверх и вперёд, <0 — приплюснута.
        private float stretch, stretchVelocity;
        private float lean;

        private void Awake()
        {
            blindOne = GetComponent<BlindOne>();
            body = transform.Find("Body");
            nose = body != null ? body.Find("Nose") : null;
            for (int i = 0; i < legs.Length; i++)
            {
                legs[i] = transform.Find($"Leg {i}");
                if (legs[i] != null)
                    legRest[i] = legs[i].localRotation;
            }
            if (body != null)
            {
                bodyRest = body.localPosition;
                bodyScale = body.localScale;
            }
            if (nose != null)
            {
                noseRest = nose.localRotation;
                noseScale = nose.localScale;
            }

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

            var state = blindOne.CurrentState.Value;
            if (state != lastState)
            {
                // Перед броском — резкий присед, после броска — тяжёлое оседание.
                if (state == BlindOne.State.Rush)
                    stretchVelocity -= 3.2f;
                else if (lastState == BlindOne.State.Rush)
                    stretchVelocity -= 1.6f;
                lastState = state;
            }

            // Полный цикл шага: две пары лап. Частота растёт со скоростью.
            float previousPhase = phase;
            phase += deltaTime * (1.2f + speed * 0.9f) * Mathf.PI * 2f;
            if (gait > 0.15f && Mathf.Floor(previousPhase / Mathf.PI) != Mathf.Floor(phase / Mathf.PI))
                PlayStep(gait);

            AnimateLegs(gait);
            AnimateBody(gait, state, deltaTime);
            AnimateNose(state, deltaTime);
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

        private void AnimateBody(float gait, BlindOne.State state, float deltaTime)
        {
            if (body == null)
                return;

            // Недодемпфированная пружина: после толчка туша несколько раз качается и успокаивается.
            float target = state == BlindOne.State.Rush ? 0.08f * gait : 0f;
            float accel = (target - stretch) * 110f - stretchVelocity * 9f;
            stretchVelocity += accel * deltaTime;
            stretch += stretchVelocity * deltaTime;
            stretch = Mathf.Clamp(stretch, -0.3f, 0.3f);

            float t = Time.time;
            float breathing = Mathf.Sin(t * 2.2f) * 0.025f;
            // Шаг приходится на |sin| = 0: туша в этот момент приплюснута, в середине шага — вытянута.
            float step = Mathf.Abs(Mathf.Sin(phase));
            float stepStretch = (step - 0.5f) * 2f * stepSquash * gait;
            float s = stretch + stepStretch + breathing;

            // Объём сохраняется: выше — значит уже и длиннее вперёд при броске.
            float height = 1f + s;
            float width = 1f / Mathf.Sqrt(Mathf.Max(0.5f, height));
            float length = width * (1f + Mathf.Max(0f, stretch) * 0.8f);
            body.localScale = Vector3.Scale(bodyScale, new Vector3(width, height, length));

            float bob = step * bodyBob * gait;
            body.localPosition = bodyRest + Vector3.up * bob;

            float leanTarget = state == BlindOne.State.Rush ? 6f * gait : 0f;
            lean = Mathf.Lerp(lean, leanTarget, 1f - Mathf.Exp(-5f * deltaTime));
            body.localRotation = Quaternion.Euler(lean - stretch * 25f, 0f, Mathf.Sin(phase) * bodyRoll * gait);
        }

        private void AnimateNose(BlindOne.State state, float deltaTime)
        {
            if (nose == null)
                return;
            float t = Time.time;
            float targetYaw, targetPitch, sniffRate;
            switch (state)
            {
                case BlindOne.State.Rush:
                    // Нос вперёд и вниз, звезда трепещет.
                    targetYaw = Mathf.Sin(t * 3f) * 5f;
                    targetPitch = 12f;
                    sniffRate = 16f;
                    break;
                case BlindOne.State.Listen:
                case BlindOne.State.Investigate:
                    // Водит носом, вынюхивая направление.
                    targetYaw = Mathf.Sin(t * 0.9f) * 30f + Mathf.Sin(t * 2.3f) * 9f;
                    targetPitch = Mathf.Sin(t * 1.7f) * 12f - 6f;
                    sniffRate = 8f;
                    break;
                default:
                    targetYaw = Mathf.Sin(t * 0.6f) * 14f;
                    targetPitch = 4f + Mathf.Sin(t * 0.8f) * 4f;
                    sniffRate = 3f;
                    break;
            }

            float follow = 1f - Mathf.Exp(-4f * deltaTime);
            noseYaw = Mathf.Lerp(noseYaw, targetYaw, follow);
            nosePitch = Mathf.Lerp(nosePitch, targetPitch, follow);

            float pulse = Mathf.Sin(t * sniffRate * Mathf.PI * 2f);
            nose.localRotation = noseRest * Quaternion.Euler(nosePitch, noseYaw, pulse * 6f);
            // Сжимается вдоль оси при каждом вдохе и раскрывается веером.
            nose.localScale = Vector3.Scale(noseScale,
                new Vector3(1f + 0.1f * pulse, 1f + 0.1f * pulse, 1f - 0.15f * pulse));
        }

        private void PlayStep(float gait)
        {
            feet.pitch = Random.Range(0.85f, 1f);
            feet.PlayOneShot(ProceduralAudio.HeavyStep, stepVolume * Mathf.Lerp(0.4f, 1f, gait));
        }
    }
}
