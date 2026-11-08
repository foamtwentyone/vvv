using System.Collections.Generic;
using UnityEngine;

// Как выглядит мазок.
public enum StrokeStyle
{
    Flat,    // пол / потолок: плоское граффити с ореолом распыления
    Raised,  // стена: граффити выступает из стены на depth (объёмные буквы)
    Bridge,  // мост: лента в воздухе толщиной depth (вниз от верха)
}

// Один мазок: меш вдоль точек.
//  - «лицо» мазка (верх моста / передняя грань на стене / граффити на полу) — прозрачная текстура баллончика
//    с мягкими краями (submesh 1, материал PaintTextures.Stroke);
//  - бока, торцы и низ — непрозрачные и освещённые (submesh 0, свой материал у каждого мазка);
//  - вокруг граффити на стене и полу — бледный ореол распыления.
// Коллайдеры (PaintSpot) создаёт PaintSpray и передаёт сюда через AddSegment / AddPad.
// Когда мокрая плита проседает, вершины мазка опускаются вместе с ней.
public class PaintStroke : MonoBehaviour
{
    public StrokeStyle Style { get; private set; }
    public bool Solid { get; private set; }
    public Color StrokeColor { get; private set; }
    public float Depth { get; private set; }
    public int Count => points.Count;
    public Vector3 LastPoint => points[points.Count - 1];

    const float SolidFraction = 0.42f; // полуширина твёрдой части (доля ширины); края лица мягко свисают

    readonly List<Vector3> points = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<float> widths = new List<float>();
    readonly List<float> times = new List<float>();
    readonly List<PaintSpot> segments = new List<PaintSpot>(); // segments[i] — между точками i и i+1
    readonly List<PaintSpot> pads = new List<PaintSpot>();     // плиты под одиночными точками

    // Буферы меша.
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<Color> colors = new List<Color>();
    readonly List<Vector3> norms = new List<Vector3>();
    readonly List<int> solidTris = new List<int>();
    readonly List<int> faceTris = new List<int>();

    // Данные точек на время сборки.
    Vector3[] P = new Vector3[0], S = new Vector3[0], D = new Vector3[0], N = new Vector3[0];
    float[] V = new float[0], W = new float[0];
    int[] TI = new int[0]; // индекс исходной точки (для времени высыхания)
    int count;            // сколько точек в массивах выше

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
        mr.sharedMaterials = new[] { solidMaterial, PaintTextures.Stroke };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void AddPoint(Vector3 pos, Vector3 normal, float width)
    {
        points.Add(pos);
        normals.Add(normal.normalized);
        widths.Add(width);
        times.Add(Time.time);
        lastChangeTime = Time.time;
        Rebuild();
    }

    public void AddSegment(PaintSpot spot) => segments.Add(spot);
    public void AddPad(PaintSpot spot) => pads.Add(spot);

    void Update()
    {
        // Пока краска мокрая — обновляем цвет и прогиб. Потом мазок статичен.
        if (Time.time - lastChangeTime <= dryTime + 0.2f) Rebuild();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (solidMaterial != null) Destroy(solidMaterial);
    }

    float Wet(int i) => dryTime > 0f ? Mathf.Clamp01(1f - (Time.time - times[i]) / dryTime) : 0f;

    Color WetColor(int i, float shade)
    {
        Color c = StrokeColor * shade;
        c = Color.Lerp(c, Color.white, 0.45f * Wet(i));
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

    void Rebuild()
    {
        int n = points.Count;
        verts.Clear(); uvs.Clear(); colors.Clear(); norms.Clear(); solidTris.Clear(); faceTris.Clear();
        if (n == 0) { mesh.Clear(); return; }

        // Одиночная точка («пшик») превращается в короткий отрезок, чтобы у неё были бока.
        int m = n == 1 ? 2 : n;
        if (P.Length < m)
        {
            int cap = m * 2;
            P = new Vector3[cap]; S = new Vector3[cap]; D = new Vector3[cap]; N = new Vector3[cap];
            V = new float[cap]; W = new float[cap]; TI = new int[cap];
        }

        if (n == 1)
        {
            Vector3 d0 = DirectionAt(0);
            float half = widths[0] * 0.3f;
            Vector3 p0 = points[0] + Vector3.down * SinkAt(0);
            for (int k = 0; k < 2; k++)
            {
                P[k] = p0 + d0 * (k == 0 ? -half : half);
                D[k] = d0; N[k] = normals[0]; W[k] = widths[0]; TI[k] = 0;
            }
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                P[i] = points[i] + Vector3.down * SinkAt(i);
                D[i] = DirectionAt(i); N[i] = normals[i]; W[i] = widths[i]; TI[i] = i;
            }
        }

        // «Бок» и координата V вдоль мазка для каждой точки.
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

        // Плоскость «лица» и «спины» мазка.
        // Raised: спина на стене, лицо выступает на Depth. Bridge: лицо — верх, спина ниже на Depth.
        float faceOffset = Style == StrokeStyle.Raised ? Depth : 0f;
        float backOffset = Style == StrokeStyle.Bridge ? -Depth : 0f;

        // 1) Ореол распыления на поверхности (стена / пол).
        if (Style != StrokeStyle.Bridge)
            AddSoftStrip(faceTris, 1.9f, Style == StrokeStyle.Flat ? -0.003f : 0f, 0.22f);

        // 2) Твёрдые бока, торцы, низ.
        if (Style != StrokeStyle.Flat)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int start = verts.Count;
                for (int i = 0; i < m; i++)
                {
                    float h = W[i] * SolidFraction;
                    Vector3 off = S[i] * (sign * h);
                    verts.Add(P[i] + N[i] * backOffset + off);
                    verts.Add(P[i] + N[i] * faceOffset + off);
                    uvs.Add(new Vector2(0.5f, V[i])); uvs.Add(new Vector2(0.5f, V[i]));
                    colors.Add(Color.white); colors.Add(Color.white);
                    norms.Add(S[i] * sign); norms.Add(S[i] * sign);
                }
                StripTris(solidTris, start, m);
            }

