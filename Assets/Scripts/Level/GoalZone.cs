using System.Collections.Generic;
using UnityEngine;

namespace Nuclear.Level
{
    /// <summary>Объём внутри бункера-цели: живой игрок здесь при открытой двери — победа.</summary>
    public class GoalZone : MonoBehaviour
    {
        private static readonly List<GoalZone> Active = new();

        [SerializeField] private Vector3 size = new(5f, 3f, 4f);

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
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
