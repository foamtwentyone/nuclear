using System.Collections.Generic;
using Nuclear.Player;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Enemies
{
    /// <summary>Общее для мобов: живые игроки, земля под точкой, проверка «стены впереди».</summary>
    public static class Mobs
    {
        private static readonly List<PlayerSurvival> Alive = new();

        /// <summary>Живые игроки (только на сервере: список подключённых знает он).</summary>
        public static List<PlayerSurvival> AlivePlayers(NetworkManager manager)
        {
            Alive.Clear();
            if (manager == null)
                return Alive;
            foreach (var client in manager.ConnectedClientsList)
            {
                if (client.PlayerObject != null && client.PlayerObject.TryGetComponent(out PlayerSurvival survival) && !survival.IsDead)
                    Alive.Add(survival);
            }
            return Alive;
        }

        /// <summary>Неподвижная геометрия (не предметы, не игроки и не сам моб).</summary>
        public static bool IsStatic(RaycastHit hit, Transform self) =>
            hit.rigidbody == null && hit.collider is not CharacterController && (self == null || !hit.transform.IsChildOf(self));

        /// <summary>Высота земли под точкой (по неподвижной геометрии); если не нашлась — fallback.</summary>
        public static float GroundY(Vector3 point, Transform self, float fallback)
        {
            var from = point + Vector3.up * 3f;
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsStatic(hit, self) && hit.point.y > best && hit.point.y <= point.y + 2.5f)
                    best = hit.point.y;
            }
            return best > float.MinValue ? best : fallback;
        }

        /// <summary>Есть ли неподвижная преграда на пути шаром radius на distance вперёд.</summary>
        public static bool Blocked(Vector3 from, Vector3 direction, float radius, float distance, Transform self, out RaycastHit wall)
        {
            foreach (var hit in Physics.SphereCastAll(from, radius, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsStatic(hit, self))
                {
                    wall = hit;
                    return true;
                }
            }
            wall = default;
            return false;
        }

        /// <summary>Свой (локальный) звук в точке: одноразовый источник, удаляется сам.</summary>
        public static void PlayAt(Vector3 point, AudioClip clip, float volume = 1f, float pitch = 1f, float maxDistance = 30f)
        {
            var holder = new GameObject(clip.name);
            holder.transform.position = point;
            var source = Audio.SoundSources.Create3D(holder, maxDistance);
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
            Object.Destroy(holder, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }
    }
}
