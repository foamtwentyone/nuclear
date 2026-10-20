using System;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Nuclear.Items
{
    /// <summary>
    /// Сервер раскладывает предметы при старте игры.
    /// Из списка точек выбирается случайная часть, чтобы лут каждый раз лежал по-разному.
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public GameObject prefab;
            public Vector3[] positions;
            [Min(1)] public int count = 1;
            [Tooltip("Случайно повернуть по вертикали. Выключено для объектов, которые должны стоять ровно (дверь).")]
            public bool randomYaw = true;
            [Tooltip("Поворот по вертикали для каждой точки (если задан, случайный поворот не используется).")]
            public float[] yaws = Array.Empty<float>();
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public void SetEntries(Entry[] value) => entries = value;

        private void Start()
        {
            NetworkManager.Singleton.OnServerStarted += SpawnAll;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnServerStarted -= SpawnAll;
        }

        private void SpawnAll()
        {
            foreach (var entry in entries)
            {
                // Перемешиваем номера точек, а не сами точки, чтобы поворот остался при своей точке.
                var order = new int[entry.positions.Length];
                for (int i = 0; i < order.Length; i++)
                    order[i] = i;
                Shuffle(order);

                int count = Mathf.Min(entry.count, order.Length);
                for (int i = 0; i < count; i++)
                {
                    int point = order[i];
                    var rotation = point < entry.yaws.Length ? Quaternion.Euler(0f, entry.yaws[point], 0f)
                        : entry.randomYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;
                    var instance = Instantiate(entry.prefab, entry.positions[point], rotation);
                    instance.GetComponent<NetworkObject>().Spawn(true);
                }
            }
        }

        private static void Shuffle(int[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
