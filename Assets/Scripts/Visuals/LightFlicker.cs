using UnityEngine;

namespace Nuclear.Visuals
{
    /// <summary>Мерцание огня: яркость и чуть-чуть позиция света плавно «дышат» по шуму Перлина.</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float depth = 0.25f;
        [SerializeField] private float speed = 7f;

        private Light glow;
        private float baseIntensity;
        private Vector3 basePosition;
        private float seed;

        private void Awake()
        {
            glow = GetComponent<Light>();
            baseIntensity = glow.intensity;
            basePosition = transform.localPosition;
            seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float t = Time.time * speed + seed;
            float noise = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 2.3f, seed + 7f) * 0.3f;
            glow.intensity = baseIntensity * (1f - depth + depth * 2f * noise);
            transform.localPosition = basePosition + new Vector3(Mathf.PerlinNoise(seed, t) - 0.5f, 0f, Mathf.PerlinNoise(seed + 3f, t) - 0.5f) * 0.08f;
        }
    }
}
