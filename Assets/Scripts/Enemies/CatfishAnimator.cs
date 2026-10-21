using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>
    /// Анимация Сома у каждого игрока локально: усы лежат на полу и колышутся (по тем же точкам,
    /// по которым сервер ловит касания), тело дышит, при броске шлёпается — подпрыгивает и плющится,
    /// хвост мотается; оглушённый — лежит и подрагивает.
    /// </summary>
    [RequireComponent(typeof(Catfish))]
    public class CatfishAnimator : MonoBehaviour
    {
        private Catfish catfish;
        private Transform body;
        private Vector3 bodyRest, bodyScale;
        private readonly Transform[,] segments = new Transform[Catfish.WhiskerCount, Catfish.WhiskerPoints - 1];
        private float slapPhase;

        private void Awake()
        {
            catfish = GetComponent<Catfish>();
            body = transform.Find("Body");
            if (body != null)
            {
                bodyRest = body.localPosition;
                bodyScale = body.localScale;
            }
            var whiskers = transform.Find("Whiskers");
            for (int w = 0; w < Catfish.WhiskerCount; w++)
            {
                for (int i = 0; i < Catfish.WhiskerPoints - 1; i++)
                    segments[w, i] = whiskers != null ? whiskers.Find($"Whisker {w}/Segment {i}") : null;
            }
        }

        private void LateUpdate()
        {
            if (!catfish.IsSpawned)
                return;
            AnimateBody();
            AnimateWhiskers();
        }

        private void AnimateBody()
        {
            if (body == null)
                return;
            float t = Time.time;
            var state = catfish.CurrentState.Value;
            float bob = 0f, squash = Mathf.Sin(t * 1.5f) * 0.02f, yaw = Mathf.Sin(t * 0.8f) * 2f, roll = 0f;
            switch (state)
            {
                case Catfish.State.Charge:
                    // Шлепки всем телом: подскок, удар об пол — приплюснулся.
                    slapPhase += Time.deltaTime / 0.22f;
                    float hop = Mathf.Abs(Mathf.Sin(slapPhase * Mathf.PI));
                    bob = hop * 0.18f;
                    squash = (hop - 0.5f) * 0.2f;
                    yaw = Mathf.Sin(slapPhase * Mathf.PI) * 9f;
                    roll = Mathf.Sin(slapPhase * Mathf.PI * 0.5f) * 6f;
                    break;
                case Catfish.State.Turn:
                    squash = -0.1f + Mathf.Sin(t * 30f) * 0.02f;
                    yaw = Mathf.Sin(t * 14f) * 6f;
                    break;
                case Catfish.State.Dazed:
                    squash = -0.12f + Mathf.Sin(t * 22f) * 0.015f;
                    roll = Mathf.Sin(t * 5f) * 4f;
                    break;
                case Catfish.State.Return:
                    yaw = Mathf.Sin(t * 4f) * 7f;
                    break;
            }
            body.localPosition = bodyRest + Vector3.up * bob;
            float height = 1f + squash;
            body.localScale = Vector3.Scale(bodyScale, new Vector3(1f / Mathf.Sqrt(height), height, 1f));
            body.localRotation = Quaternion.Euler(0f, yaw, roll);
        }

        private void AnimateWhiskers()
        {
            for (int w = 0; w < Catfish.WhiskerCount; w++)
            {
                var root = transform.TransformPoint(catfish.RootOf(w));
                var previous = catfish.WhiskerPoint(w, 0f, root);
                for (int i = 0; i < Catfish.WhiskerPoints - 1; i++)
                {
                    var next = catfish.WhiskerPoint(w, (i + 1) / (float)(Catfish.WhiskerPoints - 1), root);
                    var segment = segments[w, i];
                    if (segment != null)
                    {
                        var delta = next - previous;
                        segment.position = previous;
                        if (delta.sqrMagnitude > 1e-6f)
                            segment.rotation = Quaternion.LookRotation(delta);
                        // Отрезок — цилиндр длиной 1 вдоль +Z; к кончику уса тоньше.
                        float thickness = Mathf.Lerp(1f, 0.45f, i / (float)(Catfish.WhiskerPoints - 1));
                        segment.localScale = new Vector3(thickness, thickness, delta.magnitude);
                    }
                    previous = next;
                }
            }
        }
    }
}
