using System.Collections.Generic;
using UnityEngine;

// Как выглядит мазок.
public enum StrokeStyle
{
    Flat,    // пол / потолок: плоское граффити с ореолом распыления
    Raised,  // стена: валик густой краски, выступает из стены на depth
    Bridge,  // мост: сплюснутый валик толщиной depth (вниз от верха)
}

// Один мазок: меш вдоль точек.
//  - Flat: плоская лента с текстурой баллончика (мягкие края) + бледный ореол;
//  - Raised / Bridge: объёмный «валик» краски с округлым сечением и скруглёнными концами
//    (освещённый, со слабым свечением), у Raised ещё ореол распыления на стене.
// Коллайдеры (PaintSpot) создаёт PaintSpray и передаёт сюда через AddSegment / AddPad.
// Когда мокрая плита проседает, вершины мазка опускаются вместе с ней.
public class PaintStroke : MonoBehaviour
{
    // Все мазки на уровне (Закрасчик ищет по ним краску).
    public static readonly HashSet<PaintStroke> All = new HashSet<PaintStroke>();
    public bool IsErasing { get; private set; }
    // Старые нарисованные теги (декор уровня) — Закрасчик их не трогает.
    public bool IsDecor { get; private set; }
    // Серый след Закрасчика.
    public bool IsGray { get; set; }
    // Границы мазка (с запасом на ширину) — для быстрой отсечки при поиске краски.
    public Bounds Bounds => bounds;
    Bounds bounds;

    public void MarkDecor() => IsDecor = true;

    public StrokeStyle Style { get; private set; }
    public bool Solid { get; private set; }
    public Color StrokeColor { get; private set; }
    public float Depth { get; private set; }
    public int Count => points.Count;
    public Vector3 LastPoint => points[points.Count - 1];
    public Vector3 LastNormal => normals[normals.Count - 1];
    public float LastWidth => widths[widths.Count - 1];
    // Направление последнего отрезка (ноль, если точка одна).
    public Vector3 LastDirection => points.Count < 2 ? Vector3.zero : (points[points.Count - 1] - points[points.Count - 2]).normalized;

    const float BeadHalfWidth = 0.45f; // полуширина валика (доля ширины мазка)
    const int WallProfile = 11;        // точек в полуовальном сечении валика на стене
    const int BridgeProfile = 18;      // точек в замкнутом сечении моста
    const int TipRings = 5;            // колец в скруглённом конце
    const float WallRoundness = 2.6f;  // 2 — эллипс, больше — «квадратнее»
    const float BridgeRoundness = 4f;

    readonly List<Vector3> points = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<float> widths = new List<float>();
    readonly List<float> times = new List<float>();
    readonly List<PaintSpot> segments = new List<PaintSpot>(); // segments[i] — между точками i и i+1
    readonly List<PaintSpot> pads = new List<PaintSpot>();     // плиты под одиночными точками

    // Буферы меша.
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector3> norms = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> solidTris = new List<int>();
    readonly List<int> faceTris = new List<int>();

    // Данные точек на время сборки (одиночная точка превращается в две близкие).
    Vector3[] P = new Vector3[0], S = new Vector3[0], D = new Vector3[0], N = new Vector3[0];
    float[] V = new float[0], W = new float[0];
    int[] TI = new int[0];
    int count;

    Mesh mesh;
    Material solidMaterial;
    float dryTime;
    float lastChangeTime;

