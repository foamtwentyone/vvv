using UnityEngine;

// Пост-обработка камеры (встроенный конвейер): свечение неоновой краски и ламп (bloom),
// тёплая цветокоррекция, контраст, виньетка, лёгкое зерно — «рисованная» картинка.
// Если шейдер не найден или не поддерживается — просто ничего не делает.
[RequireComponent(typeof(Camera))]
public class VandalzPost : MonoBehaviour
{
    [Header("Свечение")]
    public float threshold = 0.9f;
    [Range(0f, 1f)] public float softKnee = 0.6f;
    public float intensity = 1.1f;
    [Range(1, 7)] public int iterations = 6;

    [Header("Цвет")]
    public Color tint = new Color(1.05f, 0.98f, 0.92f);
    public float contrast = 1.12f;
    public float saturation = 1.15f;
    public float vignette = 1.6f;
    public float grain = 0.035f;

    Material material;
    readonly RenderTexture[] chain = new RenderTexture[8];

    void Awake()
    {
        var shader = Shader.Find("Hidden/VANDALZ/Post");
        if (shader == null) shader = Resources.Load<Shader>("VandalzPost");
        if (shader != null && shader.isSupported) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        GetComponent<Camera>().allowHDR = true; // яркость > 1 нужна для свечения
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetFloat("_Threshold", threshold);
        material.SetFloat("_SoftKnee", softKnee);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_Contrast", contrast);
        material.SetFloat("_Saturation", saturation);
        material.SetFloat("_Vignette", vignette);
        material.SetFloat("_Grain", grain);
        material.SetColor("_Tint", tint);

        RenderTextureFormat format = source.format;
        int w = source.width / 2, h = source.height / 2;
        RenderTexture current = chain[0] = RenderTexture.GetTemporary(w, h, 0, format);
        Graphics.Blit(source, current, material, 0);

        int count = 1;
        for (; count < iterations; count++)
        {
            w /= 2;
            h /= 2;
            if (w < 2 || h < 2) break;
            chain[count] = RenderTexture.GetTemporary(w, h, 0, format);
            Graphics.Blit(current, chain[count], material, 1);
            current = chain[count];
        }

        for (int i = count - 2; i >= 0; i--)
        {
            Graphics.Blit(current, chain[i], material, 2); // складываем с более крупным уровнем
            RenderTexture.ReleaseTemporary(current);
            chain[i + 1] = null;
            current = chain[i];
        }

        material.SetTexture("_BloomTex", current);
        Graphics.Blit(source, destination, material, 3);
        RenderTexture.ReleaseTemporary(current);
        chain[0] = null;
    }
}
