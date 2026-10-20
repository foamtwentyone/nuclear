using UnityEngine;

namespace Nuclear.Voice
{
    /// <summary>
    /// Окно настройки микрофона: выбор устройства, индикатор громкости и пороги «речь» и «крик».
    /// Калибровка: 3 секунды молчать (порог речи ставится чуть выше фона), потом 3 секунды кричать
    /// (порог крика — чуть ниже пика).
    /// </summary>
    public class VoiceSettingsPanel
    {
        private const float CalibrationSeconds = 3f;
        private const float MeterMin = -70f;
        private const float MeterMax = 0f;

        private enum Calibration { None, Silence, Shout }

        private Calibration calibration;
        private float calibrationEnd;
        private float calibrationPeak;

        public bool Visible { get; set; }

        public void Draw(Rect area)
        {
            Tick();

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("<b>Микрофон</b> — Слепыш слышит ваш голос", RichLabel);

            bool enabled = GUILayout.Toggle(VoiceSettings.Enabled, " Слепыш слышит мой голос");
            if (enabled != VoiceSettings.Enabled)
            {
                VoiceSettings.Enabled = enabled;
                MicrophoneLevel.Instance.Restart();
            }

            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                GUILayout.Label("Микрофон не найден");
            }
            else if (GUILayout.Button($"Устройство: {MicrophoneLevel.DeviceName ?? devices[0]}"))
            {
                int current = System.Array.IndexOf(devices, MicrophoneLevel.DeviceName);
                VoiceSettings.Device = devices[(current + 1) % devices.Length];
                MicrophoneLevel.Instance.Restart();
            }

            DrawMeter();

            GUILayout.Label($"Порог речи (8 м): {VoiceSettings.SpeechThreshold:0} дБ");
            float speech = GUILayout.HorizontalSlider(VoiceSettings.SpeechThreshold, MeterMin, MeterMax - 10f);
            GUILayout.Label($"Порог крика (20 м): {VoiceSettings.ShoutThreshold:0} дБ");
            float shout = GUILayout.HorizontalSlider(VoiceSettings.ShoutThreshold, MeterMin + 10f, MeterMax);
            SetThresholds(speech, shout);

            if (calibration == Calibration.None)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Замерить тишину"))
                    StartCalibration(Calibration.Silence);
                if (GUILayout.Button("Замерить крик"))
                    StartCalibration(Calibration.Shout);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Сбросить"))
                    SetThresholds(VoiceSettings.DefaultSpeech, VoiceSettings.DefaultShout);
            }
            else
            {
                float left = Mathf.Max(0f, calibrationEnd - Time.unscaledTime);
                string what = calibration == Calibration.Silence ? "Молчите" : "Крикните во весь голос";
                GUILayout.Label($"<color=#e6b84d>{what}… {left:0.0} с</color>", RichLabel);
            }

            if (GUILayout.Button("Закрыть"))
            {
                Visible = false;
                PlayerPrefs.Save();
            }
            GUILayout.EndArea();
        }

        private void DrawMeter()
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            float level = MicrophoneLevel.LevelDb;
            var loudness = VoiceSettings.Classify(level);

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = loudness switch
            {
                Loudness.Shout => new Color(0.9f, 0.3f, 0.2f),
                Loudness.Speech => new Color(0.9f, 0.75f, 0.3f),
                _ => new Color(0.5f, 0.8f, 0.5f),
            };
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Share(level), rect.height), Texture2D.whiteTexture);

            GUI.color = Color.white;
            foreach (float mark in new[] { VoiceSettings.SpeechThreshold, VoiceSettings.ShoutThreshold })
                GUI.DrawTexture(new Rect(rect.x + rect.width * Share(mark) - 1f, rect.y, 2f, rect.height), Texture2D.whiteTexture);

            string label = loudness switch
            {
                Loudness.Shout => "крик — слышно за 20 м",
                Loudness.Speech => "речь — слышно за 8 м",
                _ => "тихо — не слышно",
            };
            GUILayout.Label(label);
        }

        private void StartCalibration(Calibration kind)
        {
            calibration = kind;
            calibrationEnd = Time.unscaledTime + CalibrationSeconds;
            calibrationPeak = MicrophoneLevel.Silence;
        }

        private void Tick()
        {
            if (calibration == Calibration.None)
                return;

            calibrationPeak = Mathf.Max(calibrationPeak, MicrophoneLevel.LevelDb);
            if (Time.unscaledTime < calibrationEnd)
                return;

            if (calibration == Calibration.Silence)
                SetThresholds(calibrationPeak + 8f, VoiceSettings.ShoutThreshold);
            else
                SetThresholds(VoiceSettings.SpeechThreshold, calibrationPeak - 6f);
            calibration = Calibration.None;
            PlayerPrefs.Save();
        }

        /// <summary>Крик всегда хотя бы на 6 дБ громче речи.</summary>
        private static void SetThresholds(float speech, float shout)
        {
            speech = Mathf.Clamp(speech, MeterMin, MeterMax - 10f);
            shout = Mathf.Clamp(Mathf.Max(shout, speech + 6f), MeterMin + 10f, MeterMax);
            VoiceSettings.SpeechThreshold = speech;
            VoiceSettings.ShoutThreshold = shout;
        }

        private static float Share(float db) => Mathf.InverseLerp(MeterMin, MeterMax, db);

        private static GUIStyle richLabel;
        private static GUIStyle RichLabel => richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };
    }
}