    public void Init(Color color, StrokeStyle style, float depth, bool solid, float dryTime)
    {
        StrokeColor = color;
        Style = style;
        Depth = style == StrokeStyle.Flat ? 0f : depth;
        Solid = solid;
        this.dryTime = dryTime;

        mesh = new Mesh { name = "PaintStroke", subMeshCount = 2 };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = gameObject.AddComponent<MeshRenderer>();
        solidMaterial = new Material(ProtoMaterials.Get(Color.white));
        solidMaterial.EnableKeyword("_EMISSION"); // неоновое свечение — его подхватывает bloom камеры
        // Контур по нормалям (меш мазка в мировых координатах), тонкий и тёмный.
        ProtoMaterials.SetOutline(solidMaterial, 2f, new Color(0.03f, 0.02f, 0.05f), false);
        mr.sharedMaterials = new[] { solidMaterial, PaintTextures.Stroke };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void AddPoint(Vector3 pos, Vector3 normal, float width)
    {
        if (points.Count == 0) bounds = new Bounds(pos, Vector3.zero);
        bounds.Encapsulate(new Bounds(pos, Vector3.one * (width + Depth * 2f + 0.1f)));
        points.Add(pos);
        normals.Add(normal.normalized);
        widths.Add(width);
        times.Add(Time.time);
        lastChangeTime = Time.time;
        Rebuild();
        MaybeAddDrip(points.Count - 1);
    }

    // ---------- Подтёки краски ----------

    class Drip { public Transform t; public Vector3 top; public float length, width, born; }
    readonly List<Drip> drips = new List<Drip>();

    // Иногда с моста или с надписи на стене свисает капля краски.
    void MaybeAddDrip(int i)
    {
        if (Style == StrokeStyle.Flat || Depth < 0.08f) return;
        if (Random.value > (Style == StrokeStyle.Bridge ? 0.3f : 0.2f)) return;

        Vector3 p = points[i], n = normals[i];
        float w = widths[i];
        Vector3 top = Style == StrokeStyle.Bridge
            ? p - n * Depth * 0.9f + Random.insideUnitSphere * w * 0.2f
            : p + n * (Depth * 0.5f) - Vector3.up * (w * 0.35f);

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "Drip";
        go.transform.SetParent(transform, true);
        go.GetComponent<Renderer>().sharedMaterial = solidMaterial;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        drips.Add(new Drip
        {
            t = go.transform,
            top = top,
            length = Random.Range(0.15f, 0.5f) * (Style == StrokeStyle.Bridge ? 1f : 0.7f),
            width = Random.Range(0.06f, 0.1f),
            born = Time.time,
        });
        AnimateDrips();
    }

    // Капля медленно вытягивается вниз и чуть покачивается.
    void AnimateDrips()
    {
        foreach (var d in drips)
        {
            if (d.t == null) continue;
            float k = Mathf.Clamp01((Time.time - d.born) / 1.2f);
            k = 1f - (1f - k) * (1f - k);
            float len = Mathf.Max(0.02f, d.length * k) * eraseShrink;
            float wobble = 1f + 0.08f * Mathf.Sin(Time.time * 3f + d.born * 7f);
            d.t.localScale = new Vector3(d.width * wobble, len, d.width * wobble) * (0.6f + 0.4f * eraseShrink);
            d.t.position = d.top - Vector3.up * (len * 0.5f);
        }
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Ближайшая к pos точка мазка (по точкам, без учёта ширины).
    public Vector3 ClosestPoint(Vector3 pos)
    {
        Vector3 best = points.Count > 0 ? points[0] : transform.position;
        float bestD = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float d = (points[i] - pos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = points[i]; }
        }
        return best;
    }

    // Стереть мазок: он сереет и тает за duration секунд, потом исчезает вместе с коллайдерами.
    public void Erase(float duration = 0.7f)
    {
        if (IsErasing) return;
        IsErasing = true;
        eraseStart = Time.time;
        eraseDuration = duration;
    }

    float eraseStart, eraseDuration;

    public void AddSegment(PaintSpot spot) => segments.Add(spot);
    public void AddPad(PaintSpot spot) => pads.Add(spot);

    void Update()
    {
        if (drips.Count > 0) AnimateDrips();
        if (IsErasing)
        {
            float k = Mathf.Clamp01((Time.time - eraseStart) / eraseDuration);
            StrokeColor = Color.Lerp(StrokeColor, new Color(0.45f, 0.45f, 0.47f), k);
            eraseShrink = 1f - k;
            Rebuild();
            if (k >= 1f)
            {
                foreach (var s in segments) if (s != null) Destroy(s.gameObject);
                foreach (var s in pads) if (s != null) Destroy(s.gameObject);
                Destroy(gameObject);
            }
            return;
        }
        // Пока краска мокрая — обновляем цвет и блеск (дёшево, через материал),
        // а меш перестраиваем только если мокрая плита просела под игроком.
        if (Time.time - lastChangeTime <= dryTime + 0.2f)
        {
            UpdateMaterial();
            float sink = TotalSink();
            if (Mathf.Abs(sink - lastSink) > 0.001f)
            {
                lastSink = sink;
                Rebuild();
            }
        }
    }

    float lastSink;

    float TotalSink()
    {
        if (!Solid) return 0f;
        float s = 0f;
        foreach (var seg in segments) if (seg != null) s += seg.Sunk;
        foreach (var pad in pads) if (pad != null) s += pad.Sunk;
        return s;
    }

    float eraseShrink = 1f; // при стирании мазок «сдувается»

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (solidMaterial != null) Destroy(solidMaterial);
    }

    float Wet(int i) => dryTime > 0f ? Mathf.Clamp01(1f - (Time.time - times[i]) / dryTime) : 0f;

    Color WetColor(int i)
    {
        Color c = Color.Lerp(StrokeColor, Color.white, 0.35f * Wet(i));
        c.a = 1f;
        return c;
    }

