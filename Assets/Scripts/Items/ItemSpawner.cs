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
                var positions = (Vector3[])entry.positions.Clone();
                Shuffle(positions);

                int count = Mathf.Min(entry.count, positions.Length);
                for (int i = 0; i < count; i++)
                {
                    var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    var instance = Instantiate(entry.prefab, positions[i], rotation);
                    instance.GetComponent<NetworkObject>().Spawn(true);
                }
            }
        }

        private static void Shuffle(Vector3[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
