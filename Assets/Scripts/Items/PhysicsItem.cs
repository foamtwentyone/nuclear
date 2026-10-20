using System;
using System.Collections.Generic;
using Nuclear.Noise;
using Nuclear.Player;
using Unity.Netcode;
using UnityEngine;

namespace Nuclear.Items
{
    /// <summary>
    /// Предмет на физике, который можно взять, нести и бросить.
    /// Физику считает только сервер: он тянет предмет к рукам всех несущих,
    /// поэтому тяжёлое можно нести вдвоём. Клиенты видят результат через NetworkTransform.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsItem : NetworkBehaviour
    {
        [Header("Переноска")]
        [SerializeField] private string displayName = "Предмет";
        [SerializeField, Range(1, 2)] private int maxCarriers = 1;
        [SerializeField] private float holdDistance = 1.6f;
        [Tooltip("Насколько ниже взгляда держит предмет один игрок (тяжёлое несут у пояса).")]
        [SerializeField] private float soloHoldDrop;
        [Tooltip("Множитель скорости игрока, когда несёт один.")]
        [SerializeField, Range(0f, 1f)] private float soloSpeedMultiplier = 1f;
        [Tooltip("Множитель скорости каждого, когда несут вдвоём.")]
        [SerializeField, Range(0f, 1f)] private float sharedSpeedMultiplier = 1f;
        [SerializeField] private float followStrength = 12f;
        [SerializeField] private float maxFollowSpeed = 10f;
        [Tooltip("Если предмет застрял дальше этого расстояния от рук, его роняют.")]
        [SerializeField] private float breakDistance = 2.5f;
        [Tooltip("Если двое несущих разошлись дальше этого расстояния, предмет падает.")]
        [SerializeField] private float maxCarrierSpread = 3f;

        [Header("Бросок")]
        [SerializeField] private bool canThrow = true;
        [SerializeField] private float throwImpulse = 3f;

        [Header("Удары")]
        [SerializeField] private float impactNoiseMinSpeed = 1.5f;
        [SerializeField] private float impactNoiseRadius = 12f;

        public readonly NetworkVariable<int> CarrierCount = new(0);

        /// <summary>Сервер: предмет ударился, пока его никто не держал. Аргумент — скорость удара.</summary>
        public event Action<float> Impacted;

        public string DisplayName => displayName;
        public float HoldDistance => holdDistance;
        public bool IsFull => CarrierCount.Value >= maxCarriers;
        public bool CanThrow => canThrow && CarrierCount.Value <= 1;
        public float SpeedMultiplier => CarrierCount.Value > 1 ? sharedSpeedMultiplier : soloSpeedMultiplier;

        private readonly List<PlayerCarry> carriers = new();
        private Rigidbody body;
        private Collider[] colliders;
        private float nextImpactTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                ReleaseAll();
        }

        public Vector3 ClosestPoint(Vector3 point) => body.ClosestPointOnBounds(point);

        public void SetCollisionIgnored(Collider other, bool ignored)
        {
            foreach (var itemCollider in colliders)
                Physics.IgnoreCollision(itemCollider, other, ignored);
        }

        internal bool TryAddCarrier(PlayerCarry carrier)
        {
            if (carriers.Count >= maxCarriers || carriers.Contains(carrier))
                return false;

            carriers.Add(carrier);
            body.useGravity = false;
            CarrierCount.Value = carriers.Count;
            return true;
        }

        internal void RemoveCarrier(PlayerCarry carrier, bool throwItem)
        {
            if (!carriers.Remove(carrier))
                return;

            carrier.OnReleasedByItem();
            CarrierCount.Value = carriers.Count;

            if (carriers.Count > 0)
                return;

            body.useGravity = true;
            if (throwItem && canThrow)
                body.AddForce(carrier.Controller.LookDirection * throwImpulse, ForceMode.Impulse);
        }

        private void ReleaseAll()
        {
            for (int i = carriers.Count - 1; i >= 0; i--)
                RemoveCarrier(carriers[i], false);
        }

        private void FixedUpdate()
        {
            if (!IsServer || carriers.Count == 0)
                return;

            Vector3 target = Vector3.zero;
            foreach (var carrier in carriers)
                target += carrier.HoldPoint(this);
            target /= carriers.Count;

            if (carriers.Count == 1)
                target += Vector3.down * soloHoldDrop;
            else if (Vector3.Distance(carriers[0].HoldPoint(this), carriers[1].HoldPoint(this)) > maxCarrierSpread)
            {
                ReleaseAll();
                return;
            }

            Vector3 toTarget = target - body.position;
            if (toTarget.magnitude > breakDistance)
            {
                ReleaseAll();
                return;
            }

            Vector3 desired = Vector3.ClampMagnitude(toTarget * followStrength, maxFollowSpeed);
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, desired, 0.5f);
            body.angularVelocity *= 0.8f;

            // Предмет разворачивается вслед за первым несущим и не крутится в руках.
            var facing = Quaternion.Euler(0f, carriers[0].transform.eulerAngles.y, 0f);
            body.MoveRotation(Quaternion.Slerp(body.rotation, facing, 0.2f));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || carriers.Count > 0 || Time.time < nextImpactTime)
                return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < impactNoiseMinSpeed)
                return;

            // Подпрыгивания после удара не считаем отдельными ударами.
            nextImpactTime = Time.time + 0.25f;
            NoiseEvents.Emit(collision.GetContact(0).point, impactNoiseRadius);
            Impacted?.Invoke(speed);
        }
    }
}
