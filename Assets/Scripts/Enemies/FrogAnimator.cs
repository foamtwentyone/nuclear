using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Анимация лягушки у каждого игрока локально: дышит горлом, моргает, перед прыжком приседает,
    /// в полёте вытягивается, при приземлении плюхается и пружинит.
    /// Веки в префабе стоят закрытыми; открытое веко развёрнуто вокруг центра глаза назад, внутрь головы,
    /// и при моргании опускается на глаз сверху.
    /// </summary>
    [RequireComponent(typeof(Frog))]
    public class FrogAnimator : MonoBehaviour
    {
        [Tooltip("На сколько градусов развёрнуто открытое веко (180 — целиком за глазом).")]
        [SerializeField] private float openLid = 180f;

        private Frog frog;
        private Transform body;
        private readonly Transform[] lids = new Transform[2];
        private readonly Quaternion[] lidClosed = new Quaternion[2];
        private Vector3 bodyScale;
        private float stretch, stretchVelocity;
        private float nextBlink, blink;
        private Frog.State lastState;

        private void Awake()
        {
            frog = GetComponent<Frog>();
            body = transform.Find("Body");
            if (body == null)
                return;
            bodyScale = body.localScale;
            for (int i = 0; i < lids.Length; i++)
            {
                lids[i] = body.Find(i == 0 ? "Lid L" : "Lid R");
                if (lids[i] != null)
                    lidClosed[i] = lids[i].localRotation;
            }
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

            if (Time.time >= nextBlink)
            {
                nextBlink = Time.time + Random.Range(2f, 6f);
                blink = 1f;
            }
            blink = Mathf.MoveTowards(blink, 0f, deltaTime * 6f);
            // 1 — открыто, 0 — закрыто; перед прыжком лягушка щурится.
            float open = state == Frog.State.Windup ? 0.55f : 1f - Mathf.Sin(blink * Mathf.PI);
            for (int i = 0; i < lids.Length; i++)
            {
                if (lids[i] != null)
                    lids[i].localRotation = lidClosed[i] * Quaternion.Euler(-openLid * open, 0f, 0f);
            }
        }
    }
}
