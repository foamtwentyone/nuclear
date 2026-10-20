using UnityEngine;

namespace Nuclear.Voice
{
    /// <summary>
    /// Громкость микрофона этого компьютера в децибелах (0 — максимум, −80 — тишина).
    /// Сам голос никуда не передаётся: громкость нужна только для шума, который слышит Слепыш.
    /// Один на всю игру, создаётся при первом обращении.
    /// </summary>
    public class MicrophoneLevel : MonoBehaviour
    {
        public const float Silence = -80f;
        private const int SampleRate = 16000;
        private const float WindowSeconds = 0.05f;

        private static MicrophoneLevel instance;

        private AudioClip clip;
        private string device;
        private readonly float[] window = new float[Mathf.CeilToInt(SampleRate * WindowSeconds)];
        private float smoothed = Silence;

        /// <summary>Сглаженная громкость: быстро растёт, медленно спадает, чтобы короткие паузы между словами не обрывали шум.</summary>
        public static float LevelDb => Instance.smoothed;
        public static bool IsRecording => Instance.clip != null;
        public static string DeviceName => Instance.device;

        public static MicrophoneLevel Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Microphone Level");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<MicrophoneLevel>();
                }
                return instance;
            }
        }

        private void Start()
        {
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                Application.RequestUserAuthorization(UserAuthorization.Microphone);
            Restart();
        }

        /// <summary>Запускает запись с выбранного в настройках микрофона (или выключает её).</summary>
        public void Restart()
        {
            if (clip != null)
            {
                Microphone.End(device);
                Destroy(clip);
                clip = null;
            }
            smoothed = Silence;

            if (!VoiceSettings.Enabled || Microphone.devices.Length == 0)
                return;

            device = VoiceSettings.Device;
            if (System.Array.IndexOf(Microphone.devices, device) < 0)
                device = Microphone.devices[0];
            clip = Microphone.Start(device, true, 1, SampleRate);
        }

        private void Update()
        {
            float current = Measure();
            float rate = current > smoothed ? 30f : 4f;
            smoothed = Mathf.Lerp(smoothed, current, 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime));
        }

        private float Measure()
        {
            if (clip == null || !Microphone.IsRecording(device))
                return Silence;

            int position = Microphone.GetPosition(device);
            int length = clip.samples;
            int start = position - window.Length;
            if (start < 0)
                start += length;
            // Буфер кольцевой: окно у его конца читается двумя кусками.
            int firstPart = Mathf.Min(window.Length, length - start);
            float sum = 0f;
            sum += SumSquares(start, firstPart);
            if (firstPart < window.Length)
                sum += SumSquares(0, window.Length - firstPart);

            float rms = Mathf.Sqrt(sum / window.Length);
            return Mathf.Max(Silence, 20f * Mathf.Log10(rms + 1e-6f));
        }

        private float SumSquares(int offset, int count)
        {
            var part = count == window.Length ? window : new float[count];
            clip.GetData(part, offset);
            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += part[i] * part[i];
            return sum;
        }

        private void OnDestroy()
        {
            if (clip != null)
                Microphone.End(device);
        }
    }
}
