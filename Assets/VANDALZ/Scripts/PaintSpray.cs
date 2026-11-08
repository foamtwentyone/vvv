using System;
using UnityEngine;

// Баллончик. Пока зажата ЛКМ, рисует сплошной МАЗОК — ленту из плит, по которой можно ходить.
//
// Куда ложится краска (луч из центра камеры):
//  - пол / потолок ближе range        -> лента по поверхности;
//  - стена                            -> горизонтальная полка, торчащая из стены;
//                                        вертикальный мазок по стене = лестница уступов;
//  - верх уже нарисованной краски     -> лента продолжается вперёд по взгляду (мост «растёт»);
//  - пусто                            -> краска застывает в воздухе в airDistance перед камерой.
//
// Ширина мазка зависит от дистанции: близко — узко, далеко — широко, но и краски тратится больше.
// Свежая краска мокрая: проседает под игроком и застывает за dryTime (см. PaintSpot).
// Если долго держать баллончик на одном месте стены, вниз стекает подтёк (см. DripRunner).
public class PaintSpray : MonoBehaviour
{
    public Camera cam;

    [Header("Краска")]
    [Tooltip("Запас краски: столько секунд можно распылять мазком ширины referenceWidth")]
    public float maxPaintSeconds = 10f;

    [Header("Луч")]
    public float range = 7f;
    public float airDistance = 3.5f;

    [Header("Ширина от дистанции")]
    public float minWidth = 0.5f;
    public float maxWidth = 1.4f;
    public float nearDistance = 1f;
    public float farDistance = 7f;
    [Tooltip("При этой ширине краска тратится ровно 1 секунда запаса за 1 секунду")]
    public float referenceWidth = 0.9f;

    [Header("Мазок")]
    public float thickness = 0.12f;
    [Tooltip("Новая точка мазка — не ближе этого к предыдущей")]
    public float minSegment = 0.2f;
    [Tooltip("Если прицел прыгнул дальше этого — мазок рвётся и начинается заново")]
    public float maxSegment = 1.2f;
    [Tooltip("Шаг уступов, когда ведёшь баллончиком по стене вверх/вниз")]
    public float ladderStep = 0.5f;
    [Tooltip("На сколько (в долях ширины) мост вырастает вперёд, когда целишься в краску")]
    public float growStep = 0.4f;
    [Tooltip("Угол поворота мазка, после которого ставится круглый стык")]
    public float jointAngle = 15f;

    [Header("Застывание")]
    public float dryTime = 2.5f;
    public float sinkSpeed = 0.6f;
    public float maxSink = 0.7f;

    [Header("Подтёки")]
    [Tooltip("Сколько секунд держать баллончик на одном месте стены, чтобы потекло")]
    public float dripDelay = 0.7f;
    public float dripStep = 0.45f;
    public float dripInterval = 0.3f;
    public int dripMaxSteps = 5;

    public float Paint { get; private set; }
    public float Paint01 => maxPaintSeconds > 0f ? Paint / maxPaintSeconds : 0f;
    public bool IsSpraying { get; private set; }
    public bool IsSprayingEmpty { get; private set; }
    public int PieceCount { get; private set; }

    // Где окажется краска прямо сейчас (для подсказки).
    public bool HasPreview { get; private set; }
    public Vector3 PreviewPosition => target.pos;
    public Quaternion PreviewRotation => Quaternion.FromToRotation(Vector3.up, target.up);
    public float PreviewWidth => target.width;
    public bool PreviewInAir => target.inAir;
    public bool PreviewBlocked { get; private set; }
    public Vector3 AimPoint { get; private set; }
    public Color CurrentColor => palette[(strokeActive ? strokeIndex - 1 : strokeIndex) % palette.Length];

    // Появился новый кусок краски: позиция, поворот, цвет (для облачка частиц).
    public event Action<Vector3, Quaternion, Color> SpotPlaced;

