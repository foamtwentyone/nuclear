using System;
using UnityEngine;
using static Nuclear.Audio.Dsp;

namespace Nuclear.Audio
{
    /// <summary>
    /// Все звуки игры, синтезированные в коде. Временная замена записям, но со звуковым дизайном:
    /// розовый шум вместо белого, хруст из «зёрен», удары из собственных частот материала,
    /// голоса через форманты, немного реверберации для пространства.
    /// Каждый клип строится один раз при первом обращении и дальше берётся из кэша.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int FootstepVariants = 6;

        private static AudioClip click, cough, maskBreath, heartbeat, land, pickup, filterRemove, filterAttach;
        private static AudioClip impactWood, impactHeavy, impactTin, sparks;
        private static AudioClip wind, distantCreak, distantHowl;
        private static AudioClip rumble, doorAlarm, doorClunk;
        private static AudioClip blindPurr, sniff, scream, bite, chatter, hitFlesh, heavyStep;
        private static AudioClip victory, defeat;
        private static readonly AudioClip[] footsteps = new AudioClip[FootstepVariants];

        // Игрок
        public static AudioClip Click => Cached(ref click, MakeClick);
        public static AudioClip Cough => Cached(ref cough, MakeCough);
        public static AudioClip MaskBreath => Cached(ref maskBreath, MakeMaskBreath);
        public static AudioClip Heartbeat => Cached(ref heartbeat, MakeHeartbeat);
        public static AudioClip Land => Cached(ref land, () => MakeStep("Land", 77, grains: 60, spread: 0.15f, heel: 0.06f, gain: 1.3f));
        public static AudioClip Pickup => Cached(ref pickup, MakePickup);
        public static AudioClip FilterRemove => Cached(ref filterRemove, MakeFilterRemove);
        public static AudioClip FilterAttach => Cached(ref filterAttach, MakeFilterAttach);

        public static AudioClip Footstep(int variant)
        {
            int index = Mathf.Abs(variant) % FootstepVariants;
            return Cached(ref footsteps[index], () => MakeStep($"Footstep {index}", 100 + index, grains: 32, spread: 0.09f, heel: 0.025f, gain: 1f));
        }

        // Предметы
        public static AudioClip ImpactWood => Cached(ref impactWood, MakeImpactWood);
        public static AudioClip ImpactHeavy => Cached(ref impactHeavy, MakeImpactHeavy);
        public static AudioClip ImpactTin => Cached(ref impactTin, MakeImpactTin);
        public static AudioClip Sparks => Cached(ref sparks, MakeSparks);

        // Мир
        public static AudioClip Wind => Cached(ref wind, MakeWind);
        public static AudioClip DistantCreak => Cached(ref distantCreak, MakeDistantCreak);
        public static AudioClip DistantHowl => Cached(ref distantHowl, MakeDistantHowl);

        // Дверь бункера
        public static AudioClip Rumble => Cached(ref rumble, MakeRumble);
        public static AudioClip DoorAlarm => Cached(ref doorAlarm, MakeDoorAlarm);
        public static AudioClip DoorClunk => Cached(ref doorClunk, MakeDoorClunk);

        // Слепыш — гигантская морская свинка
        public static AudioClip BlindPurr => Cached(ref blindPurr, MakeBlindPurr);
        public static AudioClip Sniff => Cached(ref sniff, MakeSniff);
        public static AudioClip Scream => Cached(ref scream, MakeScream);
        public static AudioClip Bite => Cached(ref bite, MakeBite);
        public static AudioClip Chatter => Cached(ref chatter, MakeChatter);
        public static AudioClip HitFlesh => Cached(ref hitFlesh, MakeHitFlesh);
        public static AudioClip HeavyStep => Cached(ref heavyStep, MakeHeavyStep);

        // Итог уровня
        public static AudioClip Victory => Cached(ref victory, () => MakePad("Victory", new[] { 220f, 277.18f, 329.63f, 493.88f, 659.25f }, opening: true));
        public static AudioClip Defeat => Cached(ref defeat, () => MakePad("Defeat", new[] { 73.42f, 146.83f, 174.61f, 220f }, opening: false));

