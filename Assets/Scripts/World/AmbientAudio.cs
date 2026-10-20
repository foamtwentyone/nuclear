using Nuclear.Audio;
using UnityEngine;

namespace Nuclear.World
{
    /// <summary>
    /// Фон мёртвого пригорода: петля ветра и редкие далёкие звуки — скрип металла, чей-то вой.
    /// Звучит у каждого игрока локально и ни на что в игре не влияет.
    /// </summary>
    public class AmbientAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float distantVolume = 0.3f;
        [SerializeField] private Vector2 distantInterval = new(15f, 35f);

        private AudioSource distant;
        private float nextDistantTime;

        private void Start()
        {
            var wind = SoundSources.Create2D(gameObject);
            wind.clip = ProceduralAudio.Wind;
            wind.loop = true;
            wind.volume = windVolume;
            wind.Play();

            distant = SoundSources.Create2D(gameObject);
            nextDistantTime = Time.time + Random.Range(distantInterval.x, distantInterval.y) * 0.5f;
        }

        private void Update()
        {
            if (Time.time < nextDistantTime)
                return;

            nextDistantTime = Time.time + Random.Range(distantInterval.x, distantInterval.y);
            // Без позиции в мире: просто сбоку, чтобы звук казался далёким и непонятно откуда.
            distant.panStereo = Random.Range(-0.8f, 0.8f);
            distant.pitch = Random.Range(0.85f, 1.1f);
            distant.PlayOneShot(Random.value < 0.6f ? ProceduralAudio.DistantCreak : ProceduralAudio.DistantHowl, distantVolume);
        }
    }
}
