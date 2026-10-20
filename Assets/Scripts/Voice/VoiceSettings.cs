using UnityEngine;

namespace Nuclear.Voice
{
    /// <summary>Громкость голоса для Слепыша: тишина или шёпот, речь, крик.</summary>
    public enum Loudness : byte
    {
        Quiet,
        Speech,
        Shout,
    }

    /// <summary>
    /// Настройки микрофона этого игрока, хранятся в PlayerPrefs. Пороги в децибелах
    /// у каждого свои: микрофоны и манера говорить разные, поэтому их калибруют.
    /// </summary>
    public static class VoiceSettings
    {
        public const float DefaultSpeech = -38f;
        public const float DefaultShout = -18f;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt("voice.enabled", 1) == 1;
            set => PlayerPrefs.SetInt("voice.enabled", value ? 1 : 0);
        }

        public static string Device
        {
            get => PlayerPrefs.GetString("voice.device", "");
            set => PlayerPrefs.SetString("voice.device", value);
        }

        /// <summary>Тише этого — шёпот: Слепыш не слышит.</summary>
        public static float SpeechThreshold
        {
            get => PlayerPrefs.GetFloat("voice.speech", DefaultSpeech);
            set => PlayerPrefs.SetFloat("voice.speech", value);
        }

        /// <summary>Громче этого — крик.</summary>
        public static float ShoutThreshold
        {
            get => PlayerPrefs.GetFloat("voice.shout", DefaultShout);
            set => PlayerPrefs.SetFloat("voice.shout", value);
        }

        public static Loudness Classify(float levelDb)
        {
            if (levelDb >= ShoutThreshold)
                return Loudness.Shout;
            return levelDb >= SpeechThreshold ? Loudness.Speech : Loudness.Quiet;
        }
    }
}
