using System;
using UnityEngine;
using static Nuclear.Audio.Dsp;

namespace Nuclear.Audio
{
    /// <summary>
    /// Все звуки игры, собранные в коде из шума, тонов и фильтров. Временная замена записям:
    /// каждый клип строится один раз при первом обращении и дальше берётся из кэша.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int FootstepVariants = 4;

        private static AudioClip click, cough, maskBreath, heartbeat, land, pickup, filterRemove, filterAttach;
        private static AudioClip impactWood, impactMetal, impactTin, sparks;
        private static AudioClip wind, distantCreak, distantHowl;
        private static AudioClip rumble, doorAlarm, doorClunk;
        private static AudioClip blindBreath, scream, swipe, hitFlesh, echoClick;
        private static AudioClip victory, defeat;
        private static readonly AudioClip[] footsteps = new AudioClip[FootstepVariants];

        // Игрок
        public static AudioClip Click => Cached(ref click, MakeClick);
        public static AudioClip Cough => Cached(ref cough, MakeCough);
        public static AudioClip MaskBreath => Cached(ref maskBreath, MakeMaskBreath);
        public static AudioClip Heartbeat => Cached(ref heartbeat, MakeHeartbeat);
        public static AudioClip Land => Cached(ref land, () => MakeStep("Land", 7, 1.6f));
        public static AudioClip Pickup => Cached(ref pickup, MakePickup);
        public static AudioClip FilterRemove => Cached(ref filterRemove, MakeFilterRemove);
        public static AudioClip FilterAttach => Cached(ref filterAttach, MakeFilterAttach);

        public static AudioClip Footstep(int variant)
        {
            int index = Mathf.Abs(variant) % FootstepVariants;
            return Cached(ref footsteps[index], () => MakeStep($"Footstep {index}", 100 + index, 1f));
        }

        // Предметы
        public static AudioClip ImpactWood => Cached(ref impactWood, MakeImpactWood);
        public static AudioClip ImpactMetal => Cached(ref impactMetal, () => MakeMetal("Impact Metal", 1.2f,
            new[] { 173f, 409f, 761f, 1187f, 1733f }, new[] { 1f, 0.7f, 0.5f, 0.35f, 0.2f }, new[] { 0.5f, 0.35f, 0.25f, 0.18f, 0.12f }));
        public static AudioClip ImpactTin => Cached(ref impactTin, () => MakeMetal("Impact Tin", 0.5f,
            new[] { 903f, 2141f, 3389f, 5011f, 6870f }, new[] { 1f, 0.6f, 0.45f, 0.3f, 0.2f }, new[] { 0.25f, 0.18f, 0.12f, 0.08f, 0.05f }));
        public static AudioClip Sparks => Cached(ref sparks, MakeSparks);

        // Мир
        public static AudioClip Wind => Cached(ref wind, MakeWind);
        public static AudioClip DistantCreak => Cached(ref distantCreak, MakeDistantCreak);
        public static AudioClip DistantHowl => Cached(ref distantHowl, MakeDistantHowl);

        // Дверь бункера
        public static AudioClip Rumble => Cached(ref rumble, MakeRumble);
        public static AudioClip DoorAlarm => Cached(ref doorAlarm, MakeDoorAlarm);
        public static AudioClip DoorClunk => Cached(ref doorClunk, () => MakeMetal("Door Clunk", 1.5f,
            new[] { 61f, 97f, 213f, 397f, 611f }, new[] { 1f, 0.8f, 0.5f, 0.3f, 0.2f }, new[] { 0.6f, 0.5f, 0.35f, 0.25f, 0.15f }));

        // Слепыш
        public static AudioClip BlindBreath => Cached(ref blindBreath, MakeBlindBreath);
        public static AudioClip Scream => Cached(ref scream, MakeScream);
        public static AudioClip Swipe => Cached(ref swipe, MakeSwipe);
        public static AudioClip HitFlesh => Cached(ref hitFlesh, MakeHitFlesh);
        public static AudioClip EchoClick => Cached(ref echoClick, MakeEchoClick);

        // Итог уровня
        public static AudioClip Victory => Cached(ref victory, () => MakeChord("Victory", new[] { 220f, 277.18f, 329.63f, 440f, 554.37f }, 0.9f));
        public static AudioClip Defeat => Cached(ref defeat, () => MakeChord("Defeat", new[] { 55f, 110f, 130.81f, 164.81f, 196f }, 0.97f));

