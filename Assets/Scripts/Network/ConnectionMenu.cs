using Nuclear.UI;
using Nuclear.Voice;
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

        private static readonly Rect MenuRect = new(16, 16, 270, 200);
        private static readonly Rect VoiceRect = new(16, 224, 320, 350);
        private static ConnectionMenu instance;

        private readonly VoiceSettingsPanel voicePanel = new();

        /// <summary>Курсор над меню (координаты экрана, как у Input System: y снизу) — клик не захватывает курсор.</summary>
        public static bool IsPointerOver(Vector2 screenPosition)
        {
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return instance != null && (MenuRect.Contains(gui) || (instance.voicePanel.Visible && VoiceRect.Contains(gui)));
        }

        private void Awake() => instance = this;

        private void Start()
        {
            // Микрофон включается сразу: индикатор в настройках и шум голоса работают без задержки.
            _ = MicrophoneLevel.Instance;
        }

        private void OnGUI()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null)
                return;

            var previousSkin = GUI.skin;
            GUI.skin = HudSkin.Get();
            DrawMenu(manager);
            GUI.skin = previousSkin;
        }

        private void DrawMenu(NetworkManager manager)
        {
            GUILayout.BeginArea(MenuRect, GUI.skin.box);

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

            if (!voicePanel.Visible && GUILayout.Button("Микрофон…"))
                voicePanel.Visible = true;

            GUILayout.EndArea();

            if (voicePanel.Visible)
                voicePanel.Draw(VoiceRect);
        }

        private static UnityTransport Transport(NetworkManager manager) =>
            (UnityTransport)manager.NetworkConfig.NetworkTransport;
    }
}
