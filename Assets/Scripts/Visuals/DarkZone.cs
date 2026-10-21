using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nuclear.Visuals
{
    /// <summary>
    /// Тёмное место (тоннель): пока своя камера внутри, рассеянный свет неба и туман плавно гаснут,
    /// и без фонаря почти ничего не видно. Действует только на свой экран — у каждого игрока своё.
    /// Солнце внутрь не попадает само: его закрывает крыша.
    /// </summary>
    public class DarkZone : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new(6f, 4.5f, 50f);
        [Tooltip("Доля рассеянного света внутри (1 — как снаружи).")]
        [SerializeField, Range(0f, 1f)] private float ambient = 0.1f;

        private static readonly List<DarkZone> Zones = new();
        private static SphericalHarmonicsL2 baseProbe;
        private static Color baseFog;
        private static bool captured;
        private static float current = 1f;

        public void Configure(Vector3 zoneSize, float ambientShare)
        {
            size = zoneSize;
            ambient = ambientShare;
        }

        private void OnEnable() => Zones.Add(this);

        private void OnDisable()
        {
            Zones.Remove(this);
            if (Zones.Count == 0)
                Restore();
        }

        private bool Contains(Vector3 point)
        {
            var local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x / 2f && Mathf.Abs(local.y) <= size.y / 2f && Mathf.Abs(local.z) <= size.z / 2f;
        }

        private void LateUpdate()
        {
            // Освещением управляет одна зона за всех.
            if (Zones.Count == 0 || Zones[0] != this)
                return;

            var camera = ActiveCamera();
            float target = 1f;
            if (camera != null)
            {
                foreach (var zone in Zones)
                {
                    if (zone.Contains(camera.transform.position))
                        target = Mathf.Min(target, zone.ambient);
                }
            }

            if (Mathf.Approximately(current, target) && !captured)
                return;
            if (!captured)
            {
                baseProbe = RenderSettings.ambientProbe;
                baseFog = RenderSettings.fogColor;
                captured = true;
            }

            current = Mathf.MoveTowards(current, target, Time.deltaTime * 1.5f);
            RenderSettings.ambientProbe = baseProbe * current;
            RenderSettings.fogColor = baseFog * Mathf.Lerp(0.2f, 1f, current);
        }

        /// <summary>Включённая камера с наибольшей глубиной — камера своего игрока (камера меню ниже).</summary>
        private static Camera ActiveCamera()
        {
            Camera best = null;
            foreach (var camera in Camera.allCameras)
            {
                if (camera.enabled && (best == null || camera.depth > best.depth))
                    best = camera;
            }
            return best;
        }

        private static void Restore()
        {
            if (!captured)
                return;
            RenderSettings.ambientProbe = baseProbe;
            RenderSettings.fogColor = baseFog;
            captured = false;
            current = 1f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