        /// <summary>Сравнение через == учитывает уничтоженные Unity-объекты, ??= — нет.</summary>
        private static AudioClip Cached(ref AudioClip field, Func<AudioClip> make)
        {
            if (field == null)
                field = make();
            return field;
        }

        private static AudioClip MakeClick()
        {
            var random = new System.Random(1);
            var data = Buffer(0.02f);
            var ring = Biquad.BandPass(3200f, 8f);
            var air = Biquad.HighPass(1500f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float noise = White(random) * Mathf.Exp(-t * 1200f);
                float impulse = i == 0 ? 1f : 0f;
                data[i] = air.Process(noise) * 0.8f + ring.Process(impulse + noise * 0.3f) * 2f;
            }
            return ToClip("Geiger Click", data);
        }

        /// <summary>Два приглушённых маской «кхе»: шумный выдох плюс голосовой тон.</summary>
        private static AudioClip MakeCough()
        {
            var random = new System.Random(2);
            var data = Buffer(0.7f);
            var breathNoise = Biquad.BandPass(1300f, 0.9f);
            var formant = Biquad.BandPass(650f, 2.5f);
            var mask = Biquad.LowPass(2600f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float envelope = Hit(t, 0f, 0.008f, 0.07f) + Hit(t, 0.3f, 0.008f, 0.09f) * 0.85f;
                float pitch = 130f + 40f * Mathf.Exp(-(t % 0.3f) * 8f);
                phase += pitch / SampleRate;
                float voiced = formant.Process(Saw(phase)) * 0.6f;
                data[i] = mask.Process((breathNoise.Process(White(random)) * 1.2f + voiced) * envelope);
            }
            return ToClip("Cough", data);
        }

        /// <summary>Вдох через фильтр (свистящий), щелчок клапана, выдох — как в противогазе.</summary>
        private static AudioClip MakeMaskBreath()
        {
            var random = new System.Random(3);
            var data = Buffer(2.8f);
            var inhale = Biquad.BandPass(600f, 2f);
            var exhale = Biquad.BandPass(420f, 1.4f);
            var valve = Biquad.BandPass(2400f, 6f);
            var mask = Biquad.LowPass(3000f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                if (i % 64 == 0)
                    inhale.Set(600f + 300f * Mathf.Clamp01(t / 1.15f), 2f);

                float noise = White(random);
                float sample = inhale.Process(noise) * Window(t, 0f, 1.15f) * 0.8f
                    + exhale.Process(noise) * Window(t, 1.35f, 1.4f) * 1.2f
                    + valve.Process(noise) * Hit(t, 1.24f, 0.002f, 0.012f) * 0.6f;
                data[i] = mask.Process(sample);
            }
            return ToClip("Mask Breath", data, 0.7f);
        }

        private static AudioClip MakeHeartbeat()
        {
            var data = Buffer(0.7f);
            var low = Biquad.LowPass(200f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float beat = t < 0.28f ? t : t - 0.28f;
                phase += (60f - beat * 150f) / SampleRate;
                float envelope = Hit(t, 0f, 0.005f, 0.06f) + Hit(t, 0.28f, 0.005f, 0.07f) * 0.7f;
                data[i] = low.Process(Sine(phase) * envelope);
            }
            return ToClip("Heartbeat", data);
        }

