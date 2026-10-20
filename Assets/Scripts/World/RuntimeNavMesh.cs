using Unity.AI.Navigation;
using UnityEngine;

namespace Nuclear.World
{
    /// <summary>
    /// Строит навигационную сетку при загрузке сцены. Уровни будут генерироваться,
    /// поэтому заранее запечённой сетки нет. Предметы и игроки появляются позже и в неё не попадают.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMesh : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<NavMeshSurface>().BuildNavMesh();
        }
    }
}
