using UnityEngine;

namespace Nuclear.Audio
{
    /// <summary>
    /// Звуки-заглушки, собранные из шума прямо в коде, чтобы прототипу не нужны были аудиофайлы.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        private static AudioClip click;
        private static AudioClip cough;
        private static AudioClip growl;
        private static AudioClip swipe;
        private static AudioClip breath;

        /// <summary>Короткий щелчок счётчика Гейгера.</summary>
        public static AudioClip Click
        {
            get
            {
                // Сравнение через == учитывает уничтоженные Unity-объекты, ??= — нет.
                if (click == null)
                    click = Create("Geiger Click", 0.006f, (t, noise) => noise * Mathf.Exp(-t * 900f));
                return click;
            }
        }

        /// <summary>Двойной глухой «кхе-кхе».</summary>
        public static AudioClip Cough
        {
            get
            {
                if (cough == null)
                    cough = CreateFiltered("Cough", 0.55f, 0.12f, t => Burst(t, 0f, 0.18f) + Burst(t, 0.25f, 0.22f) * 0.8f);
                return cough;
            }
        }

        /// <summary>Низкое рычание, когда Слепыш бросается на звук.</summary>
        public static AudioClip Growl
        {
            get
            {
                if (growl == null)
                    growl = CreateFiltered("Growl", 1.2f, 0.025f, t => Mathf.Sin(Mathf.PI * t / 1.2f) * 1.6f);
                return growl;
            }
        }

        /// <summary>Свист взмаха лапы.</summary>
        public static AudioClip Swipe
        {
            get
            {
                if (swipe == null)
                    swipe = CreateFiltered("Swipe", 0.3f, 0.35f, t => Burst(t, 0f, 0.3f) * 0.6f);
                return swipe;
            }
        }

        /// <summary>Тяжёлое дыхание, зацикленное: по нему Слепыша можно услышать раньше, чем увидеть.</summary>
        public static AudioClip Breath
        {
            get
            {
                if (breath == null)
                    breath = CreateFiltered("Breath", 2.4f, 0.06f, t => Mathf.Pow(Mathf.Sin(Mathf.PI * t / 1.2f), 2f) * 0.5f);
                return breath;
            }
        }

        private static float Burst(float t, float start, float length)
        {
            float local = t - start;
            if (local < 0f || local > length)
                return 0f;
            // Резкая атака и плавный спад.
            return Mathf.Min(1f, local * 60f) * Mathf.Exp(-local * 12f);
        }

        private static AudioClip Create(string name, float seconds, System.Func<float, float, float> sample)
        {
            int length = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[length];
            for (int i = 0; i < length; i++)
                data[i] = sample(i / (float)SampleRate, Random.Range(-1f, 1f));

            var clip = AudioClip.Create(name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Шум через простой низкочастотный фильтр, умноженный на огибающую.</summary>
        private static AudioClip CreateFiltered(string name, float seconds, float smoothing, System.Func<float, float> envelope)
        {
            float filtered = 0f;
            return Create(name, seconds, (t, noise) =>
            {
                filtered += (noise - filtered) * smoothing;
                return Mathf.Clamp(filtered * 4f * envelope(t), -1f, 1f);
            });
        }
    }
}
