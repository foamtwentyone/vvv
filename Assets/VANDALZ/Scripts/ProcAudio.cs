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
}
