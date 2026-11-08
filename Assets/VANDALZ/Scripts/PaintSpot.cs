using UnityEngine;

// Твёрдый кусок краски: плита моста (невидимый коллайдер под лентой) или уступ на стене.
// Свежая краска мокрая:
//  - пока мокрая, проседает под игроком (Press вызывается, когда игрок на ней стоит);
//  - через dryTime секунд застывает и больше не двигается.
// Если у куска есть свой Renderer (уступ), он ещё и светлеет, пока мокрый.
// По этому компоненту позже Закрасчик будет находить краску.
public class PaintSpot : MonoBehaviour
{
    public float dryTime = 2.5f;
    public float sinkSpeed = 0.6f;
    public float maxSink = 0.7f;

    public float Wetness { get; private set; } = 1f;
    public bool IsWet => Wetness > 0f;
    public float Sunk => sunk;
    // Мазок, которому принадлежит плита (null — у уступов на стене).
    public PaintStroke Stroke { get; set; }

    static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP

    Color baseColor;
    Renderer rend;
    Collider col;
    MaterialPropertyBlock block;
    float sunk;

    public void Init(Color color, float dryTime, float sinkSpeed, float maxSink)
    {
        baseColor = color;
        this.dryTime = dryTime;
        this.sinkSpeed = sinkSpeed;
        this.maxSink = maxSink;
        rend = GetComponent<Renderer>();
        col = GetComponent<Collider>();
        if (rend != null) block = new MaterialPropertyBlock();
        Wetness = 1f;
        ApplyColor();
    }

    void Update()
    {
        Wetness = dryTime > 0f ? Mathf.Max(0f, Wetness - Time.deltaTime / dryTime) : 0f;
        ApplyColor();
        if (Wetness <= 0f) enabled = false; // застыла — больше не обновляем
    }

    // Игрок стоит на краске: мокрая краска проседает, тем медленнее, чем суше.
    public void Press(float dt)
    {
        if (Wetness <= 0f || sunk >= maxSink || col == null) return;
        float d = Mathf.Min(sinkSpeed * Wetness * dt, maxSink - sunk);

        // Не проседаем сквозь что-то твёрдое под краской.
        Bounds b = col.bounds;
        if (Physics.Raycast(b.center, Vector3.down, b.extents.y + d + 0.02f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return;

        sunk += d;
        transform.position += Vector3.down * d;
    }

    void ApplyColor()
    {
        if (rend == null) return;
        Color c = Color.Lerp(baseColor, Color.white, 0.45f * Wetness);
        block.SetColor(ColorId, c);
        block.SetColor(BaseColorId, c);
        rend.SetPropertyBlock(block);
    }
}
