using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Анимация Рта у каждого игрока локально: в засаде еле «дышит» (камень, который чуть шевелится),
    /// перед укусом распахивается с дрожью, после — жуёт и раздувается, при отрыжке широко открывается.
    /// </summary>
    [RequireComponent(typeof(Mouth))]
    public class MouthAnimator : MonoBehaviour
    {
        private Mouth mouth;
        private Transform jaw, body;
        private Vector3 bodyScale;
        private float angle, swell;
        private float stateTime;
        private Mouth.State lastState;

        private void Awake()
        {
            mouth = GetComponent<Mouth>();
            body = transform.Find("Body");
            jaw = body != null ? body.Find("Jaw") : null;
            if (body != null)
                bodyScale = body.localScale;
        }

        private void Update()
        {
            if (body == null || jaw == null || !mouth.IsSpawned)
                return;

            var state = mouth.CurrentState.Value;
            if (state != lastState)
            {
                lastState = state;
                stateTime = 0f;
            }
            stateTime += Time.deltaTime;
            float t = Time.time;

            float targetAngle, targetSwell;
            switch (state)
            {
                case Mouth.State.Opening:
                    targetAngle = 70f + Mathf.Sin(t * 60f) * 4f;
                    targetSwell = 0.05f;
                    break;
                case Mouth.State.Full:
                    // Первые секунды жуёт, потом сидит раздутый и довольный.
                    targetAngle = stateTime < 2f ? Mathf.Abs(Mathf.Sin(stateTime * 9f)) * 18f : Mathf.Max(0f, Mathf.Sin(t * 1.3f)) * 3f;
                    targetSwell = 0.18f;
                    break;
                case Mouth.State.Spitting:
                    targetAngle = stateTime < 0.6f ? 80f : 0f;
                    targetSwell = 0f;
                    break;
                default:
                    targetAngle = Mathf.Max(0f, Mathf.Sin(t * 0.7f)) * 2.5f;
                    targetSwell = 0f;
                    break;
            }

            float speed = state == Mouth.State.Opening || state == Mouth.State.Spitting ? 25f : 12f;
            angle = Mathf.Lerp(angle, targetAngle, 1f - Mathf.Exp(-speed * Time.deltaTime));
            swell = Mathf.Lerp(swell, targetSwell, 1f - Mathf.Exp(-4f * Time.deltaTime));
            jaw.localRotation = Quaternion.Euler(-angle, 0f, 0f);

            float breathe = 1f + Mathf.Sin(t * 1.6f) * 0.015f + swell;
            body.localScale = Vector3.Scale(bodyScale, new Vector3(breathe, 1f + (breathe - 1f) * 0.6f, breathe));
        }
    }
}