    float SinkAt(int i)
    {
        if (!Solid) return 0f;
        float s = 0f;
        if (i - 1 >= 0 && i - 1 < segments.Count && segments[i - 1] != null) s = Mathf.Max(s, segments[i - 1].Sunk);
        if (i < segments.Count && segments[i] != null) s = Mathf.Max(s, segments[i].Sunk);
        if (i == 0) foreach (var pad in pads) if (pad != null) s = Mathf.Max(s, pad.Sunk);
        return s;
    }

    // ---------- Сборка меша ----------

    void Rebuild()
    {
        int n = points.Count;
        verts.Clear(); norms.Clear(); uvs.Clear(); colors.Clear(); solidTris.Clear(); faceTris.Clear();
        if (n == 0) { mesh.Clear(); return; }

        PreparePoints(n);

        if (Style == StrokeStyle.Flat)
        {
            AddSoftStrip(faceTris, 1.9f, -0.003f, 0.22f); // ореол
            AddSoftStrip(faceTris, 1f, 0.001f, 1f);       // само граффити
        }
        else
        {
            if (Style == StrokeStyle.Raised) AddSoftStrip(faceTris, 2.1f, 0f, 0.25f); // ореол на стене
            AddBead();
        }

        mesh.Clear();
        mesh.subMeshCount = 2;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(solidTris, 0);
        mesh.SetTriangles(faceTris, 1);
        mesh.RecalculateBounds();

        UpdateMaterial();
    }

    // Валик: цвет мазка, светлее и глянцевее, пока мокрый.
    void UpdateMaterial()
    {
        if (points.Count == 0) return;
        int last = points.Count - 1;
        Color c = WetColor(last);
        if (IsDecor) c *= 0.75f; // старые теги потускнели
        float wet = Wet(last);
        solidMaterial.color = c;
        solidMaterial.SetColor("_EmissionColor", c * (ProtoMaterials.IsToon ? 1.15f : 0.35f));
        float smooth = Mathf.Lerp(0.3f, 0.85f, wet);
        if (solidMaterial.HasProperty("_Glossiness")) solidMaterial.SetFloat("_Glossiness", smooth); // Built-in
        if (solidMaterial.HasProperty("_Smoothness")) solidMaterial.SetFloat("_Smoothness", smooth); // URP
    }

    void PreparePoints(int n)
    {
        int m = n == 1 ? 2 : n;
        if (P.Length < m)
        {
            int cap = m * 2;
            P = new Vector3[cap]; S = new Vector3[cap]; D = new Vector3[cap]; N = new Vector3[cap];
            V = new float[cap]; W = new float[cap]; TI = new int[cap];
        }

        if (n == 1)
        {
            // Одиночный «пшик»: две почти совпадающие точки — скруглённые концы сделают из них каплю.
            Vector3 d0 = DirectionAt(0);
            float half = widths[0] * 0.05f;
            Vector3 p0 = points[0] + Vector3.down * SinkAt(0);
            for (int k = 0; k < 2; k++)
            {
                P[k] = p0 + d0 * (k == 0 ? -half : half);
                D[k] = d0; N[k] = normals[0]; W[k] = widths[0] * eraseShrink; TI[k] = 0;
            }
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                P[i] = points[i] + Vector3.down * SinkAt(i);
                D[i] = DirectionAt(i); N[i] = normals[i]; W[i] = widths[i] * eraseShrink; TI[i] = i;
            }
        }

