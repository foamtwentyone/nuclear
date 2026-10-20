using System;
using UnityEngine;

namespace Nuclear.Noise
{
    /// <summary>
    /// Шина звуков для Слепыша. Вызывается только на сервере:
    /// точка, где прозвучал шум, и радиус, на котором его слышно.
    /// </summary>
    public static class NoiseEvents
    {
        public static event Action<Vector3, float> Emitted;

        public static void Emit(Vector3 position, float radius)
        {
            Emitted?.Invoke(position, radius);
        }
    }
}
