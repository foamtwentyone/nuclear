using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Nuclear.Network
{
    /// <summary>
    /// Временное меню прототипа: создать игру (хост) или подключиться по IP.
    /// Позже заменится на лобби через Unity Relay/Lobby или Steam.
    /// </summary>
    public class ConnectionMenu : MonoBehaviour
    {
        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private ushort port = 7777;

        private void OnGUI()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null)
                return;

            GUILayout.BeginArea(new Rect(16, 16, 260, 200), GUI.skin.box);

            if (!manager.IsClient && !manager.IsServer)
            {
                GUILayout.Label("IP хоста");
                address = GUILayout.TextField(address);

                if (GUILayout.Button("Создать игру (хост)"))
                {
                    // Слушаем все интерфейсы, чтобы могли зайти игроки из локальной сети.
                    Transport(manager).SetConnectionData(address, port, "0.0.0.0");
                    manager.StartHost();
                }

                if (GUILayout.Button("Подключиться"))
                {
                    Transport(manager).SetConnectionData(address, port);
                    manager.StartClient();
                }
            }
            else
            {
                string role = manager.IsHost ? "Хост" : "Клиент";
                GUILayout.Label($"{role}, игроков: {manager.ConnectedClientsIds.Count}");

                if (GUILayout.Button("Выйти"))
                    manager.Shutdown();
            }

            GUILayout.EndArea();
        }

        private static UnityTransport Transport(NetworkManager manager) =>
            (UnityTransport)manager.NetworkConfig.NetworkTransport;
    }
}