    static readonly Color[] palette =
    {
        new Color(1f, 0.2f, 0.6f),   // розовый
        new Color(0.2f, 0.9f, 1f),   // голубой
        new Color(1f, 0.85f, 0.1f),  // жёлтый
        new Color(0.5f, 1f, 0.2f),   // кислотно-зелёный
        new Color(1f, 0.45f, 0.1f),  // оранжевый
        new Color(0.65f, 0.35f, 1f), // фиолетовый
    };

    const int PlayerLayer = 2; // встроенный слой "Ignore Raycast" — на нём игрок

    // Точка мазка.
    struct PaintPoint
    {
        public Vector3 pos;      // центр краски
        public Vector3 up;       // «верх» плиты
        public float width;
        public bool inAir;
        public bool onWall;      // полка на стене
        public bool onPaint;     // луч попал в краску
        public Vector3 surface;  // точка на стене (для полок и подтёков)
        public Vector3 outward;  // нормаль стены
    }

    FirstPersonController controller;
    Transform paintRoot;
    int rayMask;

    PaintPoint target;
    Color strokeColor;
    int strokeIndex;
    bool strokeActive;

    bool hasLast;
    PaintPoint last;
    bool hasLastDir;
    Vector3 lastDir;

    bool dripAnchorValid;
    Vector3 dripAnchor;
    float dripStillTime;
    bool dripSpawnedHere;

    void Awake()
    {
        Paint = maxPaintSeconds;
        rayMask = ~(1 << PlayerLayer);
        controller = GetComponent<FirstPersonController>();
        // Краска не привязана к игроку, чтобы оставалась на месте.
        paintRoot = new GameObject("Paint").transform;
    }

    void Update()
    {
        IsSpraying = false;
        IsSprayingEmpty = false;
        HasPreview = false;

        PressPaintUnderFeet();

        if (cam == null || Cursor.lockState != CursorLockMode.Locked)
        {
            EndStroke();
            return;
        }

        ComputeTarget();

        bool held = VandalzInput.SprayHeld();
        if (!held || Paint <= 0f)
        {
            IsSprayingEmpty = held;
            EndStroke();
            HasPreview = Paint > 0f;
            return;
        }

        if (!strokeActive) StartStroke();

        IsSpraying = true;
        HasPreview = true;
        // Широкий мазок тратит больше краски.
        Paint = Mathf.Max(0f, Paint - Time.deltaTime * target.width / referenceWidth);

        AddPoint(target);
        UpdateDrip();
    }

    // Мокрая краска под ногами проседает.
    void PressPaintUnderFeet()
    {
        if (controller == null || controller.GroundCollider == null) return;
        var spot = controller.GroundCollider.GetComponent<PaintSpot>();
        if (spot != null) spot.Press(Time.deltaTime);
    }

    // Каждое нажатие ЛКМ — новый мазок и новый цвет.
    void StartStroke()
    {
        strokeActive = true;
        strokeColor = palette[strokeIndex % palette.Length];
        strokeIndex++;
        hasLast = false;
        hasLastDir = false;
    }

    void EndStroke()
    {
        strokeActive = false;
        hasLast = false;
        hasLastDir = false;
        dripAnchorValid = false;
    }

    float WidthForDistance(float distance)
    {
        return Mathf.Lerp(minWidth, maxWidth, Mathf.InverseLerp(nearDistance, farDistance, distance));
    }

