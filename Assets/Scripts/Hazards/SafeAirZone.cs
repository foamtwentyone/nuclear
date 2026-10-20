using System.Collections.Generic;
using UnityEngine;

namespace Nuclear.Hazards
{
    /// <summary>Объём с чистым воздухом (бункер, шлюз): здесь фильтр не тратится.</summary>
    public class SafeAirZone : MonoBehaviour
    {
        private static readonly List<SafeAirZone> Active = new();

        [SerializeField] private Vector3 size = new(10f, 5f, 10f);

        public void Configure(Vector3 zoneSize) => size = zoneSize;

        public static bool Contains(Vector3 point)
        {
            foreach (var zone in Active)
            {
                Vector3 local = zone.transform.InverseTransformPoint(point);
                if (Mathf.Abs(local.x) <= zone.size.x / 2f
                    && Mathf.Abs(local.y) <= zone.size.y / 2f
                    && Mathf.Abs(local.z) <= zone.size.z / 2f)
                    return true;
            }
            return false;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
