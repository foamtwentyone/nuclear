using UnityEngine;

namespace Nuclear.Audio
{
    /// <summary>
    /// Единые настройки источников звука. 3D-звук затухает как в жизни (быстро вблизи, медленно вдали)
    /// и стихает до нуля к maxDistance — так радиус слышимости совпадает с игровым.
    /// </summary>
    public static class SoundSources
    {
        private static AnimationCurve rolloff;

        public static AudioSource Create3D(GameObject owner, float maxDistance)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            return source;
        }

        public static AudioSource Create2D(GameObject owner)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Кривая 1/(1 + 8x), сдвинутая так, чтобы на краю (x = 1) была ровно тишина.</summary>
        private static AnimationCurve Rolloff
        {
            get
            {
                if (rolloff != null)
                    return rolloff;

                var keys = new Keyframe[9];
                for (int i = 0; i < keys.Length; i++)
                {
                    float x = i / (float)(keys.Length - 1);
                    float value = (1f / (1f + 8f * x) - 1f / 9f) / (1f - 1f / 9f);
                    keys[i] = new Keyframe(x, value);
                }
                rolloff = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                    rolloff.SmoothTangents(i, 0f);
                return rolloff;
            }
        }
    }
}