        /// <summary>Сравнение через == учитывает уничтоженные Unity-объекты, ??= — нет.</summary>
        private static AudioClip Cached(ref AudioClip field, Func<AudioClip> make)
        {
            if (field == null)
                field = make();
            return field;
        }

        // ---------- Игрок ----------

        /// <summary>Щелчок счётчика Гейгера: короткий импульс через два резонатора, без резкого верха.</summary>
        private static AudioClip MakeClick()
        {
            var random = new System.Random(1);
            var data = Buffer(0.03f);
            var body = Biquad.BandPass(2400f, 6f);
            var tick = Biquad.BandPass(4500f, 4f);
            var soften = Biquad.LowPass(7000f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float impulse = (i == 0 ? 1f : 0f) + White(random) * Mathf.Exp(-t * 2500f) * 0.4f;
                data[i] = soften.Process(body.Process(impulse) * 1.5f + tick.Process(impulse));
            }
            return ToClip("Geiger Click", data);
        }

        /// <summary>
        /// Кашель в противогазе: шумное «кх» переходит в голосовой толчок (пила через три форманты),
        /// всё глушится резиной маски. Два толчка, второй слабее.
        /// </summary>
        private static AudioClip MakeCough()
        {
            var noise = new PinkNoise(2);
            var random = new System.Random(2);
            var data = Buffer(0.75f);
            var voice = new Saw();
            var f1 = Biquad.BandPass(600f, 4f);
            var f2 = Biquad.BandPass(1200f, 5f);
            var f3 = Biquad.BandPass(2500f, 6f);
            var aspiration = Biquad.BandPass(1800f, 0.8f);
            var mask = Biquad.LowPass(2400f);
            var maskResonance = Biquad.BandPass(900f, 2f);

            foreach (float start in new[] { 0f, 0.32f })
            {
                float strength = start > 0f ? 0.8f : 1f;
                for (int i = Mathf.RoundToInt(start * SampleRate); i < data.Length; i++)
                {
                    float local = TimeAt(i) - start;
                    if (local > 0.3f)
                        break;
                    float envelope = Hit(local, 0f, 0.012f, 0.09f) * strength;
                    float voicedShare = Mathf.Clamp01((local - 0.03f) / 0.05f);
                    float pitch = (135f - local * 60f) * (1f + (float)(random.NextDouble() - 0.5) * 0.03f);
                    float glottal = voice.Next(pitch);
                    float vowel = f1.Process(glottal) + f2.Process(glottal) * 0.6f + f3.Process(glottal) * 0.3f;
                    float breath = aspiration.Process(noise.Next()) * 2.5f;
                    data[i] += (breath * (1f - voicedShare * 0.6f) + vowel * voicedShare) * envelope;
                }
            }

            for (int i = 0; i < data.Length; i++)
            {
                float muffled = mask.Process(data[i]);
                data[i] = muffled + maskResonance.Process(muffled) * 0.4f;
            }
            return ToClip("Cough", data);
        }

