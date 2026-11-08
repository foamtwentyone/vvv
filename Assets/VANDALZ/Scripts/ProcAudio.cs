using System.Collections.Generic;
using UnityEngine;

// Все звуки игры, сгенерированные кодом (файлы не нужны). Клипы создаются один раз и кешируются.
// Play2D — короткий звук «в голове» игрока, PlayAt — звук в точке мира.
public static class ProcAudio
{
    const int Rate = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static AudioSource oneShot2D;
    static System.Random rng = new System.Random(1337);

    // ---------- Воспроизведение ----------

    public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        if (oneShot2D == null)
        {
            var go = new GameObject("UI Audio");
            Object.DontDestroyOnLoad(go);
            oneShot2D = go.AddComponent<AudioSource>();
            oneShot2D.spatialBlend = 0f;
            oneShot2D.playOnAwake = false;
        }
        oneShot2D.pitch = pitch;
        oneShot2D.PlayOneShot(clip, volume);
    }

    public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        var go = new GameObject("SFX");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 1f;
        src.minDistance = 2f;
        src.maxDistance = 30f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    static float Rand() => (float)rng.NextDouble() * 2f - 1f;

    static AudioClip Make(string name, float seconds, System.Func<int, float, float> sample)
    {
        if (cache.TryGetValue(name, out var c) && c != null) return c;
        int n = Mathf.Max(1, (int)(seconds * Rate));
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i, i / (float)Rate), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        cache[name] = clip;
        return clip;
    }

    // ---------- Баллончик ----------

    // Шипение: белый шум без низов, слегка мягче на верхах. Зацикливается.
    public static AudioClip Hiss
    {
        get
        {
            float low = 0f, soft = 0f;
            return Make("hiss", 2f, (i, t) =>
            {
                float w = Rand();
                low += (w - low) * 0.05f;
                soft += ((w - low) - soft) * 0.7f;
                return soft * 0.5f;
            });
        }
    }

    // Пустой баллон: «пшик-щёлк».
    public static AudioClip EmptyClick => Make("empty", 0.17f, (i, t) =>
        Rand() * (Mathf.Exp(-t * 90f) + (t > 0.07f ? 0.5f * Mathf.Exp(-(t - 0.07f) * 120f) : 0f)) * 0.6f);

    // Встряхивание: шарик бьётся о стенки баллона.
    public static AudioClip Rattle => Make("rattle", 0.6f, (i, t) =>
    {
        float s = 0f;
        for (int k = 0; k < 6; k++)
        {
            float hit = k * 0.1f + 0.02f * Mathf.Sin(k * 7f);
            if (t >= hit)
            {
                float dt = t - hit;
                s += Mathf.Sin(dt * 2f * Mathf.PI * (2600f + k * 140f)) * Mathf.Exp(-dt * 70f) * 0.35f;
            }
        }
        return s + Rand() * 0.02f * Mathf.Exp(-t * 4f);
    });

    // Смена баллона: короткий «клац».
    public static AudioClip Swap => Make("swap", 0.12f, (i, t) =>
        (Mathf.Sin(t * 2f * Mathf.PI * 900f) * 0.5f + Rand() * 0.4f) * Mathf.Exp(-t * 45f));

    // ---------- Игрок ----------

    public static AudioClip Footstep(int variant)
    {
        variant = Mathf.Abs(variant) % 4;
        float low = 0f;
        float tone = 90f + variant * 12f;
        return Make("step" + variant, 0.16f, (i, t) =>
        {
            float w = Rand();
            low += (w - low) * (0.15f + variant * 0.03f);
            return (low * 1.6f + Mathf.Sin(t * 2f * Mathf.PI * tone) * 0.4f) * Mathf.Exp(-t * 32f) * 0.7f;
        });
    }

    public static AudioClip Jump => Make("jump", 0.18f, (i, t) =>
    {
        float f = Mathf.Lerp(180f, 420f, t / 0.18f);
        return Mathf.Sin(t * 2f * Mathf.PI * f) * Mathf.Exp(-t * 18f) * 0.25f + Rand() * 0.08f * Mathf.Exp(-t * 30f);
    });

    public static AudioClip Land
    {
        get
        {
            float low = 0f;
            return Make("land", 0.3f, (i, t) =>
            {
                low += (Rand() - low) * 0.08f;
                return (low * 2.2f + Mathf.Sin(t * 2f * Mathf.PI * 60f) * 0.6f) * Mathf.Exp(-t * 16f);
            });
        }
    }

    public static AudioClip Hurt => Make("hurt", 0.4f, (i, t) =>
    {
        float f = Mathf.Lerp(300f, 90f, t / 0.4f);
        return (Mathf.Sin(t * 2f * Mathf.PI * f) * 0.5f + Rand() * 0.25f) * Mathf.Exp(-t * 7f);
    });

    public static AudioClip Whoosh
    {
        get
        {
            float low = 0f;
            return Make("whoosh", 0.7f, (i, t) =>
            {
                low += (Rand() - low) * Mathf.Lerp(0.02f, 0.25f, t / 0.7f);
                return low * 1.5f * Mathf.Sin(Mathf.PI * t / 0.7f);
            });
        }
    }

    // ---------- Игра ----------

    // Подбор: два быстрых «бип» вверх.
    public static AudioClip Pickup => Make("pickup", 0.25f, (i, t) =>
    {
        float f = t < 0.09f ? 880f : 1320f;
        float tt = t < 0.09f ? t : t - 0.09f;
        return Mathf.Sin(t * 2f * Mathf.PI * f) * Mathf.Exp(-tt * 18f) * 0.35f;
    });

    // Тег засчитан: мажорное арпеджио.
    public static AudioClip TagComplete => Make("tag", 1.0f, (i, t) =>
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        float s = 0f;
        for (int k = 0; k < notes.Length; k++)
        {
            float start = k * 0.09f;
            if (t < start) continue;
            float dt = t - start;
            s += (Mathf.Sin(dt * 2f * Mathf.PI * notes[k]) + 0.3f * Mathf.Sin(dt * 4f * Mathf.PI * notes[k])) * Mathf.Exp(-dt * 4f) * 0.18f;
        }
        return s;
    });

    // Тайник краски: булькающее «буль-буль».
    public static AudioClip Refill => Make("refill", 0.6f, (i, t) =>
    {
        float f = 300f + 200f * Mathf.Sin(t * 40f) + t * 600f;
        return Mathf.Sin(t * 2f * Mathf.PI * f) * 0.25f * Mathf.Exp(-t * 3f);
    });

    // Финиш.
    public static AudioClip Finish => Make("finish", 1.8f, (i, t) =>
    {
        float[] notes = { 392f, 523.25f, 659.25f, 783.99f, 1046.5f };
        float s = 0f;
        for (int k = 0; k < notes.Length; k++)
        {
            float start = k * 0.12f;
            if (t < start) continue;
            float dt = t - start;
            s += Mathf.Sin(dt * 2f * Mathf.PI * notes[k]) * Mathf.Exp(-dt * 1.8f) * 0.15f;
        }
        return s;
    });

    // ---------- Атмосфера ----------

    // Гул заброшенного метро: низкий гул + медленно «дышащий» шум. Зацикливается.
    public static AudioClip AmbientDrone
    {
        get
        {
            float low = 0f, low2 = 0f;
            const float len = 8f;
            return Make("drone", len, (i, t) =>
            {
                low += (Rand() - low) * 0.004f;
                low2 += (Rand() - low2) * 0.03f;
                float breath = 0.5f + 0.5f * Mathf.Sin(t / len * 2f * Mathf.PI * 2f);
                float hum = Mathf.Sin(t * 2f * Mathf.PI * 50f) * 0.05f + Mathf.Sin(t * 2f * Mathf.PI * 100f) * 0.02f;
                return low * 6f + low2 * 0.15f * breath + hum;
            });
        }
    }

    // Жужжание лампы дневного света. Зацикливается.
    public static AudioClip LampBuzz => Make("buzz", 1f, (i, t) =>
        (Mathf.Sin(t * 2f * Mathf.PI * 100f) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 200f) * 0.3f + Rand() * 0.05f) * 0.3f);

    // Закрасчик: влажное «чавкающее» качение валика. Зацикливается.
    public static AudioClip EnemyRoll
    {
        get
        {
            float low = 0f;
            const float len = 2f;
            return Make("roll", len, (i, t) =>
            {
                low += (Rand() - low) * 0.06f;
                float squish = 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 3f);
                return low * 2f * squish + Mathf.Sin(t * 2f * Mathf.PI * 55f) * 0.15f;
            });
        }
    }

    // Закрасчик заметил краску: низкое «уррр».
    public static AudioClip EnemyGrowl => Make("growl", 0.8f, (i, t) =>
    {
        float f = 70f + 15f * Mathf.Sin(t * 30f);
        return (Mathf.Sin(t * 2f * Mathf.PI * f) + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * f * 2.01f)) * 0.3f * Mathf.Sin(Mathf.PI * t / 0.8f);
    });

    // Краска стирается: «шшшух» вниз.
    public static AudioClip Erase
    {
        get
        {
            float low = 0f;
            return Make("erase", 0.6f, (i, t) =>
            {
                low += (Rand() - low) * Mathf.Lerp(0.4f, 0.03f, t / 0.6f);
                return low * 0.9f * Mathf.Exp(-t * 3f);
            });
        }
    }

    // ---------- Музыка ----------
    // Лоу-фай хип-хоп бит (boom-bap, 92 BPM, ля минор), 4 такта по кругу.
    // MusicBase — основной слой, MusicDanger — тревожный слой той же длины (вступает при охоте Закрасчика).

    const float Bpm = 92f;
    static float StepLen => 60f / Bpm / 4f; // шестнадцатая
    static int LoopSamples => Mathf.RoundToInt(StepLen * 64 * Rate);

    public static AudioClip MusicBase
    {
        get
        {
            if (cache.TryGetValue("musicBase", out var c) && c != null) return c;
            var buf = new float[LoopSamples];
            var r = new System.Random(92);
            float[] bassNotes = { 55f, 55f, 43.65f, 49f };
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f }, new[] { 220f, 261.63f, 329.63f },
                new[] { 174.61f, 220f, 261.63f }, new[] { 196f, 246.94f, 293.66f },
            };
            for (int step = 0; step < 64; step++)
            {
                int bar = step / 16, pos = step % 16;
                float swing = (pos % 2 == 1) ? StepLen * 0.22f : 0f;
                int at = Mathf.RoundToInt((step * StepLen + swing) * Rate);
                if (pos == 0 || pos == 7 || pos == 10) { Kick(buf, at, 1f); Bass(buf, at, bassNotes[bar], pos == 0 ? 0.42f : 0.3f); }
                if (pos == 4 || pos == 12) Snare(buf, at, 0.55f, r);
                if (pos % 2 == 0) Hat(buf, at, pos == 14 ? 0.12f : 0.07f, pos == 14 ? 18f : 60f, r);
            }
            // Мягкий аккордовый «пэд» и потрескивание пластинки.
            int barLen = LoopSamples / 4;
            for (int i = 0; i < buf.Length; i++)
            {
                int bar = Mathf.Min(3, i / barLen);
                float t = i / (float)Rate;
                float tb = (i - bar * barLen) / (float)Rate;
                float env = Mathf.Clamp01(tb / 0.4f) * Mathf.Clamp01((barLen / (float)Rate - tb) / 0.3f);
                float pad = 0f;
                foreach (float f in chords[bar]) pad += Mathf.Sin(t * 2f * Mathf.PI * f) + 0.3f * Mathf.Sin(t * 4f * Mathf.PI * f);
                buf[i] += pad * 0.022f * env;
                if (r.NextDouble() < 0.0004) buf[i] += (float)(r.NextDouble() - 0.5) * 0.25f;
                buf[i] += (float)(r.NextDouble() - 0.5) * 0.006f;
            }
            return FinishMusic("musicBase", buf);
        }
    }

    public static AudioClip MusicDanger
    {
        get
        {
            if (cache.TryGetValue("musicDanger", out var c) && c != null) return c;
            var buf = new float[LoopSamples];
            var r = new System.Random(13);
            float[] bassNotes = { 55f, 55f, 43.65f, 49f };
            for (int step = 0; step < 64; step++)
            {
                int bar = step / 16, pos = step % 16;
                int at = Mathf.RoundToInt(step * StepLen * Rate);
                Hat(buf, at, 0.05f + (pos % 4 == 2 ? 0.04f : 0f), 70f, r);
                if (pos % 2 == 0) SawPulse(buf, at, bassNotes[bar] * 2f, 0.12f);
                if (pos == 0) Stab(buf, at, 0.12f);
            }
            return FinishMusic("musicDanger", buf);
        }
    }

    static AudioClip FinishMusic(string name, float[] buf)
    {
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)System.Math.Tanh(buf[i] * 1.2f) * 0.8f; // мягкий лимитер
        var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
        clip.SetData(buf, 0);
        cache[name] = clip;
        return clip;
    }

    static void Add(float[] buf, int at, int len, System.Func<float, float> f)
    {
        for (int i = 0; i < len; i++)
        {
            int k = (at + i) % buf.Length; // хвост звука заворачиваем в начало — петля без щелчков
            buf[k] += f(i / (float)Rate);
        }
    }

    static void Kick(float[] buf, int at, float v)
    {
        float phase = 0f;
        Add(buf, at, (int)(0.35f * Rate), t =>
        {
            float freq = 45f + 80f * Mathf.Exp(-t * 30f);
            phase += freq / Rate;
            return Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 9f) * v;
        });
    }

    static void Snare(float[] buf, int at, float v, System.Random r)
    {
        float low = 0f;
        Add(buf, at, (int)(0.25f * Rate), t =>
        {
            float w = (float)(r.NextDouble() * 2 - 1);
            low += (w - low) * 0.3f;
            return ((w - low) * 0.8f * Mathf.Exp(-t * 16f) + Mathf.Sin(t * 2f * Mathf.PI * 185f) * 0.5f * Mathf.Exp(-t * 25f)) * v;
        });
    }

    static void Hat(float[] buf, int at, float v, float decay, System.Random r)
    {
        float low = 0f;
        Add(buf, at, (int)(0.12f * Rate), t =>
        {
            float w = (float)(r.NextDouble() * 2 - 1);
            low += (w - low) * 0.5f;
            return (w - low) * Mathf.Exp(-t * decay) * v;
        });
    }

    static void Bass(float[] buf, int at, float freq, float v)
    {
        Add(buf, at, (int)(0.45f * Rate), t =>
        {
            float x = Mathf.Sin(t * 2f * Mathf.PI * freq) + 0.25f * Mathf.Sin(t * 4f * Mathf.PI * freq);
            return x * Mathf.Exp(-t * 4f) * Mathf.Clamp01(t / 0.01f) * v;
        });
    }

    static void SawPulse(float[] buf, int at, float freq, float v)
    {
        Add(buf, at, (int)(0.14f * Rate), t =>
        {
            float saw = 2f * (t * freq - Mathf.Floor(t * freq + 0.5f));
            return saw * Mathf.Exp(-t * 18f) * v;
        });
    }

    static void Stab(float[] buf, int at, float v)
    {
        Add(buf, at, (int)(0.5f * Rate), t =>
            (Mathf.Sin(t * 2f * Mathf.PI * 466.16f) + Mathf.Sin(t * 2f * Mathf.PI * 440f)) * Mathf.Exp(-t * 6f) * v);
    }

    // ---------- Ещё эффекты ----------

    // Где-то за стеной проходит поезд: нарастающий гул и дребезг.
    public static AudioClip TrainRumble
    {
        get
        {
            float low = 0f, low2 = 0f;
            const float len = 5f;
            return Make("train", len, (i, t) =>
            {
                low += (Rand() - low) * 0.01f;
                low2 += (Rand() - low2) * 0.08f;
                float env = Mathf.Sin(Mathf.PI * t / len);
                float clack = Mathf.Repeat(t, 0.42f) < 0.03f ? 0.4f : 0f;
                return (low * 9f + low2 * 0.3f * clack) * env * env;
            });
        }
    }

    // Подобрал золотую крышку: звонкий колокольчик.
    public static AudioClip Collect => Make("collect", 1.1f, (i, t) =>
    {
        float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
        float s = 0f;
        for (int k = 0; k < notes.Length; k++)
        {
            float start = k * 0.06f;
            if (t < start) continue;
            float dt = t - start;
            s += Mathf.Sin(dt * 2f * Mathf.PI * notes[k]) * Mathf.Exp(-dt * 5f) * 0.14f;
        }
        return s;
    });
}