        /// <summary>Шаг по пеплу и щебню: хруст, мелкие осколки и глухой удар пяткой.</summary>
        private static AudioClip MakeStep(string name, int seed, float weight)
        {
            var random = new System.Random(seed);
            var data = Buffer(0.2f * weight);
            var crunch = Biquad.BandPass(1200f + (seed % 4) * 250f, 0.8f);
            var grit = Biquad.HighPass(2500f);
            float gritAmount = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float noise = White(random);
                if (random.NextDouble() < 0.004 * Math.Exp(-t * 20f))
                    gritAmount = 1f;
                gritAmount *= 0.95f;

                float body = crunch.Process(noise) * Hit(t, 0f, 0.002f, 0.035f * weight);
                float thud = Mathf.Sin(2f * Mathf.PI * (85f / weight) * t) * Hit(t, 0f, 0.002f, 0.03f * weight) * 0.6f * weight;
                data[i] = body + grit.Process(noise) * gritAmount * 0.5f + thud;
            }
            return ToClip(name, data, 0.8f);
        }

        private static AudioClip MakePickup()
        {
            var random = new System.Random(4);
            var data = Buffer(0.15f);
            var cloth = Biquad.BandPass(2200f, 0.6f);
            for (int i = 0; i < data.Length; i++)
                data[i] = cloth.Process(White(random)) * Hit(TimeAt(i), 0f, 0.01f, 0.04f);
            return ToClip("Pickup", data, 0.6f);
        }

        /// <summary>Три щелчка резьбы и шипение: старый фильтр откручен.</summary>
        private static AudioClip MakeFilterRemove()
        {
            var random = new System.Random(5);
            var data = Buffer(0.9f);
            var thread = Biquad.BandPass(3000f, 5f);
            var hiss = Biquad.HighPass(3000f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float noise = White(random);
                float clicks = Hit(t, 0f, 0.001f, 0.01f) + Hit(t, 0.12f, 0.001f, 0.01f) + Hit(t, 0.24f, 0.001f, 0.01f);
                data[i] = thread.Process(noise) * clicks * 2f + hiss.Process(noise) * Window(t, 0.3f, 0.6f) * 0.5f;
            }
            return ToClip("Filter Remove", data, 0.7f);
        }

        /// <summary>Два щелчка и мягкий хлопок уплотнителя: новый фильтр встал.</summary>
        private static AudioClip MakeFilterAttach()
        {
            var random = new System.Random(6);
            var data = Buffer(0.4f);
            var thread = Biquad.BandPass(2600f, 5f);
            var seal = Biquad.LowPass(400f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float noise = White(random);
                float clicks = Hit(t, 0f, 0.001f, 0.01f) + Hit(t, 0.1f, 0.001f, 0.01f);
                data[i] = thread.Process(noise) * clicks * 2f + seal.Process(noise) * Hit(t, 0.25f, 0.003f, 0.04f) * 3f;
            }
            return ToClip("Filter Attach", data, 0.7f);
        }

        private static AudioClip MakeImpactWood()
        {
            var random = new System.Random(7);
            var data = Buffer(0.4f);
            var body = Biquad.LowPass(900f);
            var knock = Biquad.BandPass(420f, 4f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                phase += (110f - 40f * Mathf.Clamp01(t / 0.1f)) / SampleRate;
                float noise = White(random);
                data[i] = Sine(phase) * Hit(t, 0f, 0.002f, 0.08f)
                    + body.Process(noise) * Hit(t, 0f, 0.001f, 0.05f) * 0.8f
                    + knock.Process(noise) * Hit(t, 0f, 0.001f, 0.06f) * 2f;
            }
            return ToClip("Impact Wood", data);
        }

        /// <summary>Металл: набор негармоничных обертонов со своим затуханием плюс щелчок удара.</summary>
        private static AudioClip MakeMetal(string name, float seconds, float[] partials, float[] amplitudes, float[] decays)
        {
            var random = new System.Random(name.Length * 31);
            var data = Buffer(seconds);
            var transient = Biquad.HighPass(2000f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float sample = transient.Process(White(random)) * Hit(t, 0f, 0.0005f, 0.01f) * 0.6f;
                for (int p = 0; p < partials.Length; p++)
                    sample += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * amplitudes[p] * Hit(t, 0f, 0.001f, decays[p]);
                data[i] = sample;
            }
            return ToClip(name, data);
        }

        /// <summary>Треск искр: частые микроразряды и короткое электрическое гудение.</summary>
        private static AudioClip MakeSparks()
        {
            var random = new System.Random(8);
            var data = Buffer(0.6f);
            var crackle = Biquad.HighPass(3000f);
            var buzz = Biquad.BandPass(400f, 2f);
            float discharge = 0f;
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                if (random.NextDouble() < 300.0 / SampleRate * Math.Exp(-t * 4f))
                    discharge = 1f;
                discharge *= 0.97f;
                phase += 100.0 / SampleRate;
                data[i] = crackle.Process(White(random)) * discharge + buzz.Process(Saw(phase)) * Mathf.Exp(-t / 0.25f) * 0.3f;
            }
            return ToClip("Sparks", data);
        }

        /// <summary>Петля ветра: низкий гул с порывами и свист в щелях. Порывы кратны длине петли.</summary>
        private static AudioClip MakeWind()
        {
            const float length = 12f;
            const float fade = 0.5f;
            var random = new System.Random(9);
            var data = Buffer(length + fade);
            var low = Biquad.LowPass(500f);
            var whistle = Biquad.BandPass(850f, 9f);
            var highWhistle = Biquad.BandPass(1400f, 12f);
            float brown = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float noise = White(random);
                brown = brown * 0.98f + noise * 0.02f;
                float gust = 0.55f
                    + 0.25f * Mathf.Sin(2f * Mathf.PI * t / length)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * t / length + 1.3f)
                    + 0.05f * Mathf.Sin(2f * Mathf.PI * 7f * t / length + 0.4f);
                data[i] = low.Process(brown * 6f) * gust
                    + whistle.Process(noise) * 0.5f * gust * gust
                    + highWhistle.Process(noise) * 0.25f * Mathf.Max(0f, gust - 0.6f);
            }
            return ToClip("Wind", LoopCrossfade(data, fade), 0.8f);
        }

        /// <summary>Далёкий скрип металла: пила с плавающим тоном через узкий фильтр и эхо.</summary>
        private static AudioClip MakeDistantCreak()
        {
            var random = new System.Random(10);
            var data = Buffer(2.4f);
            var resonance = Biquad.BandPass(700f, 3f);
            var distance = Biquad.LowPass(1500f);
            double phase = 0;
            float jitter = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                jitter = jitter * 0.999f + White(random) * 0.5f;
                phase += (160f - 40f * (t / 2.4f) + jitter) / SampleRate;
                data[i] = distance.Process(resonance.Process(Saw(phase)) * Window(t, 0f, 1.8f));
            }
            Echo(data, new[] { 0.13f, 0.29f, 0.47f }, new[] { 0.4f, 0.25f, 0.15f });
            return ToClip("Distant Creak", data, 0.6f);
        }

        /// <summary>Далёкий вой: голосовой тон с вибрато, приглушённый расстоянием.</summary>
        private static AudioClip MakeDistantHowl()
        {
            var data = Buffer(3.6f);
            var formant = Biquad.BandPass(700f, 1.5f);
            var distance = Biquad.LowPass(1200f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float pitch = 380f + 180f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 2.8f)) + 8f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                phase += pitch / SampleRate;
                float voice = Sine(phase) + 0.3f * Saw(phase);
                data[i] = distance.Process(formant.Process(voice) * Window(t, 0f, 2.8f));
            }
            Echo(data, new[] { 0.21f, 0.43f, 0.71f }, new[] { 0.45f, 0.3f, 0.18f });
            return ToClip("Distant Howl", data, 0.6f);
        }

        /// <summary>Петля механизма двери: низкий гул, сетевые 50 Гц и стрекот шестерёнок.</summary>
        private static AudioClip MakeRumble()
        {
            const float length = 3f;
            const float fade = 0.25f;
            var random = new System.Random(11);
            var data = Buffer(length + fade);
            var low = Biquad.LowPass(150f);
            var gears = Biquad.BandPass(1500f, 4f);
            float brown = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float noise = White(random);
                brown = brown * 0.985f + noise * 0.015f;
                float tick = Hit(t % 0.0625f, 0f, 0.0005f, 0.004f);
                data[i] = low.Process(brown * 10f)
                    + Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.25f
                    + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.1f
                    + gears.Process(noise) * tick * 0.6f;
            }
            return ToClip("Rumble", LoopCrossfade(data, fade), 0.7f);
        }

        /// <summary>Двухтональный сигнал тревоги, будто из старого динамика.</summary>
        private static AudioClip MakeDoorAlarm()
        {
            var data = Buffer(1f);
            var speaker = Biquad.LowPass(3000f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float tone = SoftClip(3f * Mathf.Sin(2f * Mathf.PI * 660f * t)) * Window(t, 0f, 0.42f)
                    + SoftClip(3f * Mathf.Sin(2f * Mathf.PI * 520f * t)) * Window(t, 0.5f, 0.42f);
                data[i] = speaker.Process(tone);
            }
            return ToClip("Door Alarm", data, 0.6f);
        }

        /// <summary>Петля дыхания Слепыша: мокрый вдох с бульканьем и хриплый выдох с рыком.</summary>
        private static AudioClip MakeBlindBreath()
        {
            const float length = 3.2f;
            const float fade = 0.2f;
            var random = new System.Random(12);
            var data = Buffer(length + fade);
            var wet = Biquad.BandPass(350f, 3f);
            var rasp = Biquad.BandPass(900f, 1.2f);
            var growl = Biquad.BandPass(300f, 2f);
            var body = Biquad.LowPass(4000f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float local = t % length;
                float noise = White(random);
                phase += 68.0 / SampleRate;
                float inhale = wet.Process(noise) * Window(local, 0f, 1.3f) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 28f * t));
                float exhale = (rasp.Process(noise) + growl.Process(Saw(phase)) * 0.5f) * Window(local, 1.5f, 1.5f);
                data[i] = body.Process(inhale * 1.2f + exhale);
            }
            return ToClip("Blind Breath", LoopCrossfade(data, fade), 0.8f);
        }

        /// <summary>Рык и визг при броске: низкий рокочущий тон и поднимающийся вопль, перегруженные.</summary>
        private static AudioClip MakeScream()
        {
            var random = new System.Random(13);
            var data = Buffer(1.6f);
            var throat = Biquad.BandPass(500f, 2f);
            var throatHigh = Biquad.BandPass(1400f, 3f);
            var shriek = Biquad.BandPass(2200f, 2.5f);
            var breath = Biquad.BandPass(1800f, 1f);
            double growlPhase = 0, shriekPhase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float noise = White(random);
                growlPhase += (85f + 10f * Mathf.Sin(2f * Mathf.PI * 7f * t)) / SampleRate;
                shriekPhase += (300f + 220f * Mathf.Clamp01((t - 0.15f) / 0.4f) + 15f * Mathf.Sin(2f * Mathf.PI * 9f * t)) / SampleRate;

                float growl = Saw(growlPhase) * (0.8f + 0.2f * noise);
                float sample = (throat.Process(growl) + 0.6f * throatHigh.Process(growl)) * Window(t, 0f, 1.6f)
                    + shriek.Process(Saw(shriekPhase)) * Window(t, 0.15f, 1.3f) * 0.8f
                    + breath.Process(noise) * Window(t, 0f, 1.6f) * 0.2f;
                data[i] = SoftClip(sample * 1.5f);
            }
            return ToClip("Scream", data);
        }

        /// <summary>Свист взмаха: полоса шума, которая проходит вверх и обратно вниз.</summary>
        private static AudioClip MakeSwipe()
        {
            var random = new System.Random(14);
            var data = Buffer(0.35f);
            var air = Biquad.BandPass(500f, 1.5f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                if (i % 32 == 0)
                {
                    float x = t / 0.35f;
                    air.Set(x < 0.5f ? Mathf.Lerp(500f, 2400f, x * 2f) : Mathf.Lerp(2400f, 700f, (x - 0.5f) * 2f), 1.5f);
                }
                data[i] = air.Process(White(random)) * Window(t, 0f, 0.35f);
            }
            return ToClip("Swipe", data, 0.7f);
        }

        /// <summary>Удар по живому: глухой низ и шлепок.</summary>
        private static AudioClip MakeHitFlesh()
        {
            var random = new System.Random(15);
            var data = Buffer(0.4f);
            var slap = Biquad.LowPass(1800f);
            var crunch = Biquad.BandPass(900f, 1.5f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float noise = White(random);
                data[i] = Mathf.Sin(2f * Mathf.PI * 70f * t) * Hit(t, 0f, 0.002f, 0.08f)
                    + slap.Process(noise) * Hit(t, 0f, 0.002f, 0.04f) * 0.8f
                    + crunch.Process(noise) * Hit(t, 0.01f, 0.002f, 0.05f) * 0.6f;
            }
            return ToClip("Hit Flesh", data);
        }

        /// <summary>Щелчок языком: Слепыш «прощупывает» пространство звуком.</summary>
        private static AudioClip MakeEchoClick()
        {
            var random = new System.Random(16);
            var data = Buffer(0.12f);
            var tongue = Biquad.BandPass(2600f, 12f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float impulse = i < 40 ? 1f : 0f;
                data[i] = tongue.Process(impulse + White(random) * Mathf.Exp(-t * 400f) * 0.2f);
            }
            return ToClip("Echo Click", data, 0.8f);
        }

        /// <summary>Медленный аккорд для конца уровня. decayBias &lt; 1 — к концу звук глохнет и темнеет.</summary>
        private static AudioClip MakeChord(string name, float[] notes, float decayBias)
        {
            var data = Buffer(4.5f);
            var soften = Biquad.LowPass(name == "Defeat" ? 700f : 2500f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float envelope = Mathf.Clamp01(t / 1.2f) * Mathf.Exp(-Mathf.Max(0f, t - 1.5f) * (1.1f - decayBias) * 2f) * Window(t, 0f, 4.5f);
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float detune = 1f + 0.002f * Mathf.Sin(2f * Mathf.PI * (0.3f + n * 0.1f) * t);
                    sample += Mathf.Sin(2f * Mathf.PI * notes[n] * detune * t) / (1f + n * 0.4f);
                }
                data[i] = soften.Process(sample * envelope);
            }
            return ToClip(name, data, 0.6f);
        }
    }
}