        Vector3 lastSide = Vector3.zero;
        for (int i = 0; i < m; i++)
        {
            Vector3 side = Vector3.Cross(N[i], D[i]);
            if (side.sqrMagnitude < 1e-6f) side = lastSide.sqrMagnitude > 0f ? lastSide : Vector3.Cross(N[i], Vector3.forward);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
            S[i] = side.normalized;
            lastSide = S[i];
            V[i] = i == 0 ? 0f : V[i - 1] + Vector3.Distance(P[i - 1], P[i]) / Mathf.Max(0.05f, W[i]);
        }
        count = m;
    }

    // Плоская лента с текстурой баллончика: пары вершин по точкам + прозрачные «хвосты» на концах.
    void AddSoftStrip(List<int> tris, float widthScale, float offset, float alpha)
    {
        int m = count;
        int start = verts.Count;
        for (int k = -1; k <= m; k++)
        {
            int i = Mathf.Clamp(k, 0, m - 1);
            float w = W[i] * widthScale;
            Vector3 c = P[i] + N[i] * offset;
            float a = alpha;
            float v = V[i];
            if (k == -1) { c -= D[i] * w * 0.5f; a = 0f; v -= 0.5f * widthScale; }
            else if (k == m) { c += D[i] * w * 0.5f; a = 0f; v += 0.5f * widthScale; }

            verts.Add(c - S[i] * w * 0.5f);
            verts.Add(c + S[i] * w * 0.5f);
            norms.Add(N[i]); norms.Add(N[i]);
            uvs.Add(new Vector2(0f, v / widthScale));
            uvs.Add(new Vector2(1f, v / widthScale));
            Color col = StrokeColor; // мокрый оттенок — только у материала валика, ленту не перестраиваем
            col.a = a;
            colors.Add(col); colors.Add(col);
        }

        for (int p = 0; p < m + 1; p++)
        {
            int a = start + p * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }
    }

    // Объёмный валик: кольца сечения вдоль мазка + скруглённые концы.
    void AddBead()
    {
        int m = count;
        bool bridge = Style == StrokeStyle.Bridge;
        int profile = bridge ? BridgeProfile : WallProfile;
        int start = verts.Count;
        int rings = 0;

        // Начало: кольца от острия к полному сечению.
        for (int j = TipRings; j >= 1; j--) { AddRing(0, j, -1f, bridge, profile); rings++; }
        for (int i = 0; i < m; i++) { AddRing(i, 0, 0f, bridge, profile); rings++; }
        // Конец: от полного сечения к острию.
        for (int j = 1; j <= TipRings; j++) { AddRing(m - 1, j, 1f, bridge, profile); rings++; }

        // Треугольники между соседними кольцами — наружу (нормали заданы вручную).
        int edges = bridge ? profile : profile - 1;
        for (int r = 0; r < rings - 1; r++)
        {
            int ring0 = start + r * profile, ring1 = ring0 + profile;
            for (int k = 0; k < edges; k++)
            {
                int k1 = (k + 1) % profile;
                int a = ring0 + k, b = ring0 + k1, c = ring1 + k, d = ring1 + k1;
                solidTris.Add(a); solidTris.Add(b); solidTris.Add(c);
                solidTris.Add(b); solidTris.Add(d); solidTris.Add(c);
            }
        }
    }

    // Одно кольцо сечения у точки i. tipStep > 0 — кольцо скруглённого конца (сжато и сдвинуто по dirSign).
    void AddRing(int i, int tipStep, float dirSign, bool bridge, int profile)
    {
        float h = W[i] * BeadHalfWidth;
        float scale = 1f, along = 0f, blendCos = 1f, blendSin = 0f;
        if (tipStep > 0)
        {
            float phi = tipStep / (float)TipRings * Mathf.PI * 0.5f;
            float tipLength = bridge ? h : Mathf.Max(h, Depth * 0.6f); // у валика на стене кончик подлиннее
            scale = Mathf.Cos(phi);
            along = Mathf.Sin(phi) * tipLength * dirSign;
            blendCos = Mathf.Cos(phi);
            blendSin = Mathf.Sin(phi) * dirSign;
        }

        Vector3 dir = D[i], side = S[i], up = N[i];
        float roundness = bridge ? BridgeRoundness : WallRoundness;
        float halfH = (bridge ? Depth * 0.5f : Depth) * eraseShrink;
        // Центр сечения: у моста — середина толщины, у валика — чуть «утоплен» в стену, чтобы не было щели.
        Vector3 center = P[i] + dir * along + (bridge ? -up * halfH : -up * 0.01f);

        for (int k = 0; k < profile; k++)
        {
            float theta = bridge
                ? k / (float)profile * Mathf.PI * 2f
                : k / (float)(profile - 1) * Mathf.PI;
            float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
            float u = SignPow(c, 2f / roundness), v = SignPow(s, 2f / roundness);

            Vector3 pos = center + side * (u * h * scale) + up * (v * halfH * scale);

            // Нормаль суперэллипса + наклон вдоль мазка на концах.
            Vector3 nrm = side * (SignPow(u, roundness - 1f) / Mathf.Max(h, 1e-3f))
                        + up * (SignPow(v, roundness - 1f) / Mathf.Max(halfH, 1e-3f));
            nrm = nrm.sqrMagnitude > 1e-8f ? nrm.normalized : up;
            nrm = (nrm * blendCos + dir * blendSin).normalized;

            verts.Add(pos);
            norms.Add(nrm);
            uvs.Add(new Vector2(0.5f, 0.5f));
            colors.Add(Color.white);
        }
    }

    static float SignPow(float x, float p) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), p);

    Vector3 DirectionAt(int i)
    {
        int n = points.Count;
        if (n < 2)
        {
            // Одиночная точка: любое направление в плоскости поверхности.
            Vector3 d0 = Vector3.ProjectOnPlane(Vector3.up, normals[i]);
            if (d0.sqrMagnitude < 1e-4f) d0 = Vector3.ProjectOnPlane(Vector3.forward, normals[i]);
            return d0.normalized;
        }
        Vector3 d = points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)];
        return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
    }
}