        /// <summary>Вдох через фильтр (мягкий свист), тихий клапан, выдох с дрожанием резины.</summary>
        private static AudioClip MakeMaskBreath()
        {
            var noise = new PinkNoise(3);
            var data = Buffer(2.8f);
            var inhale = Biquad.BandPass(900f, 1.5f);
            var hiss = Biquad.HighPass(3000f);
            var exhale = Biquad.BandPass(450f, 1f);
            var soften = Biquad.LowPass(5000f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                if (i % 64 == 0)
                    inhale.Set(900f + 400f * Mathf.Clamp01(t / 1.15f), 1.5f);
                float n = noise.Next();
                float inhaleEnvelope = Window(t, 0f, 1.15f);
                float exhaleEnvelope = Window(t, 1.35f, 1.35f) * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 28f * t));
                data[i] = soften.Process(inhale.Process(n) * inhaleEnvelope + hiss.Process(n) * inhaleEnvelope * 0.15f + exhale.Process(n) * exhaleEnvelope * 1.2f);
            }
            AddGrain(data, 1.24f, 1800f, 0.008f, 0.25f);
            return ToClip("Mask Breath", data, 0.7f);
        }

        /// <summary>Сердце: два глухих толчка с падающим тоном.</summary>
        private static AudioClip MakeHeartbeat()
        {
            var data = Buffer(0.7f);
            var low = Biquad.LowPass(180f);
            foreach (var (start, strength) in new[] { (0f, 1f), (0.26f, 0.7f) })
            {
                double phase = 0;
                for (int i = Mathf.RoundToInt(start * SampleRate); i < data.Length; i++)
                {
                    float local = TimeAt(i) - start;
                    if (local > 0.25f)
                        break;
                    phase += (70f - 25f * Mathf.Clamp01(local / 0.08f)) / SampleRate;
                    float tone = Sine(phase) + 0.3f * Sine(phase * 2.0);
                    data[i] += tone * Hit(local, 0f, 0.004f, 0.05f) * strength;
                }
            }
            Filter(data, low);
            return ToClip("Heartbeat", data);
        }

        /// <summary>
        /// Шаг по пеплу и щебню: десятки коротких «зёрен» разной высоты (хруст камешков)
        /// и мягкий удар пятки. grains — сколько камешков, spread — за какое время, heel — длина удара.
        /// </summary>
        private static AudioClip MakeStep(string name, int seed, int grains, float spread, float heel, float gain)
        {
            var random = new System.Random(seed);
            var noise = new PinkNoise(seed);
            var data = Buffer(spread + 0.12f);

            for (int g = 0; g < grains; g++)
            {
                // Больше камешков в начале шага.
                float start = spread * (float)Math.Pow(random.NextDouble(), 1.8);
                float frequency = MeshRange(random, 900f, 3800f);
                float amplitude = MeshRange(random, 0.2f, 1f) * Mathf.Exp(-start / (spread * 0.45f));
                AddGrain(data, start, frequency, MeshRange(random, 0.0015f, 0.005f), amplitude * 0.35f);
            }

            var heelFilter = Biquad.LowPass(250f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float thump = heelFilter.Process(noise.Next()) * Hit(t, 0f, 0.003f, heel) * 3f
                    + Mathf.Sin(2f * Mathf.PI * 75f * t) * Hit(t, 0f, 0.003f, heel * 1.2f) * 0.5f;
                data[i] = (data[i] + thump) * gain;
            }

            Filter(data, Biquad.LowPass(6500f));
            Filter(data, Biquad.HighPass(60f));
            return ToClip(name, data, 0.8f);
        }

        /// <summary>Шорох одежды и тихий звяк снаряжения при подборе.</summary>
        private static AudioClip MakePickup()
        {
            var random = new System.Random(4);
            var noise = new PinkNoise(4);
            var data = Buffer(0.2f);
            var cloth = Biquad.BandPass(1600f, 0.7f);
            for (int i = 0; i < data.Length; i++)
                data[i] = cloth.Process(noise.Next()) * Hit(TimeAt(i), 0f, 0.015f, 0.05f) * 3f;
            for (int g = 0; g < 3; g++)
                AddGrain(data, MeshRange(random, 0.02f, 0.08f), MeshRange(random, 2200f, 3200f), 0.006f, 0.15f);
            return ToClip("Pickup", data, 0.6f);
        }

        /// <summary>Три щелчка резьбы и выход воздуха: старый фильтр откручен.</summary>
        private static AudioClip MakeFilterRemove()
        {
            var random = new System.Random(5);
            var noise = new PinkNoise(5);
            var data = Buffer(1f);
            foreach (float tick in new[] { 0f, 0.11f, 0.23f })
                AddModes(data, tick, new[] { 3200f, 1900f }, new[] { 1f, 0.5f }, new[] { 0.006f, 0.008f }, 0.6f, random);
            var air = Biquad.HighPass(2500f);
            for (int i = 0; i < data.Length; i++)
                data[i] += air.Process(noise.Next()) * Window(TimeAt(i), 0.3f, 0.6f) * 0.6f;
            return ToClip("Filter Remove", data, 0.7f);
        }

        /// <summary>Два щелчка и мягкий хлопок уплотнителя: новый фильтр встал.</summary>
        private static AudioClip MakeFilterAttach()
        {
            var random = new System.Random(6);
            var noise = new PinkNoise(6);
            var data = Buffer(0.45f);
            foreach (float tick in new[] { 0f, 0.1f })
                AddModes(data, tick, new[] { 2900f, 1700f }, new[] { 1f, 0.5f }, new[] { 0.006f, 0.008f }, 0.6f, random);
            var seal = Biquad.LowPass(300f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                data[i] += seal.Process(noise.Next()) * Hit(t, 0.25f, 0.003f, 0.03f) * 6f
                    + Mathf.Sin(2f * Mathf.PI * 180f * t) * Hit(t, 0.25f, 0.003f, 0.04f) * 0.3f;
            }
            return ToClip("Filter Attach", data, 0.7f);
        }

        // ---------- Предметы ----------

        /// <summary>Деревянный ящик: глухие собственные частоты доски и удар корпуса.</summary>
        private static AudioClip MakeImpactWood()
        {
            var random = new System.Random(7);
            var data = Buffer(0.35f);
            AddModes(data, 0f, new[] { 220f, 480f, 840f, 1290f, 1910f }, new[] { 1f, 0.7f, 0.45f, 0.3f, 0.18f }, new[] { 0.07f, 0.05f, 0.035f, 0.025f, 0.015f }, 1f, random);
            AddTransient(data, 8, 2500f, 0.003f, 0.6f);
            AddGrain(data, 0f, 110f, 0.03f, 0.8f);
            return ToClip("Impact Wood", Reverb(data, 0.3f, 0.4f, 0.06f));
        }

        /// <summary>Аккумулятор: тяжёлый глухой удар пластикового корпуса со свинцом внутри, без звона.</summary>
        private static AudioClip MakeImpactHeavy()
        {
            var random = new System.Random(9);
            var noise = new PinkNoise(9);
            var data = Buffer(0.45f);
            var body = Biquad.LowPass(400f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                phase += (80f - 25f * Mathf.Clamp01(t / 0.1f)) / SampleRate;
                data[i] = Sine(phase) * Hit(t, 0f, 0.002f, 0.08f) + body.Process(noise.Next()) * Hit(t, 0f, 0.002f, 0.04f) * 4f;
            }
            AddModes(data, 0f, new[] { 310f, 720f, 1180f }, new[] { 0.5f, 0.3f, 0.15f }, new[] { 0.04f, 0.025f, 0.015f }, 1f, random);
            return ToClip("Impact Heavy", Reverb(data, 0.3f, 0.4f, 0.06f));
        }

        /// <summary>Жестянка: звонкое «дзынь» и короткий отскок.</summary>
        private static AudioClip MakeImpactTin()
        {
            var random = new System.Random(10);
            var data = Buffer(0.4f);
            var modes = new[] { 1180f, 2650f, 4120f, 5760f };
            var decays = new[] { 0.12f, 0.08f, 0.05f, 0.03f };
            AddModes(data, 0f, modes, new[] { 1f, 0.6f, 0.35f, 0.2f }, decays, 1f, random);
            AddModes(data, 0.06f, modes, new[] { 1f, 0.6f, 0.35f, 0.2f }, decays, 0.3f, random);
            AddTransient(data, 10, 4000f, 0.002f, 0.4f);
            return ToClip("Impact Tin", Reverb(data, 0.25f, 0.4f, 0.05f));
        }

        /// <summary>Искры: частые микроразряды, шипение и короткое гудение.</summary>
        private static AudioClip MakeSparks()
        {
            var random = new System.Random(11);
            var noise = new PinkNoise(11);
            var data = Buffer(0.6f);
            var crackle = Biquad.BandPass(4000f, 1f);
            var sizzle = Biquad.HighPass(5000f);
            var buzzFilter = Biquad.LowPass(300f);
            var buzz = new Saw();
            float discharge = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                if (random.NextDouble() < 300.0 / SampleRate * Math.Exp(-t * 4f))
                    discharge = 1f;
                discharge *= 0.97f;
                float n = noise.Next();
                data[i] = SoftClip(crackle.Process(White(random)) * discharge * 2f
                    + sizzle.Process(n) * Mathf.Exp(-t / 0.3f) * 0.5f
                    + buzzFilter.Process(buzz.Next(100f)) * Mathf.Exp(-t / 0.25f) * 0.3f);
            }
            return ToClip("Sparks", data);
        }

        // ---------- Мир ----------

        /// <summary>
        /// Ветер в стерео: в каждом канале свой розовый шум, низкий гул, средний поток и тихий свист.
        /// Порывы — сумма синусов с целым числом периодов на длину петли, поэтому петля бесшовная.
        /// </summary>
        private static AudioClip MakeWind()
        {
            const float length = 16f;
            const float fade = 0.6f;
            var left = WindChannel(21, length, fade, 0f);
            var right = WindChannel(22, length, fade, 0.9f);
            return ToStereoClip("Wind", LoopCrossfade(left, fade), LoopCrossfade(right, fade), 0.7f);
        }

        private static float[] WindChannel(int seed, float length, float fade, float phaseShift)
        {
            var noise = new PinkNoise(seed);
            var data = Buffer(length + fade);
            var low = Biquad.LowPass(300f);
            var mid = Biquad.BandPass(700f, 0.7f);
            var whistle = Biquad.BandPass(900f, 6f);
            float Gust(float t, float a, float b) =>
                0.5f + 0.25f * Mathf.Sin(2f * Mathf.PI * a * t / length + phaseShift) + 0.15f * Mathf.Sin(2f * Mathf.PI * b * t / length + 1.3f + phaseShift);

            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                if (i % 128 == 0)
                    whistle.Set(900f + 150f * Mathf.Sin(2f * Mathf.PI * t / length + phaseShift), 6f);
                float n = noise.Next();
                float gust = Gust(t, 1f, 3f);
                float breeze = Gust(t, 2f, 5f);
                data[i] = low.Process(n) * 2.5f * gust + mid.Process(n) * 1.2f * breeze + whistle.Process(n) * 0.6f * gust * gust * breeze;
            }
            return data;
        }

        /// <summary>Далёкий скрип металла с эхом пустых улиц.</summary>
        private static AudioClip MakeDistantCreak()
        {
            var random = new System.Random(12);
            var data = Buffer(2.4f);
            var saw = new Saw();
            var r1 = Biquad.BandPass(650f, 4f);
            var r2 = Biquad.BandPass(1300f, 6f);
            var distance = Biquad.LowPass(1800f);
            float jitter = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                jitter = jitter * 0.999f + White(random) * 0.5f;
                float source = saw.Next(160f - 40f * (t / 2.4f) + jitter);
                data[i] = distance.Process((r1.Process(source) + r2.Process(source) * 0.5f) * Window(t, 0f, 1.8f));
            }
            return ToClip("Distant Creak", Reverb(data, 1.5f, 0.85f, 0.35f), 0.6f);
        }

        /// <summary>Далёкий протяжный вой — то ли ветер в трубах, то ли что-то живое.</summary>
        private static AudioClip MakeDistantHowl()
        {
            var data = Buffer(3.6f);
            var saw = new Saw();
            var formant = Biquad.BandPass(700f, 2f);
            var distance = Biquad.LowPass(1200f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float pitch = 380f + 180f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 2.8f)) + 8f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                phase += pitch / SampleRate;
                float voice = Sine(phase) + 0.25f * formant.Process(saw.Next(pitch));
                data[i] = distance.Process(voice * Window(t, 0f, 2.8f));
            }
            return ToClip("Distant Howl", Reverb(data, 2f, 0.9f, 0.4f), 0.6f);
        }

        // ---------- Дверь бункера ----------

        /// <summary>Петля механизма двери: низкий гул, мягкие 50 Гц и негромкое стрекотание шестерёнок.</summary>
        private static AudioClip MakeRumble()
        {
            const float length = 3f;
            const float fade = 0.25f;
            var random = new System.Random(13);
            var noise = new PinkNoise(13);
            var data = Buffer(length + fade);
            var low = Biquad.LowPass(140f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                data[i] = low.Process(noise.Next()) * 4f + Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.18f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.06f;
            }
            for (float tick = 0f; tick < length + fade; tick += 0.0625f)
                AddGrain(data, tick, MeshRange(random, 1100f, 1400f), 0.003f, 0.12f);
            return ToClip("Rumble", LoopCrossfade(data, fade), 0.7f);
        }

        /// <summary>Двухтональная сирена: тёплый тон с обертонами вместо резкого меандра, с эхом.</summary>
        private static AudioClip MakeDoorAlarm()
        {
            var data = Buffer(1f);
            foreach (var (start, frequency) in new[] { (0f, 740f), (0.45f, 587f) })
            {
                for (int i = Mathf.RoundToInt(start * SampleRate); i < data.Length; i++)
                {
                    float local = TimeAt(i) - start;
                    if (local > 0.4f)
                        break;
                    float f = frequency * (1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 6f * local));
                    float tone = Mathf.Sin(2f * Mathf.PI * f * local) + 0.25f * Mathf.Sin(4f * Mathf.PI * f * local) + 0.1f * Mathf.Sin(6f * Mathf.PI * f * local);
                    data[i] += tone * Window(local, 0f, 0.4f);
                }
            }
            return ToClip("Door Alarm", Reverb(data, 0.8f, 0.7f, 0.25f), 0.6f);
        }

        /// <summary>Тяжёлый лязг створки с отзвуком.</summary>
        private static AudioClip MakeDoorClunk()
        {
            var random = new System.Random(14);
            var data = Buffer(1.4f);
            AddModes(data, 0f, new[] { 61f, 97f, 213f, 397f, 611f, 950f }, new[] { 1f, 0.8f, 0.5f, 0.3f, 0.2f, 0.1f }, new[] { 0.6f, 0.5f, 0.35f, 0.25f, 0.15f, 0.08f }, 1f, random);
            AddTransient(data, 14, 1500f, 0.004f, 0.5f);
            return ToClip("Door Clunk", Reverb(data, 1f, 0.75f, 0.25f));
        }

        // ---------- Слепыш ----------

        /// <summary>
        /// Петля дыхания-урчания: морские свинки урчат частыми низкими толчками. У гиганта это
        /// 55 Гц с обертонами, пульсирующие 12.5 раз в секунду, поверх — сиплое дыхание.
        /// </summary>
        private static AudioClip MakeBlindPurr()
        {
            const float length = 3.6f;
            const float fade = 0.2f;
            var noise = new PinkNoise(15);
            var data = Buffer(length + fade);
            var breath = Biquad.BandPass(600f, 0.8f);
            var soften = Biquad.LowPass(2000f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float local = t % length;
                float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * 12.5f * t), 3f);
                float tone = Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 110f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 165f * t);
                float inhale = Window(local, 0f, 1.4f);
                float exhale = Window(local, 1.6f, 1.8f);
                float purr = tone * pulse * (0.35f * inhale + exhale);
                float air = breath.Process(noise.Next()) * (inhale * 1.6f + exhale * 0.8f);
                data[i] = soften.Process(purr + air * 2f);
            }
            return ToClip("Blind Purr", LoopCrossfade(data, fade), 0.8f);
        }

        /// <summary>Серия из трёх быстрых шмыганий носом — Слепыш ищет запах и звук.</summary>
        private static AudioClip MakeSniff()
        {
            var noise = new PinkNoise(16);
            var data = Buffer(0.5f);
            var nose = Biquad.BandPass(2000f, 1.2f);
            var air = Biquad.HighPass(1200f);
            foreach (var (start, strength) in new[] { (0f, 1f), (0.12f, 0.85f), (0.24f, 0.7f) })
            {
                for (int i = Mathf.RoundToInt(start * SampleRate); i < data.Length; i++)
                {
                    float local = TimeAt(i) - start;
                    if (local > 0.1f)
                        break;
                    if (i % 32 == 0)
                        nose.Set(2000f + 1200f * (local / 0.1f), 1.2f);
                    data[i] += air.Process(nose.Process(noise.Next())) * Hit(local, 0f, 0.01f, 0.03f) * strength * 4f;
                }
            }
            return ToClip("Sniff", data, 0.7f);
        }

        /// <summary>
        /// Визг при броске: морская свинка кричит «уи-и-ик» — восходящий писк. У гиганта он ниже на октаву,
        /// с носовыми формантами, а под ним низкий рык.
        /// </summary>
        private static AudioClip MakeScream()
        {
            var random = new System.Random(17);
            var noise = new PinkNoise(17);
            var data = Buffer(1.4f);
            var squeal = new Saw();
            var growl = new Saw();
            var f1 = Biquad.BandPass(900f, 3f);
            var f2 = Biquad.BandPass(2100f, 4f);
            var f3 = Biquad.BandPass(3300f, 5f);
            var growlFilter = Biquad.LowPass(400f);
            var breath = Biquad.BandPass(1800f, 1f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                float pitch = t < 0.15f ? Mathf.Lerp(320f, 520f, t / 0.15f)
                    : t < 0.8f ? 520f + 25f * Mathf.Sin(2f * Mathf.PI * 6f * t)
                    : Mathf.Lerp(520f, 760f, Mathf.Clamp01((t - 0.8f) / 0.3f));
                float source = squeal.Next(pitch);
                float nasal = f1.Process(source) + f2.Process(source) * 0.7f + f3.Process(source) * 0.35f;
                float rough = growlFilter.Process(growl.Next(70f + (float)random.NextDouble() * 8f)) * (0.8f + 0.2f * White(random));
                float sample = nasal * Window(t, 0f, 1.25f) + rough * Window(t, 0f, 1.1f) * 0.6f + breath.Process(noise.Next()) * Window(t, 0f, 1.2f) * 0.5f;
                data[i] = SoftClip(sample * 1.3f);
            }
            return ToClip("Scream", Reverb(data, 0.5f, 0.6f, 0.15f));
        }

        /// <summary>Укус: короткий бросок воздуха и два щелчка резцов.</summary>
        private static AudioClip MakeBite()
        {
            var random = new System.Random(18);
            var noise = new PinkNoise(18);
            var data = Buffer(0.4f);
            var whoosh = Biquad.BandPass(300f, 1.2f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                if (i % 32 == 0)
                    whoosh.Set(Mathf.Lerp(300f, 1500f, Mathf.Clamp01(t / 0.15f)), 1.2f);
                data[i] = whoosh.Process(noise.Next()) * Window(t, 0f, 0.16f) * 3f;
            }
            foreach (float clack in new[] { 0.14f, 0.2f })
                AddModes(data, clack, new[] { 2200f, 3400f, 5100f }, new[] { 0.8f, 1f, 0.5f }, new[] { 0.012f, 0.01f, 0.006f }, 0.8f, random);
            AddGrain(data, 0.14f, 120f, 0.025f, 0.6f);
            return ToClip("Bite", data);
        }

        /// <summary>Стук зубов: так морские свинки предупреждают. Звучит, когда Слепыш потерял добычу.</summary>
        private static AudioClip MakeChatter()
        {
            var random = new System.Random(19);
            var data = Buffer(0.6f);
            for (int c = 0; c < 8; c++)
            {
                float start = c / 18f + MeshRange(random, -0.004f, 0.004f);
                AddModes(data, start, new[] { 2600f, 3900f }, new[] { 1f, 0.6f }, new[] { 0.008f, 0.005f }, 1f - c * 0.08f, random);
            }
            return ToClip("Chatter", Reverb(data, 0.3f, 0.5f, 0.1f), 0.7f);
        }

        /// <summary>Попадание по игроку: глухой удар и хруст.</summary>
        private static AudioClip MakeHitFlesh()
        {
            var random = new System.Random(20);
            var noise = new PinkNoise(20);
            var data = Buffer(0.4f);
            var slap = Biquad.LowPass(1500f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                data[i] = Mathf.Sin(2f * Mathf.PI * 65f * t) * Hit(t, 0f, 0.002f, 0.07f) + slap.Process(noise.Next()) * Hit(t, 0f, 0.002f, 0.035f) * 4f;
            }
            for (int g = 0; g < 8; g++)
                AddGrain(data, MeshRange(random, 0.005f, 0.05f), MeshRange(random, 800f, 1600f), 0.004f, 0.25f);
            return ToClip("Hit Flesh", data);
        }

        /// <summary>Тяжёлый мягкий шаг гиганта: низкий удар в землю и осыпающаяся земля.</summary>
        private static AudioClip MakeHeavyStep()
        {
            var random = new System.Random(21);
            var noise = new PinkNoise(21);
            var data = Buffer(0.45f);
            var earth = Biquad.LowPass(180f);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                phase += (45f - 15f * Mathf.Clamp01(t / 0.12f)) / SampleRate;
                data[i] = Sine(phase) * Hit(t, 0f, 0.006f, 0.09f) + earth.Process(noise.Next()) * Hit(t, 0f, 0.004f, 0.06f) * 5f;
            }
            for (int g = 0; g < 10; g++)
                AddGrain(data, MeshRange(random, 0.01f, 0.12f), MeshRange(random, 300f, 900f), 0.006f, 0.08f);
            return ToClip("Heavy Step", Reverb(data, 0.4f, 0.5f, 0.1f));
        }

        // ---------- Итог уровня ----------

        /// <summary>
        /// Тёплый аккорд из трёх слегка расстроенных пил на ноту, через мягкий фильтр.
        /// При победе фильтр раскрывается (светлеет), при поражении закрывается (глохнет).
        /// </summary>
        private static AudioClip MakePad(string name, float[] notes, bool opening)
        {
            var data = Buffer(4.5f);
            var voices = new Saw[notes.Length * 3];
            for (int v = 0; v < voices.Length; v++)
                voices[v] = new Saw();
            var tone = Biquad.LowPass(800f, 0.9f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = TimeAt(i);
                if (i % 128 == 0)
                    tone.Set(opening ? Mathf.Lerp(600f, 3000f, t / 3f) : Mathf.Lerp(1400f, 250f, t / 3.5f), 0.9f);
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    for (int d = 0; d < 3; d++)
                        sample += voices[n * 3 + d].Next(notes[n] * (1f + (d - 1) * 0.004f)) / (1f + n * 0.3f);
                }
                float envelope = Mathf.Clamp01(t / 0.8f) * Window(t, 0f, 4.5f) * (opening ? 1f : Mathf.Exp(-t * 0.3f));
                data[i] = tone.Process(sample * envelope);
            }
            return ToClip(name, Reverb(data, 2f, 0.9f, 0.35f), 0.6f);
        }

        // ---------- Вспомогательное ----------

        /// <summary>Короткий шумовой щелчок в начале удара (касание поверхности).</summary>
        private static void AddTransient(float[] data, int seed, float cutoff, float decay, float gain)
        {
            var noise = new PinkNoise(seed);
            var filter = Biquad.LowPass(cutoff);
            int length = Mathf.Min(data.Length, Mathf.CeilToInt(decay * 8f * SampleRate));
            for (int i = 0; i < length; i++)
                data[i] += filter.Process(noise.Next()) * Mathf.Exp(-TimeAt(i) / decay) * gain * 4f;
        }

        private static float MeshRange(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
