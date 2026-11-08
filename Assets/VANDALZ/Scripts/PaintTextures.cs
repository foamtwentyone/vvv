using UnityEngine;

// Текстуры и материалы краски, сгенерированные кодом (файлы картинок не нужны).
// Шейдер "Sprites/Default": без освещения, с прозрачностью и цветом вершин,
// рисует обе стороны. Есть и в Built-in, и в URP.
public static class PaintTextures
{
    static Material strokeMaterial, blobMaterial, particleMaterial;

    // Полоса баллончика: плотная середина, рваные «распылённые» края. Повторяется вдоль мазка.
    public static Material Stroke => strokeMaterial != null ? strokeMaterial : (strokeMaterial = MakeUnlit(MakeStrokeTexture()));

    // Пятно-клякса с неровным краем — для подсказки.
    public static Material Blob => blobMaterial != null ? blobMaterial : (blobMaterial = MakeUnlit(MakeBlobTexture()));

    // Мягкая точка — для частиц.
    public static Material Particle => particleMaterial != null ? particleMaterial : (particleMaterial = MakeUnlit(MakeSoftDot()));

    public static Material MakeUnlit(Texture texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        var mat = shader != null ? new Material(shader) : new Material(ProtoMaterials.Get(Color.white));
        if (texture != null) mat.mainTexture = texture;
        return mat;
    }

    static Texture2D MakeStrokeTexture()
    {
        // U — поперёк мазка (0..1), V — вдоль (повторяется).
        const int w = 64, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
        {
            wrapModeU = TextureWrapMode.Clamp,
            wrapModeV = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        var rng = new System.Random(21);
        for (int y = 0; y < h; y++)
        {
            // Край мазка чуть «гуляет» вдоль длины (плавно и бесшовно по V).
            float wobble = 0.06f * Mathf.Sin(y * Mathf.PI * 2f / h * 3f) + 0.03f * Mathf.Sin(y * Mathf.PI * 2f / h * 7f);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float d = Mathf.Abs(u - 0.5f) * 2f + wobble; // 0 в центре, 1 на краю
                float core = 1f - Smooth(0.5f, 0.85f, d);
                // Брызги у края.
                float r = (float)rng.NextDouble();
                float speckle = d > 0.55f && d < 1.05f && r < (1.05f - d) * 0.45f ? 0.85f : 0f;
                float a = Mathf.Max(core, speckle);
                float shade = 1f - (float)rng.NextDouble() * 0.12f; // лёгкая зернистость
                tex.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeBlobTexture()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var rng = new System.Random(7);
        float half = s * 0.5f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 v = new Vector2(x + 0.5f - half, y + 0.5f - half) / half;
            float ang = Mathf.Atan2(v.y, v.x);
            float edge = 0.78f + 0.07f * Mathf.Sin(ang * 5f) + 0.04f * Mathf.Sin(ang * 11f + 1f);
            float d = v.magnitude / edge;
            float core = 1f - Smooth(0.75f, 1f, d);
            float speckle = d > 0.9f && d < 1.25f && rng.NextDouble() < (1.25f - d) * 0.5f ? 0.8f : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(core, speckle)));
        }
        tex.Apply();
        return tex;
    }

    public static Texture2D MakeSoftDot()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
            float a = Mathf.Clamp01(1f - d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        return tex;
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }
}
