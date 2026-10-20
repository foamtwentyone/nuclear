using Nuclear.Audio;
using Nuclear.Hazards;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Дозиметр своего игрока: щелчки учащаются рядом с радиацией.
    /// Щелчки случайные, как у настоящего счётчика: частота зависит от фона.
    /// </summary>
    public class Dosimeter : MonoBehaviour
    {
        [Tooltip("Щелчков в секунду при нулевом фоне.")]
        [SerializeField] private float backgroundRate = 0.4f;
        [Tooltip("Дополнительных щелчков в секунду на единицу фона.")]
        [SerializeField] private float ratePerIntensity = 10f;
        [SerializeField] private float maxRate = 60f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

        /// <summary>Текущий фон в точке игрока, доза в секунду.</summary>
        public float Intensity { get; private set; }

        private PlayerController player;
        private AudioSource source;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.playOnAwake = false;
        }

        private void Update()
        {
            if (!player.IsSpawned || !player.IsOwner)
                return;

            Intensity = RadiationZone.IntensityAt(player.LookOrigin);

            // Ожидаемое число щелчков за кадр: целая часть щёлкает всегда, дробная — с такой вероятностью.
            float expected = Mathf.Min(maxRate, backgroundRate + Intensity * ratePerIntensity) * Time.deltaTime;
            int clicks = Mathf.FloorToInt(expected);
            if (Random.value < expected - clicks)
                clicks++;

            for (int i = 0; i < clicks; i++)
                source.PlayOneShot(ProceduralAudio.Click, volume * Random.Range(0.7f, 1f));
        }
    }
}