            // Торцы.
            AddCap(0, faceOffset, backOffset, -D[0]);
            AddCap(m - 1, faceOffset, backOffset, D[m - 1]);

            // Низ моста.
            if (Style == StrokeStyle.Bridge)
            {
                int start = verts.Count;
                for (int i = 0; i < m; i++)
                {
                    float h = W[i] * SolidFraction;
                    Vector3 c = P[i] + N[i] * backOffset;
                    verts.Add(c - S[i] * h); verts.Add(c + S[i] * h);
                    uvs.Add(new Vector2(0.5f, V[i])); uvs.Add(new Vector2(0.5f, V[i]));
                    colors.Add(Color.white); colors.Add(Color.white);
                    norms.Add(-N[i]); norms.Add(-N[i]);
                }
                StripTris(solidTris, start, m);
            }
        }

        // 3) Лицо мазка — текстура баллончика с мягкими краями и концами.
        AddSoftStrip(faceTris, 1f, faceOffset + (Style == StrokeStyle.Bridge ? 0.002f : 0.001f), 1f);

        mesh.Clear();
        mesh.subMeshCount = 2;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetNormals(norms);
        mesh.SetTriangles(solidTris, 0);
        mesh.SetTriangles(faceTris, 1);
        mesh.RecalculateBounds();

        // Цвет твёрдой части: темнее граффити, светлее пока мокрая.
        solidMaterial.color = WetColor(TI[m - 1], 0.7f);
    }

    // Мягкая лента: пары вершин по точкам + прозрачные «хвосты» на концах.
    void AddSoftStrip(List<int> tris, float widthScale, float offset, float alpha)
    {
        int n = count;
        int start = verts.Count;
        for (int k = -1; k <= n; k++)
        {
            int i = Mathf.Clamp(k, 0, n - 1);
            float w = W[i] * widthScale;
            Vector3 c = P[i] + N[i] * offset;
            float a = alpha;
            float v = V[i];
            if (k == -1) { c -= D[i] * w * 0.5f; a = 0f; v -= 0.5f * widthScale; }
            else if (k == n) { c += D[i] * w * 0.5f; a = 0f; v += 0.5f * widthScale; }

            verts.Add(c - S[i] * w * 0.5f);
            verts.Add(c + S[i] * w * 0.5f);
            uvs.Add(new Vector2(0f, v / widthScale));
            uvs.Add(new Vector2(1f, v / widthScale));
            Color col = WetColor(TI[i], 1f);
            col.a = a;
            colors.Add(col); colors.Add(col);
            norms.Add(N[i]); norms.Add(N[i]);
        }
        StripTris(tris, start, n + 2);
    }

    void AddCap(int i, float faceOffset, float backOffset, Vector3 outward)
    {
        float h = W[i] * SolidFraction;
        Vector3 back = P[i] + N[i] * backOffset;
        Vector3 face = P[i] + N[i] * faceOffset;
        int s = verts.Count;
        verts.Add(back - S[i] * h); verts.Add(back + S[i] * h);
        verts.Add(face - S[i] * h); verts.Add(face + S[i] * h);
        for (int k = 0; k < 4; k++) { uvs.Add(new Vector2(0.5f, 0f)); colors.Add(Color.white); norms.Add(outward); }
        solidTris.Add(s); solidTris.Add(s + 2); solidTris.Add(s + 1);
        solidTris.Add(s + 1); solidTris.Add(s + 2); solidTris.Add(s + 3);
    }

    // Треугольники для ленты из pairs пар вершин, начиная с индекса start.
    // Обе стороны (нормали заданы вручную), чтобы бока были видны откуда угодно.
    static void StripTris(List<int> tris, int start, int pairs)
    {
        for (int p = 0; p < pairs - 1; p++)
        {
            int a = start + p * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(b); tris.Add(d); tris.Add(c);
        }
    }

    Vector3 DirectionAt(int i)
    {
        int n = points.Count;
        if (n < 2)
        {
            // Одиночная точка (короткий «пшик»): любое направление в плоскости поверхности.
            Vector3 d0 = Vector3.ProjectOnPlane(Vector3.up, normals[i]);
            if (d0.sqrMagnitude < 1e-4f) d0 = Vector3.ProjectOnPlane(Vector3.forward, normals[i]);
            return d0.normalized;
        }
        Vector3 d = points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)];
        return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
    }
}
