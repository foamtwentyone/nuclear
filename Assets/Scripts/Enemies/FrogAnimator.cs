using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Анимация лягушки у каждого игрока локально: дышит горлом, моргает, перед прыжком приседает,
    /// в полёте вытягивается, при приземлении плюхается и пружинит.
    /// </summary>
    [RequireComponent(typeof(Frog))]
    public class FrogAnimator : MonoBehaviour
    {
        private Frog frog;
        private Transform body, eyes;
        private Vector3 bodyScale, eyesScale;
        private float stretch, stretchVelocity;
        private float nextBlink, blink;
        private Frog.State lastState;

        private void Awake()
        {
            frog = GetComponent<Frog>();
            body = transform.Find("Body");
            eyes = body != null ? body.Find("Eyes") : null;
            if (body != null)
                bodyScale = body.localScale;
            if (eyes != null)
                eyesScale = eyes.localScale;
        }

        private void Update()
        {
            if (body == null || !frog.IsSpawned)
                return;
            float deltaTime = Time.deltaTime;
            var state = frog.CurrentState.Value;
            if (state != lastState)
            {
                if (state == Frog.State.Windup)
                    stretchVelocity -= 2.5f;
                else if (state == Frog.State.Recover || (lastState == Frog.State.Jump && state == Frog.State.Sit))
                    stretchVelocity -= 4f;
                lastState = state;
            }

            float target = state switch
            {
                Frog.State.Windup => -0.3f,
                Frog.State.Jump => 0.25f * Mathf.Sin(frog.JumpProgress * Mathf.PI),
                _ => 0f,
            };
            float accel = (target - stretch) * 160f - stretchVelocity * 10f;
            stretchVelocity += accel * deltaTime;
            stretch = Mathf.Clamp(stretch + stretchVelocity * deltaTime, -0.4f, 0.4f);

            float breathe = Mathf.Sin(Time.time * 3.2f) * 0.03f;
            float height = 1f + stretch + breathe;
            float width = 1f / Mathf.Sqrt(Mathf.Max(0.5f, height));
            body.localScale = Vector3.Scale(bodyScale, new Vector3(width, height, width));

            if (eyes == null)
                return;
            if (Time.time >= nextBlink)
            {
                nextBlink = Time.time + Random.Range(2f, 6f);
                blink = 1f;
            }
            blink = Mathf.MoveTowards(blink, 0f, deltaTime * 6f);
            float lid = state == Frog.State.Windup ? 0.6f : 1f - Mathf.Sin(blink * Mathf.PI) * 0.9f;
            eyes.localScale = Vector3.Scale(eyesScale, new Vector3(1f, lid, 1f));
        }
    }
}
