using System.Collections.Generic;
using UnityEngine;

// Процедурные текстуры и материалы окружения: кафель метро, бетон, тактильная плитка у края,
// шпалы, ржавчина, старые афиши. Всё генерируется кодом при первом обращении.
public static class EnvTextures
{
    static Texture2D tiles, concrete, darkConcrete, tactile, rust, poster, wood;
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    // Материал с текстурой. tiling — сколько повторов текстуры на объект (подбирается под размер).
    public static Material Make(string key, Texture2D tex, Color tint, Vector2 tiling, float smoothness = 0.2f)
    {
        string id = key + tiling.x.ToString("0.0") + "x" + tiling.y.ToString("0.0");
        if (materials.TryGetValue(id, out var m) && m != null) return m;
        m = new Material(ProtoMaterials.Get(Color.white)) { color = tint, mainTexture = tex, mainTextureScale = tiling };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);          // URP
        if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        materials[id] = m;
        return m;
    }

    public static Texture2D Tiles => tiles != null ? tiles : (tiles = MakeTiles());
    public static Texture2D Concrete => concrete != null ? concrete : (concrete = MakeConcrete(0.62f, 0.12f, 11));
    public static Texture2D DarkConcrete => darkConcrete != null ? darkConcrete : (darkConcrete = MakeConcrete(0.3f, 0.1f, 23));
    public static Texture2D Tactile => tactile != null ? tactile : (tactile = MakeTactile());
    public static Texture2D Rust => rust != null ? rust : (rust = MakeRust());
    public static Texture2D Wood => wood != null ? wood : (wood = MakeWood());
    public static Texture2D Poster => poster != null ? poster : (poster = MakePoster());

    // ---------- Генерация ----------

    // Сглаженный «шум значений»: плавные пятна грязи, бесшовный на заданном периоде.
    static float ValueNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
        float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Hash(int x, int y, int period, int seed)
    {
        x = ((x % period) + period) % period;
        y = ((y % period) + period) % period;
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 2147483647;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    // Многооктавный шум, бесшовный на размере текстуры.
    static float Fbm(int px, int py, int size, int seed, int baseCells)
    {
        float sum = 0f, amp = 0.5f;
        int cells = baseCells;
        for (int o = 0; o < 4; o++)
        {
            sum += amp * ValueNoise(px * cells / (float)size, py * cells / (float)size, cells, seed + o * 31);
            cells *= 2;
            amp *= 0.5f;
        }
        return sum / 0.9375f;
    }

    static Texture2D NewTex(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
        };
    }

    // Кафель метро: прямоугольная плитка 2:1 со смещением рядов, тёмные швы, грязь и сколы.
    static Texture2D MakeTiles()
    {
        const int size = 256;
        var tex = NewTex(size);
        var r = new System.Random(5);
        const int cols = 4, rows = 8; // плиток на текстуру
        float tileW = size / (float)cols, tileH = size / (float)rows;
        var tileShade = new float[cols * 2, rows];
        for (int i = 0; i < cols * 2; i++)
            for (int j = 0; j < rows; j++)
                tileShade[i, j] = 0.86f + (float)r.NextDouble() * 0.12f;

        for (int y = 0; y < size; y++)
        {
            int row = (int)(y / tileH);
            float offset = (row % 2) * tileW * 0.5f;
            for (int x = 0; x < size; x++)
            {
                float fx = (x + offset) % size;
                int col = (int)(fx / tileW);
                float lx = fx - col * tileW, ly = y - row * tileH;
                float edge = Mathf.Min(Mathf.Min(lx, tileW - lx), Mathf.Min(ly, tileH - ly));

                float dirt = Fbm(x, y, size, 3, 4);
                float grime = Mathf.Clamp01((dirt - 0.45f) * 2.2f);
                Color tile = new Color(0.92f, 0.93f, 0.86f) * tileShade[col % (cols * 2), row % rows];
                // Лёгкая выпуклость края плитки.
                if (edge < 3f) tile *= 0.9f + 0.1f * edge / 3f;
                Color grout = new Color(0.22f, 0.22f, 0.2f);
                Color c = edge < 1.4f ? grout : tile;
                c = Color.Lerp(c, new Color(0.35f, 0.33f, 0.26f), grime * 0.55f);
                // Редкие сколы.
                if (edge >= 1.4f && Fbm(x, y, size, 77, 16) > 0.8f) c *= 0.6f;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeConcrete(float baseValue, float variation, int seed)
    {
        const int size = 256;
        var tex = NewTex(size);
        var r = new System.Random(seed);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Fbm(x, y, size, seed, 4);
                float fine = (float)r.NextDouble();
                float v = baseValue + (n - 0.5f) * variation * 2f + (fine - 0.5f) * 0.05f;
                // Тёмные крапинки и трещинки.
                if (fine > 0.985f) v *= 0.7f;
                float crack = Mathf.Abs(Fbm(x, y, size, seed + 5, 8) - 0.5f);
                if (crack < 0.008f) v *= 0.75f;
                tex.SetPixel(x, y, new Color(v, v * 0.99f, v * 0.96f, 1f));
            }
        tex.Apply();
        return tex;
    }

    // Жёлтая тактильная полоса с выпуклыми точками.
    static Texture2D MakeTactile()
    {
        const int size = 128;
        var tex = NewTex(size);
        const int dots = 8;
        float cell = size / (float)dots;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = (x % cell) - cell / 2f, cy = (y % cell) - cell / 2f;
                float d = Mathf.Sqrt(cx * cx + cy * cy) / (cell * 0.32f);
                float n = Fbm(x, y, size, 9, 4);
                Color c = new Color(0.95f, 0.75f, 0.1f) * (0.85f + n * 0.2f);
                if (d < 1f) c *= 1.1f - d * 0.25f;          // выпуклая точка
                else if (d < 1.15f) c *= 0.75f;            // тень вокруг
                c = Color.Lerp(c, new Color(0.3f, 0.27f, 0.2f), Mathf.Clamp01((n - 0.6f) * 2f) * 0.5f);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeRust()
    {
        const int size = 128;
        var tex = NewTex(size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Fbm(x, y, size, 41, 4);
                Color c = Color.Lerp(new Color(0.25f, 0.13f, 0.07f), new Color(0.55f, 0.3f, 0.15f), n);
                if (Fbm(x, y, size, 42, 8) > 0.7f) c = Color.Lerp(c, new Color(0.4f, 0.4f, 0.42f), 0.5f);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeWood()
    {
        const int size = 128;
        var tex = NewTex(size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.Sin((x + Fbm(x, y, size, 51, 4) * 30f) * 0.35f) * 0.5f + 0.5f;
                Color c = Color.Lerp(new Color(0.22f, 0.14f, 0.08f), new Color(0.38f, 0.25f, 0.14f), grain);
                c *= 0.8f + Fbm(x, y, size, 52, 8) * 0.3f;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }

    // Выцветшая афиша: цветные плашки, «текст» полосками, потёки и порванные края.
    static Texture2D MakePoster()
    {
        const int w = 128, h = 192;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        Color bg = new Color(0.75f, 0.35f, 0.25f);
        Color accent = new Color(0.15f, 0.35f, 0.55f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = bg;
                float cx = x - w * 0.5f, cy = y - h * 0.62f;
                if (cx * cx + cy * cy < 30f * 30f) c = new Color(0.95f, 0.85f, 0.4f);           // «солнце»
                if (y < h * 0.35f && y > h * 0.1f && (y / 7) % 2 == 0 && x > 12 && x < w - 12 - (y % 5) * 6) c = accent; // «текст»
                float n = Fbm(x, y, 128, 61, 4);
                c = Color.Lerp(c, new Color(0.85f, 0.82f, 0.72f), 0.35f + n * 0.3f);            // выцвело
                if (Fbm(x, y, 128, 62, 8) > 0.72f) c *= 0.55f;                                   // грязь
                float tear = Fbm(x, y, 128, 63, 4);
                float a = (x < 3 || x > w - 4 || y < 3 || y > h - 4) ? 0f : 1f;
                if (y > h - 30 && tear > 0.62f) a = 0f;                                           // оторванный угол
                c.a = a;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }
}
