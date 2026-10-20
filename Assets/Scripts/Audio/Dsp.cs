using System;
using UnityEngine;

namespace Nuclear.Audio
{
    /// <summary>
    /// Минимальный набор для синтеза звуков в коде: шум, фильтры, огибающие, сборка клипа.
    /// Звуки — заглушки до настоящих записей, но со звуковым дизайном, а не голым шумом.
    /// </summary>
    public static class Dsp
    {
        public const int SampleRate = 44100;

        public static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

        public static float TimeAt(int index) => index / (float)SampleRate;

        public static float White(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        /// <summary>Пила от -1 до 1; фаза в периодах.</summary>
        public static float Saw(double phase) => (float)(2.0 * (phase - Math.Floor(phase + 0.5)));

        public static float Sine(double phase) => (float)Math.Sin(phase * 2.0 * Math.PI);

        /// <summary>Быстрая атака и экспоненциальный спад; ноль до начала.</summary>
        public static float Hit(float t, float start, float attack, float decay)
        {
            float local = t - start;
            if (local < 0f)
                return 0f;
            if (local < attack)
                return local / attack;
            return Mathf.Exp(-(local - attack) / decay);
        }

        /// <summary>Плавное окно sin² от start длиной length; ноль снаружи.</summary>
        public static float Window(float t, float start, float length)
        {
            float x = (t - start) / length;
            if (x <= 0f || x >= 1f)
                return 0f;
            float s = Mathf.Sin(Mathf.PI * x);
            return s * s;
        }

        /// <summary>Мягкое ограничение, добавляет «грязи» громким звукам.</summary>
        public static float SoftClip(float x) => (float)Math.Tanh(x);

        /// <summary>Несколько затухающих отражений: дешёвое ощущение пространства.</summary>
        public static void Echo(float[] data, float[] delays, float[] gains)
        {
            var dry = (float[])data.Clone();
            for (int e = 0; e < delays.Length; e++)
            {
                int offset = Mathf.RoundToInt(delays[e] * SampleRate);
                for (int i = offset; i < data.Length; i++)
                    data[i] += dry[i - offset] * gains[e];
            }
        }

        /// <summary>
        /// Делает петлю бесшовной: хвост длиной fadeSeconds плавно подмешивается в начало,
        /// а сам отрезается. Буфер должен быть длиннее нужной петли на fadeSeconds.
        /// </summary>
        public static float[] LoopCrossfade(float[] data, float fadeSeconds)
        {
            int fade = Mathf.RoundToInt(fadeSeconds * SampleRate);
            var loop = new float[data.Length - fade];
            Array.Copy(data, loop, loop.Length);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                loop[i] = loop[i] * k + data[loop.Length + i] * (1f - k);
            }
            return loop;
        }

        public static AudioClip ToClip(string name, float[] data, float peak = 0.9f)
        {
            float max = 0f;
            foreach (float sample in data)
                max = Mathf.Max(max, Mathf.Abs(sample));
            if (max > 0f)
            {
                float gain = peak / max;
                for (int i = 0; i < data.Length; i++)
                    data[i] *= gain;
            }

            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Биквадратный фильтр по формулам RBJ. Частоту можно менять на ходу для свипов.</summary>
        public sealed class Biquad
        {
            private enum Kind { LowPass, HighPass, BandPass }

            private readonly Kind kind;
            private float b0, b1, b2, a1, a2;
            private float x1, x2, y1, y2;

            private Biquad(Kind kind, float frequency, float q)
            {
                this.kind = kind;
                Set(frequency, q);
            }

            public static Biquad LowPass(float frequency, float q = 0.707f) => new Biquad(Kind.LowPass, frequency, q);
            public static Biquad HighPass(float frequency, float q = 0.707f) => new Biquad(Kind.HighPass, frequency, q);
            public static Biquad BandPass(float frequency, float q = 1f) => new Biquad(Kind.BandPass, frequency, q);

            public void Set(float frequency, float q)
            {
                double w0 = 2.0 * Math.PI * Mathf.Clamp(frequency, 10f, SampleRate * 0.45f) / SampleRate;
                double cos = Math.Cos(w0);
                double alpha = Math.Sin(w0) / (2.0 * q);
                double a0 = 1.0 + alpha;

                double nb0, nb1, nb2;
                switch (kind)
                {
                    case Kind.LowPass:
                        nb0 = (1.0 - cos) / 2.0; nb1 = 1.0 - cos; nb2 = nb0;
                        break;
                    case Kind.HighPass:
                        nb0 = (1.0 + cos) / 2.0; nb1 = -(1.0 + cos); nb2 = nb0;
                        break;
                    default:
                        nb0 = alpha; nb1 = 0.0; nb2 = -alpha;
                        break;
                }

                b0 = (float)(nb0 / a0);
                b1 = (float)(nb1 / a0);
                b2 = (float)(nb2 / a0);
                a1 = (float)(-2.0 * cos / a0);
                a2 = (float)((1.0 - alpha) / a0);
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return y;
            }
        }
    }
}
