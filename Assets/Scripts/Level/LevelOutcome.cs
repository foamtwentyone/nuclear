using Nuclear.Player;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Level
{
    /// <summary>
    /// Условия конца уровня. Победа: дверь открыта и хотя бы один живой игрок внутри бункера.
    /// Поражение: погибли все. Решает сервер.
    /// </summary>
    [RequireComponent(typeof(BunkerDoor))]
    public class LevelOutcome : NetworkBehaviour
    {
        public enum Result : byte
        {
            Playing,
            Won,
            Lost,
        }

        public static LevelOutcome Current { get; private set; }

        public readonly NetworkVariable<Result> Value = new();

        private BunkerDoor door;

        private void Awake()
        {
            door = GetComponent<BunkerDoor>();
        }

        public override void OnNetworkSpawn() => Current = this;

        public override void OnNetworkDespawn()
        {
            if (Current == this)
                Current = null;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || Value.Value != Result.Playing)
                return;

            int players = 0;
            int alive = 0;
            bool anyoneInside = false;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null || !client.PlayerObject.TryGetComponent(out PlayerSurvival survival))
                    continue;

                players++;
                if (survival.IsDead)
                    continue;

                alive++;
                if (GoalZone.Contains(survival.transform.position + Vector3.up))
                    anyoneInside = true;
            }

            if (players == 0)
                return;

            if (alive == 0)
                Value.Value = Result.Lost;
            else if (door.IsOpen && anyoneInside)
                Value.Value = Result.Won;
        }
    }
}
