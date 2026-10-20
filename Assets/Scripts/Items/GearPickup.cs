using UnityEngine;

namespace Nuclear.Items
{
    /// <summary>Мелкий предмет, который кладётся в любую ячейку инвентаря (PlayerGear).</summary>
    public enum GearKind : byte
    {
        Bandage,
        Flashlight,
        Crowbar,
        FlashlightBattery,
        Filter,
        Can,
    }

    /// <summary>
    /// Помечает предмет как мелкий: E кладёт его в инвентарь, если есть свободная ячейка.
    /// Иначе его можно нести и бросать как обычный предмет.
    /// </summary>
    [RequireComponent(typeof(PhysicsItem))]
    public class GearPickup : MonoBehaviour
    {
        [SerializeField] private GearKind kind;

        /// <summary>Только сервер: заряд выброшенного фонаря в секундах (меньше нуля — полный).</summary>
        public float Charge { get; set; } = -1f;

        public GearKind Kind => kind;

        public static string NameOf(GearKind kind) => kind switch
        {
            GearKind.Bandage => "Бинт",
            GearKind.Flashlight => "Фонарь",
            GearKind.Crowbar => "Лом",
            GearKind.FlashlightBattery => "Батарейка",
            GearKind.Filter => "Фильтр",
            GearKind.Can => "Консерва",
            _ => kind.ToString(),
        };
    }
}