    // Считает, куда ляжет краска, если распылять прямо сейчас.
    void ComputeTarget()
    {
        Transform camT = cam.transform;
        Ray ray = new Ray(camT.position, camT.forward);
        var p = new PaintPoint();

        if (Physics.Raycast(ray, out RaycastHit hit, range, rayMask, QueryTriggerInteraction.Ignore))
        {
            AimPoint = hit.point;
            Vector3 n = hit.normal;
            p.width = WidthForDistance(hit.distance);
            p.onPaint = hit.collider.GetComponent<PaintSpot>() != null;
            Vector3 along = Vector3.ProjectOnPlane(ray.direction, n);

            if (Mathf.Abs(n.y) < 0.5f)
            {
                // Стена (или бок краски): горизонтальная полка, выступающая из поверхности.
                p.onWall = true;
                p.outward = new Vector3(n.x, 0f, n.z).normalized;
                p.surface = hit.point;
                p.pos = hit.point + p.outward * (p.width * 0.5f - 0.05f);
                p.up = Vector3.up;
            }
            else if (p.onPaint && n.y > 0.5f && along.sqrMagnitude > 0.01f)
            {
                // Верх краски: продолжаем в той же плоскости чуть дальше по взгляду.
                p.pos = hit.point - n * (thickness * 0.5f) + along.normalized * (p.width * growStep);
                p.up = n;
            }
            else
            {
                // Пол, потолок: лента лежит на поверхности.
                p.pos = hit.point + n * (thickness * 0.5f);
                p.up = n;
            }
        }
        else
        {
            // Пусто: краска застывает в воздухе, горизонтально.
            p.inAir = true;
            p.width = WidthForDistance(airDistance);
            p.pos = ray.origin + ray.direction * airDistance;
            p.up = Vector3.up;
            AimPoint = p.pos;
        }

        target = p;
        PreviewBlocked = OverlapsPlayer(p.pos, PreviewRotation, new Vector3(p.width, thickness, p.width));
    }

    // Добавляет точку к мазку: плита от предыдущей точки + стык на повороте.
    void AddPoint(PaintPoint p)
    {
        if (!hasLast)
        {
            if (PlaceJoint(p)) { last = p; hasLast = true; hasLastDir = false; }
            return;
        }

        Vector3 d = p.pos - last.pos;
        float len = d.magnitude;
        if (len < minSegment) return;

        if (len > maxSegment)
        {
            // Прицел прыгнул (со стены в воздух и т.п.) — рвём мазок, начинаем новый кусок.
            hasLast = false;
            AddPoint(p);
            return;
        }

        // Вертикальный мазок по стене — лестница из уступов.
        if (p.onWall && last.onWall && Mathf.Abs(d.y) > 0.6f * len)
        {
            if (len < ladderStep) return;
            if (PlaceJoint(p)) { last = p; hasLastDir = false; }
            return;
        }

        if (!PlaceSegment(last, p))
        {
            hasLast = false; // мешает игрок — начнём с новой точки
            return;
        }

        Vector3 dir = d / len;
        if (hasLastDir && Vector3.Angle(lastDir, dir) > jointAngle) PlaceJoint(last);
        lastDir = dir;
        hasLastDir = true;
        last = p;
    }

    // Круглый «блин» в точке: начало мазка, поворот, ступень лестницы.
    bool PlaceJoint(PaintPoint p)
    {
        Quaternion rot = p.onWall
            ? Quaternion.LookRotation(p.outward, Vector3.up)
            : Quaternion.FromToRotation(Vector3.up, p.up);
        Vector3 size = new Vector3(p.width, thickness, p.width);
        if (OverlapsPlayer(p.pos, rot, size)) return false;

        // Высота меша цилиндра = 2, поэтому по Y масштаб thickness / 2.
        CreatePiece(PrimitiveType.Cylinder, p.pos, rot, new Vector3(p.width, thickness * 0.5f, p.width), strokeColor, true);
        return true;
    }

    // Плита между двумя точками мазка.
    bool PlaceSegment(PaintPoint a, PaintPoint b)
    {
        Vector3 d = b.pos - a.pos;
        float len = d.magnitude;
        Vector3 up = Vector3.ProjectOnPlane(a.up + b.up, d);
        if (up.sqrMagnitude < 0.05f)
        {
            // Мазок идёт строго вдоль «верха» (например, вертикально в воздухе) —
            // ставим плиту лицом к игроку.
            up = Vector3.ProjectOnPlane(-cam.transform.forward, d);
            if (up.sqrMagnitude < 0.0001f) return false;
        }

        Quaternion rot = Quaternion.LookRotation(d / len, up.normalized);
        float width = (a.width + b.width) * 0.5f;
        Vector3 center = (a.pos + b.pos) * 0.5f;
        // Чуть длиннее, чтобы соседние плиты перекрывались без щелей.
        Vector3 size = new Vector3(width, thickness, len + 0.04f);
        if (OverlapsPlayer(center, rot, size)) return false;

        CreatePiece(PrimitiveType.Cube, center, rot, size, strokeColor, true);
        return true;
    }

