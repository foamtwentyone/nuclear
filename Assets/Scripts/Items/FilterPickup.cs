using UnityEngine;

namespace Nuclear.Items
{
    /// <summary>
    /// Помечает предмет как фильтр для противогаза: при подборе он уходит в карман,
    /// а если карман полон, его можно нести как обычный предмет.
    /// </summary>
    [RequireComponent(typeof(PhysicsItem))]
    public class FilterPickup : MonoBehaviour
    {
    }
}
