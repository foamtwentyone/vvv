using System.Collections.Generic;
using UnityEngine;

// Один мазок: плоская лента-меш вдоль точек с текстурой баллончика.
// Твёрдый мазок (мост в воздухе) дополнительно имеет невидимые коллайдеры-плиты
// под каждым отрезком (PaintSpot). Когда мокрая плита проседает под игроком,
// лента прогибается вместе с ней.
public class PaintStroke : MonoBehaviour
{
    public bool Solid { get; private set; }
    public Color StrokeColor { get; private set; }
    public int Count => points.Count;
    public Vector3 LastPoint => points[points.Count - 1];

    readonly List<Vector3> points = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<float> widths = new List<float>();
    readonly List<float> times = new List<float>();
    readonly List<PaintSpot> segments = new List<PaintSpot>(); // segments[i] — между точками i и i+1

    // Буферы меша, переиспользуются.
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> tris = new List<int>();

    Mesh mesh;
    float dryTime;
    float lastChangeTime;

    public void Init(Color color, bool solid, float dryTime)
    {
        StrokeColor = color;
        Solid = solid;
        this.dryTime = dryTime;

        mesh = new Mesh { name = "PaintStroke" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = PaintTextures.Stroke;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    public void AddPoint(Vector3 pos, Vector3 normal, float width)
    {
        points.Add(pos);
        normals.Add(normal);
        widths.Add(width);
        times.Add(Time.time);
        lastChangeTime = Time.time;
        Rebuild();
    }

    public void AddSegment(PaintSpot spot)
    {
        segments.Add(spot);
    }

    void Update()
    {
        // Пока краска мокрая — обновляем цвет и прогиб. Потом лента статична.
        if (Time.time - lastChangeTime <= dryTime + 0.2f) Rebuild();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    float SinkAt(int i)
    {
        if (!Solid) return 0f;
        float s = 0f;
        if (i - 1 >= 0 && i - 1 < segments.Count && segments[i - 1] != null) s = Mathf.Max(s, segments[i - 1].Sunk);
        if (i < segments.Count && segments[i] != null) s = Mathf.Max(s, segments[i].Sunk);
        return s;
    }

    void Rebuild()
    {
        int n = points.Count;
        verts.Clear(); uvs.Clear(); colors.Clear(); tris.Clear();
        if (n == 0) { mesh.Clear(); return; }

        Vector3 lastSide = Vector3.zero;
        float v = 0f;

        // Пары вершин: [мягкий конец] + по паре на каждую точку + [мягкий конец].
        for (int k = -1; k <= n; k++)
        {
            int i = Mathf.Clamp(k, 0, n - 1);
            Vector3 dir = DirectionAt(i);
            Vector3 side = Vector3.Cross(normals[i], dir);
            if (side.sqrMagnitude < 1e-6f) side = lastSide.sqrMagnitude > 0f ? lastSide : Vector3.Cross(normals[i], Vector3.forward);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
            side.Normalize();
            lastSide = side;

            float w = widths[i];
            Vector3 center = points[i] + Vector3.down * SinkAt(i);
            float alpha = 1f;

            // Концы мазка вытягиваем на полширины и делаем прозрачными — мягкие края.
            if (k == -1) { center -= dir * w * 0.5f; alpha = 0f; }
            else if (k == n) { center += dir * w * 0.5f; alpha = 0f; }

            if (k > -1)
            {
                Vector3 prev = verts.Count >= 2 ? (verts[verts.Count - 1] + verts[verts.Count - 2]) * 0.5f : center;
                v += Vector3.Distance(prev, center) / Mathf.Max(0.05f, w);
            }

            verts.Add(center - side * w * 0.5f);
            verts.Add(center + side * w * 0.5f);
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));

            float wet = dryTime > 0f ? Mathf.Clamp01(1f - (Time.time - times[i]) / dryTime) : 0f;
            Color c = Color.Lerp(StrokeColor, Color.white, 0.45f * wet);
            c.a = alpha;
            colors.Add(c);
            colors.Add(c);
        }

        int pairs = verts.Count / 2;
        for (int p = 0; p < pairs - 1; p++)
        {
            int a = p * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    Vector3 DirectionAt(int i)
    {
        int n = points.Count;
        if (n < 2)
        {
            // Одиночная точка (короткий «пшик»): любое направление в плоскости поверхности.
            Vector3 d0 = Vector3.ProjectOnPlane(Vector3.forward, normals[i]);
            if (d0.sqrMagnitude < 1e-4f) d0 = Vector3.ProjectOnPlane(Vector3.right, normals[i]);
            return d0.normalized;
        }
        Vector3 d = points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)];
        return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
    }
}