    // ---------- Подтёки ----------

    void UpdateDrip()
    {
        bool canDrip = target.onWall && !target.onPaint;
        if (!canDrip) { dripAnchorValid = false; return; }

        if (dripAnchorValid && (target.pos - dripAnchor).sqrMagnitude < 0.15f * 0.15f)
        {
            dripStillTime += Time.deltaTime;
            if (dripStillTime >= dripDelay && !dripSpawnedHere)
            {
                var go = new GameObject("Drip");
                go.transform.SetParent(paintRoot, false);
                go.AddComponent<DripRunner>().Init(this, target.surface, target.outward, strokeColor, target.width * 0.6f);
                dripSpawnedHere = true;
            }
        }
        else
        {
            dripAnchor = target.pos;
            dripAnchorValid = true;
            dripStillTime = 0f;
            dripSpawnedHere = false;
        }
    }

    // Вызывается DripRunner: полоска по стене от from до to и маленький уступ в точке to.
    public void CreateDripPiece(Vector3 fromSurface, Vector3 toSurface, Vector3 outward, Color color, float ledgeWidth)
    {
        Vector3 d = toSurface - fromSurface;
        float len = d.magnitude;
        if (len > 0.01f)
        {
            Quaternion streakRot = Quaternion.LookRotation(outward, d / len);
            Vector3 center = (fromSurface + toSurface) * 0.5f + outward * 0.02f;
            CreatePiece(PrimitiveType.Cube, center, streakRot, new Vector3(0.16f, len, 0.03f), color, false);
        }

        Vector3 ledgePos = toSurface + outward * (ledgeWidth * 0.5f - 0.03f);
        Quaternion ledgeRot = Quaternion.LookRotation(outward, Vector3.up);
        if (!OverlapsPlayer(ledgePos, ledgeRot, new Vector3(ledgeWidth, thickness, ledgeWidth)))
            CreatePiece(PrimitiveType.Cylinder, ledgePos, ledgeRot, new Vector3(ledgeWidth, thickness * 0.5f, ledgeWidth), color, true);
    }

    // ---------- Общее ----------

    // Не создаём краску внутри игрока — иначе он застрянет.
    // По высоте коробку чуть уменьшаем: краска вровень с полом под ногами — это нормально.
    bool OverlapsPlayer(Vector3 center, Quaternion rot, Vector3 size)
    {
        Vector3 half = new Vector3(size.x * 0.5f, Mathf.Max(0.01f, size.y * 0.5f - 0.03f), size.z * 0.5f);
        return Physics.CheckBox(center, half, rot, 1 << PlayerLayer, QueryTriggerInteraction.Ignore);
    }

    void CreatePiece(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Color color, bool solid)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = solid ? "PaintPiece" : "PaintStreak";
        go.transform.SetParent(paintRoot, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;

        // У цилиндра капсульный коллайдер — для плоского блина меняем его на коробку.
        DestroyImmediate(go.GetComponent<Collider>());
        if (solid) go.AddComponent<BoxCollider>();

        go.GetComponent<Renderer>().sharedMaterial = ProtoMaterials.Get(color);
        if (solid) go.AddComponent<PaintSpot>().Init(color, dryTime, sinkSpeed, maxSink);

        PieceCount++;
        SpotPlaced?.Invoke(pos, rot, color);
    }

    // Стереть всю краску и заполнить баллончик (рестарт уровня).
    public void ResetPaint()
    {
        foreach (Transform child in paintRoot) Destroy(child.gameObject);
        PieceCount = 0;
        Paint = maxPaintSeconds;
        EndStroke();
    }
}
