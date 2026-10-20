using UnityEngine;

namespace Nuclear.Items
{
    /// <summary>Снаряжение, которое уходит в карман (PlayerGear), а не в руки.</summary>
    public enum GearKind : byte
    {
        Bandage,
        Flashlight,
        Crowbar,
        FlashlightBattery,
    }

    /// <summary>
    /// Помечает предмет как снаряжение: E кладёт его в карман, если там есть место.
    /// Иначе его можно нести и бросать как обычный предмет.
    /// </summary>
    [RequireComponent(typeof(PhysicsItem))]
    public class GearPickup : MonoBehaviour
    {
        [SerializeField] private GearKind kind;

        /// <summary>Только сервер: заряд выброшенного фонаря в секундах (меньше нуля — полный).</summary>
        public float Charge { get; set; } = -1f;

        public GearKind Kind => kind;
    }
}
