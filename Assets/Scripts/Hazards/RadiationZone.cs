using System.Collections.Generic;
using UnityEngine;

namespace Nuclear.Hazards
{
    /// <summary>
    /// Радиоактивное пятно. Сильнее всего фонит в центре, к краю радиуса спадает до нуля.
    /// Зоны есть в сцене у всех, поэтому дозиметр щёлкает без запросов к серверу.
    /// </summary>
    public class RadiationZone : MonoBehaviour
    {
        private static readonly List<RadiationZone> Active = new();

        [SerializeField] private float radius = 8f;
        [Tooltip("Доза в секунду в центре зоны.")]
        [SerializeField] private float intensity = 3f;

        public void Configure(float zoneRadius, float zoneIntensity)
        {
            radius = zoneRadius;
            intensity = zoneIntensity;
        }

        /// <summary>Суммарный фон всех зон в точке, доза в секунду.</summary>
        public static float IntensityAt(Vector3 point)
        {
            float total = 0f;
            foreach (var zone in Active)
            {
                float distance = Vector3.Distance(point, zone.transform.position);
                if (distance < zone.radius)
                    total += zone.intensity * (1f - distance / zone.radius);
            }
            return total;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.9f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
