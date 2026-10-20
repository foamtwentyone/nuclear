using Nuclear.Level;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nuclear.Player
{
    /// <summary>
    /// Вскрытие капотов: свой игрок держит E, глядя на капот с пустыми руками.
    /// Серверу сообщается только начало и конец; скорость и шум считает CarHood.
    /// </summary>
    [RequireComponent(typeof(PlayerGear))]
    public class PlayerPry : NetworkBehaviour
    {
        [SerializeField] private float reach = 2.5f;

        /// <summary>Капот, на который смотрит свой игрок (для подсказки).</summary>
        public CarHood LookHood { get; private set; }

        private PlayerController controller;
        private PlayerCarry carry;
        private PlayerSurvival survival;
        private PlayerGear gear;
        private CarHood prying;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            carry = GetComponent<PlayerCarry>();
            survival = GetComponent<PlayerSurvival>();
            gear = GetComponent<PlayerGear>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            bool able = !survival.IsDead && carry.HeldItem == null;
            LookHood = able ? FindHoodInView() : null;
            if (LookHood != null && LookHood.Opened.Value)
                LookHood = null;

            var keyboard = Keyboard.current;
            bool holding = keyboard != null && keyboard.eKey.isPressed && Cursor.lockState == CursorLockMode.Locked;
            var wanted = holding ? LookHood : null;
            if (wanted == prying)
                return;

            if (prying != null)
                PryRpc(prying.NetworkObject, false);
            prying = wanted;
            if (prying != null)
                PryRpc(prying.NetworkObject, true);
        }

        private CarHood FindHoodInView()
        {
            // Капот — триггер, поэтому лучом с учётом триггеров; предметы в руках не мешают (руки пусты).
            var hits = Physics.RaycastAll(controller.LookOrigin, controller.LookDirection, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            CarHood best = null;
            float bestDistance = float.MaxValue;
            foreach (var hit in hits)
            {
                var hood = hit.collider.GetComponentInParent<CarHood>();
                if (hood != null && hit.distance < bestDistance)
                {
                    best = hood;
                    bestDistance = hit.distance;
                }
            }
            return best;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void PryRpc(NetworkObjectReference hoodReference, bool start)
        {
            if (hoodReference.TryGet(out var hoodObject) && hoodObject.TryGetComponent(out CarHood hood))
                hood.SetPrying(gear, start);
        }

        public override void OnNetworkDespawn()
        {
            prying = null;
        }
    }
}
