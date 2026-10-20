using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Выносливость: тратится на бег и прыжки, восстанавливается после паузы.
    /// Если выдохся до нуля, бежать нельзя, пока запас не вернётся до порога.
    /// </summary>
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] private float max = 100f;
        [SerializeField] private float sprintDrainPerSecond = 20f;
        [SerializeField] private float regenPerSecond = 15f;
        [Tooltip("Сколько секунд после траты выносливость не восстанавливается.")]
        [SerializeField] private float regenDelay = 1f;
        [Tooltip("Доля запаса, до которой нужно восстановиться после полного истощения.")]
        [SerializeField, Range(0f, 1f)] private float recoverThreshold = 0.3f;

        public float Current { get; private set; }
        public float Max => max;
        public float Normalized => Current / max;
        public bool IsExhausted { get; private set; }

        private float regenBlockedUntil;

        private void Awake()
        {
            Current = max;
        }

        public bool CanSprint => !IsExhausted && Current > 0f;

        /// <summary>Списывает выносливость за кадр бега.</summary>
        public void DrainSprint(float deltaTime)
        {
            Spend(sprintDrainPerSecond * deltaTime);
        }

        /// <summary>Пытается потратить разовую стоимость (прыжок). Возвращает false, если не хватает.</summary>
        public bool TrySpend(float amount)
        {
            if (IsExhausted || Current < amount)
                return false;

            Spend(amount);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (Time.time < regenBlockedUntil)
                return;

            Current = Mathf.Min(max, Current + regenPerSecond * deltaTime);

            if (IsExhausted && Normalized >= recoverThreshold)
                IsExhausted = false;
        }

        private void Spend(float amount)
        {
            Current = Mathf.Max(0f, Current - amount);
            regenBlockedUntil = Time.time + regenDelay;

            if (Current <= 0f)
                IsExhausted = true;
        }
    }
}
