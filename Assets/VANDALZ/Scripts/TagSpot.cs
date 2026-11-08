using System;
using System.Collections.Generic;
using UnityEngine;

// Спот для граффити: светящаяся пунктирная рамка на стене.
// Закрась её обычным баллоном на 70% — тег засчитан (звук, вспышка, +1 к счёту).
// Объект смотрит «из стены»: transform.forward — нормаль стены, рамка в плоскости XY.
public class TagSpot : MonoBehaviour
{
    public static readonly List<TagSpot> All = new List<TagSpot>();
    public static event Action<TagSpot> TagCompleted;

    public string title = "ТЕГ";
    public Vector2 size = new Vector2(2.4f, 1.4f);
    [Range(0f, 1f)] public float required = 0.7f;

    public bool Completed { get; private set; }
    public float Coverage01 => total > 0 ? covered / (float)total : 0f;
    // Насколько засчитано: 1 — тег готов.
    public float Progress01 => Mathf.Clamp01(Coverage01 / required);

    const int GridX = 12, GridY = 7;
    readonly bool[,] cells = new bool[GridX, GridY];
    int covered, total = GridX * GridY;

    LineRenderer frame;
    Material frameMaterial;
    TextMesh label;
    ParticleSystem burst;
    Light glow;

    static readonly Color Waiting = new Color(1f, 1f, 1f, 0.9f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.2f, 1f);

    public static TagSpot Create(Transform parent, Vector3 center, Vector3 wallNormal, Vector2 size, string title)
    {
        var go = new GameObject("TagSpot: " + title);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(wallNormal, Vector3.up));
        var spot = go.AddComponent<TagSpot>();
        spot.size = size;
        spot.title = title;
        spot.Build();
        return spot;
    }

    void OnEnable()
    {
        All.Add(this);
        PaintSpray.PaintApplied += OnPaint;
    }

    void OnDisable()
    {
        All.Remove(this);
        PaintSpray.PaintApplied -= OnPaint;
    }

    void Build()
    {
        // Пунктирная рамка.
        frame = gameObject.AddComponent<LineRenderer>();
        frameMaterial = PaintTextures.MakeUnlit(MakeDashTexture());
        frame.sharedMaterial = frameMaterial;
        frame.textureMode = LineTextureMode.Tile;
        frame.useWorldSpace = false;
        frame.loop = true;
        frame.widthMultiplier = 0.06f;
        frame.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        frame.receiveShadows = false;
        float hx = size.x * 0.5f, hy = size.y * 0.5f, z = 0.04f;
        frame.positionCount = 4;
        frame.SetPositions(new[] { new Vector3(-hx, -hy, z), new Vector3(-hx, hy, z), new Vector3(hx, hy, z), new Vector3(hx, -hy, z) });

        // Уголки-скобки поверх пунктира.
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1f : 1f, sy = i < 2 ? -1f : 1f;
            var corner = new GameObject("Corner").AddComponent<LineRenderer>();
            corner.transform.SetParent(transform, false);
            corner.sharedMaterial = PaintTextures.MakeUnlit(null);
            corner.useWorldSpace = false;
            corner.widthMultiplier = 0.09f;
            corner.startColor = corner.endColor = Waiting;
            corner.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            corner.positionCount = 3;
            corner.SetPositions(new[]
            {
                new Vector3(sx * hx, sy * (hy - 0.3f), z),
                new Vector3(sx * hx, sy * hy, z),
                new Vector3(sx * (hx - 0.3f), sy * hy, z),
            });
        }

        label = WorldText.Create(transform, title, new Vector3(0f, hy + 0.28f, 0.05f), Quaternion.Euler(0f, 180f, 0f), 0.3f, Waiting);

        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0f, 0.8f);
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 3.5f;
        glow.intensity = 0.6f;
        glow.color = new Color(0.8f, 0.9f, 1f);

        // Конфетти при засчитанном теге.
        var burstGo = new GameObject("Burst");
        burstGo.transform.SetParent(transform, false);
        burstGo.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        burst = burstGo.AddComponent<ParticleSystem>();
        var main = burst.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 1.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(Gold, new Color(1f, 0.3f, 0.7f));
        var emission = burst.emission;
        emission.enabled = false;
        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(size.x, size.y, 0.1f);
        var r = burstGo.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = PaintTextures.Particle;
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnPaint(Vector3 pos, Vector3 normal, float width, SprayMode kind)
    {
        if (Completed || (kind != SprayMode.Wall && kind != SprayMode.Surface)) return;
        if (Vector3.Dot(normal, transform.forward) < 0.7f) return;

        Vector3 local = transform.InverseTransformPoint(pos);
        if (Mathf.Abs(local.z) > 0.4f) return;
        float hx = size.x * 0.5f, hy = size.y * 0.5f;
        float radius = width * 0.6f;
        if (local.x < -hx - radius || local.x > hx + radius || local.y < -hy - radius || local.y > hy + radius) return;

        for (int ix = 0; ix < GridX; ix++)
            for (int iy = 0; iy < GridY; iy++)
            {
                if (cells[ix, iy]) continue;
                float cx = -hx + (ix + 0.5f) * size.x / GridX;
                float cy = -hy + (iy + 0.5f) * size.y / GridY;
                float dx = cx - local.x, dy = cy - local.y;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    cells[ix, iy] = true;
                    covered++;
                }
            }

        if (Coverage01 >= required) Complete();
    }

    void Complete()
    {
        Completed = true;
        burst.Emit(80);
        ProcAudio.PlayAt(ProcAudio.TagComplete, transform.position, 1f);
        if (label != null) label.text = "ГОТОВО: " + title;
        glow.color = Gold;
        glow.intensity = 1.6f;
        TagCompleted?.Invoke(this);
    }

    void Update()
    {
        // Пунктир «бежит» по рамке, пока тег не засчитан; цвет — от белого к золоту по прогрессу.
        float p = Progress01;
        Color c = Completed ? Gold : Color.Lerp(Waiting, Gold, p * 0.6f);
        float pulse = Completed ? 1f : 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
        c.a *= pulse;
        frame.startColor = frame.endColor = c;
        frameMaterial.mainTextureOffset = new Vector2(Completed ? 0f : -Time.time * 0.8f, 0f);
        if (label != null && !Completed) label.text = title + (p > 0f ? $"  {Mathf.RoundToInt(p * 100f)}%" : "");
    }

    static Texture2D MakeDashTexture()
    {
        var tex = new Texture2D(32, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 4; y++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, x < 20 ? 1f : 0f));
        tex.Apply();
        return tex;
    }

    // Сбросить прогресс (рестарт уровня).
    public void ResetTag()
    {
        Array.Clear(cells, 0, cells.Length);
        covered = 0;
        Completed = false;
        glow.color = new Color(0.8f, 0.9f, 1f);
        glow.intensity = 0.6f;
    }
}
