using System;
using UnityEngine;

// Что сейчас под прицелом баллончика.
public enum SprayMode
{
    None,
    Surface,       // пол / потолок: плоское граффити
    Wall,          // стена: объёмное граффити, выступает из стены — на него можно встать
    Air,           // пусто: твёрдый мост в воздухе
    ExtendBridge,  // прицел на мосту: мост продолжается вперёд
}

// Баллончик. Пока зажата ЛКМ, рисует МАЗОК (PaintStroke). Твёрдое — само граффити:
//
//  - пол / потолок ближе range  -> плоское граффити;
//  - стена                      -> объёмное граффити, выступает из стены на wallDepth.
//                                  Что нарисовал, то и получил: горизонтальная линия — полка,
//                                  диагональ — пандус, зигзаг — ступеньки;
//  - пусто                      -> мост: твёрдая лента в воздухе в airDistance перед камерой;
//  - верх моста                 -> мост продолжается вперёд по взгляду.
//
// Краска лежит на отдельном слое PaintLayer. Луч для поверхностей проходит сквозь краску,
// поэтому краска не «нарастает» на саму себя навстречу игроку.
//
// Ширина граффити зависит от дистанции (близко — узко), широкий мазок тратит больше краски.
// Свежая краска мокрая: проседает под игроком и застывает за dryTime (PaintSpot).
// Если подержать баллончик на одном месте стены, вниз стекает подтёк с каплей (DripRunner).
public class PaintSpray : MonoBehaviour
{
    public Camera cam;

    [Header("Краска")]
    [Tooltip("Запас краски: столько секунд можно распылять мазком ширины referenceWidth")]
    public float maxPaintSeconds = 10f;
    [Tooltip("При этой ширине краска тратится ровно 1 секунда запаса за 1 секунду")]
    public float referenceWidth = 0.6f;

    [Header("Луч")]
    public float range = 7f;
    public float airDistance = 3.5f;

    [Header("Размер")]
    [Tooltip("Ширина граффити вблизи (nearDistance) и вдали (farDistance)")]
    public float minWidth = 0.2f;
    public float maxWidth = 0.6f;
    public float nearDistance = 1f;
    public float farDistance = 7f;
    [Tooltip("Ширина моста в воздухе")]
    public float airWidth = 0.7f;
    public float bridgeThickness = 0.1f;

    [Header("Мазок")]
    [Tooltip("Новая точка мазка — не ближе этого к предыдущей")]
    public float minSegment = 0.15f;
    [Tooltip("Если прицел прыгнул дальше — начинается новый мазок")]
    public float maxSegment = 1.0f;
    [Tooltip("Если поверхность повернулась сильнее (угол стены) — начинается новый мазок")]
    public float maxNormalTurn = 35f;
    [Tooltip("На сколько (в долях ширины) мост вырастает вперёд, когда целишься в его край")]
    public float growStep = 0.45f;

    [Header("Граффити на стене")]
    [Tooltip("Насколько граффити выступает из стены (на нём можно стоять)")]
    public float wallDepth = 0.3f;

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
    public float dripWidth = 0.12f;
    public float dropWidth = 0.32f;

    public const int PlayerLayer = 2; // встроенный "Ignore Raycast" — на нём игрок
    public const int PaintLayer = 8;  // первый свободный слой; в Tags & Layers можно назвать его "Paint"

    public float Paint { get; private set; }
    public float Paint01 => maxPaintSeconds > 0f ? Paint / maxPaintSeconds : 0f;
    public bool IsSpraying { get; private set; }
    public bool IsSprayingEmpty { get; private set; }
    public int PieceCount { get; private set; }

    // Для подсказки.
    public bool HasPreview { get; private set; }
    public SprayMode PreviewMode => target.mode;
    public Vector3 PreviewPosition => target.pos;
    public Vector3 PreviewNormal => target.normal;
    public float PreviewWidth => target.width;
    public bool PreviewBlocked { get; private set; }
    public Vector3 AimPoint => target.aim;
    public Color CurrentColor => palette[(strokeActive ? strokeIndex - 1 : strokeIndex) % palette.Length];

    // Появился новый кусок мазка: позиция, поворот, цвет (для облачка частиц).
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

    struct Target
    {
        public SprayMode mode;
        public Vector3 pos;     // точка на поверхности / верх моста
        public Vector3 normal;
        public float width;
        public Vector3 anchor;  // для ExtendBridge: куда попал луч на мосту
        public Vector3 aim;     // куда летит струя
    }

    FirstPersonController controller;
    Transform paintRoot;
    Target target;

    Color strokeColor;
    int strokeIndex;
    bool strokeActive;

    PaintStroke stroke;     // текущий кусок мазка (null — начать новый)
    SprayMode strokeKind;
    int strokeSerial;
    Vector3 lastPos, lastNormal;
    float lastWidth;

    bool dripAnchorValid;
    Vector3 dripAnchor;
    float dripStillTime;
    bool dripSpawnedHere;

    void Awake()
    {
        Paint = maxPaintSeconds;
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

        AddTarget(target);
        UpdateDrip();
    }

    // Мокрая краска под ногами проседает.
    void PressPaintUnderFeet()
    {
        if (controller == null || controller.GroundCollider == null) return;
        var spot = controller.GroundCollider.GetComponent<PaintSpot>();
        if (spot != null) spot.Press(Time.deltaTime);
    }

    // Каждое нажатие ЛКМ — новый цвет.
    void StartStroke()
    {
        strokeActive = true;
        strokeColor = palette[strokeIndex % palette.Length];
        strokeIndex++;
        stroke = null;
    }

    void EndStroke()
    {
        strokeActive = false;
        stroke = null;
        dripAnchorValid = false;
    }

    float WidthForDistance(float distance)
    {
        return Mathf.Lerp(minWidth, maxWidth, Mathf.InverseLerp(nearDistance, farDistance, distance));
    }

    // ---------- Куда целимся ----------

    void ComputeTarget()
    {
        Transform camT = cam.transform;
        Ray ray = new Ray(camT.position, camT.forward);
        int surfaceMask = ~((1 << PlayerLayer) | (1 << PaintLayer));

        bool hasSurface = Physics.Raycast(ray, out RaycastHit sh, range, surfaceMask, QueryTriggerInteraction.Ignore);
        bool hasPaint = Physics.Raycast(ray, out RaycastHit ph, range, 1 << PaintLayer, QueryTriggerInteraction.Ignore);

        var t = new Target { mode = SprayMode.None };

        // Прицел на верхе моста (и мост ближе поверхности) — продолжаем мост.
        if (hasPaint && (!hasSurface || ph.distance <= sh.distance) && ph.normal.y > 0.5f && IsBridge(ph.collider))
        {
            Vector3 along = Vector3.ProjectOnPlane(ray.direction, ph.normal);
            if (along.sqrMagnitude > 0.01f)
            {
                t.mode = SprayMode.ExtendBridge;
                t.normal = ph.normal;
                t.width = airWidth;
                t.anchor = ph.point;
                t.pos = ph.point + along.normalized * (airWidth * growStep);
                t.aim = ph.point;
            }
        }

        if (t.mode == SprayMode.None)
        {
            if (hasSurface)
            {
                t.mode = Mathf.Abs(sh.normal.y) < 0.5f ? SprayMode.Wall : SprayMode.Surface;
                t.normal = sh.normal;
                t.width = WidthForDistance(sh.distance);
                t.pos = sh.point;
                t.aim = sh.point;
            }
            else
            {
                t.mode = SprayMode.Air;
                t.normal = Vector3.up;
                t.width = airWidth;
                t.pos = ray.origin + ray.direction * airDistance;
                t.aim = t.pos;
            }
        }

        target = t;

        switch (t.mode)
        {
            case SprayMode.Air:
            case SprayMode.ExtendBridge:
                PreviewBlocked = OverlapsPlayer(t.pos - t.normal * (bridgeThickness * 0.5f),
                    Quaternion.FromToRotation(Vector3.up, t.normal), new Vector3(t.width, bridgeThickness, t.width));
                break;
            case SprayMode.Wall:
                PreviewBlocked = OverlapsPlayer(t.pos + t.normal * (wallDepth * 0.5f),
                    Quaternion.LookRotation(WallUp(t.normal), t.normal), new Vector3(t.width, wallDepth, t.width));
                break;
            default:
                PreviewBlocked = false; // граффити на полу ничему не мешает
                break;
        }
    }

    static bool IsBridge(Collider c)
    {
        var spot = c.GetComponent<PaintSpot>();
        return spot != null && spot.Stroke != null && spot.Stroke.Style == StrokeStyle.Bridge;
    }

    // ---------- Рисование ----------

    void AddTarget(Target t)
    {
        SprayMode kind = t.mode == SprayMode.ExtendBridge ? SprayMode.Air : t.mode;
        bool onSurface = kind == SprayMode.Surface || kind == SprayMode.Wall;
        Vector3 pos = onSurface ? t.pos + t.normal * SurfaceOffset() : t.pos;

        bool needNew = stroke == null
                       || kind != strokeKind
                       || Vector3.Angle(t.normal, lastNormal) > maxNormalTurn
                       || (pos - lastPos).magnitude > maxSegment;

        if (needNew)
        {
            BeginStroke(kind);
            if (onSurface) pos = t.pos + t.normal * SurfaceOffset(); // смещение зависит от номера мазка
            // Продолжение моста начинаем с точки, куда попал луч, — чтобы новая лента примыкала к старой.
            if (t.mode == SprayMode.ExtendBridge && !AppendPoint(t.anchor, t.normal, t.width))
            {
                DropStroke();
                return;
            }
        }

        if (stroke.Count > 0 && (pos - lastPos).magnitude < minSegment) return;
        if (!AppendPoint(pos, t.normal, t.width)) DropStroke(); // мешает игрок — начнём заново
    }

    // Мазок прерван; пустой — удаляем, чтобы не копились лишние объекты.
    void DropStroke()
    {
        if (stroke != null && stroke.Count == 0) Destroy(stroke.gameObject);
        stroke = null;
    }

    void BeginStroke(SprayMode kind)
    {
        var go = new GameObject(kind == SprayMode.Air ? "PaintBridge" : "PaintStroke");
        go.transform.SetParent(paintRoot, false);
        stroke = go.AddComponent<PaintStroke>();
        switch (kind)
        {
            case SprayMode.Air: stroke.Init(strokeColor, StrokeStyle.Bridge, bridgeThickness, true, dryTime); break;
            case SprayMode.Wall: stroke.Init(strokeColor, StrokeStyle.Raised, wallDepth, true, dryTime); break;
            default: stroke.Init(strokeColor, StrokeStyle.Flat, 0f, false, dryTime); break;
        }
        strokeKind = kind;
        strokeSerial++;
    }

    // Разные мазки чуть на разной высоте над поверхностью — чтобы не мерцали друг сквозь друга.
    float SurfaceOffset() => 0.01f + (strokeSerial % 10) * 0.002f;

    bool AppendPoint(Vector3 pos, Vector3 normal, float width)
    {
        if (stroke.Count == 0)
        {
            if (strokeKind == SprayMode.Air && !CreatePad(pos - normal * (bridgeThickness * 0.5f), normal, width, bridgeThickness)) return false;
            if (strokeKind == SprayMode.Wall && !CreatePad(pos + normal * (wallDepth * 0.5f), normal, width, wallDepth)) return false;
            Commit(pos, normal, width);
            return true;
        }

        Vector3 d = pos - lastPos;
        float len = d.magnitude;

        switch (strokeKind)
        {
            case SprayMode.Air:
                // Мазок в воздухе строго вверх/вниз — ставим ленту лицом к игроку.
                if (Vector3.Cross(normal, d / len).sqrMagnitude < 0.05f)
                {
                    Vector3 facing = Vector3.ProjectOnPlane(-cam.transform.forward, d);
                    if (facing.sqrMagnitude < 1e-4f) return false;
                    normal = facing.normalized;
                }
                if (!CreateSegment(lastPos, pos, normal, (lastWidth + width) * 0.5f, -bridgeThickness)) return false;
                break;

            case SprayMode.Wall:
                if (!CreateSegment(lastPos, pos, normal, (lastWidth + width) * 0.5f, wallDepth)) return false;
                break;
        }

        Commit(pos, normal, width);
        return true;
    }

    void Commit(Vector3 pos, Vector3 normal, float width)
    {
        stroke.AddPoint(pos, normal, width);
        lastPos = pos;
        lastWidth = width;
        lastNormal = normal;
        SpotPlaced?.Invoke(pos, Quaternion.FromToRotation(Vector3.up, normal), strokeColor);
    }

    // ---------- Твёрдые куски (невидимые коллайдеры под мазком) ----------

    // «Вверх» в плоскости стены (для ориентации одиночной точки).
    static Vector3 WallUp(Vector3 normal)
    {
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
        return up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
    }

    // Плита под одиночной точкой (короткий «пшик» тоже твёрдый). center — центр плиты.
    bool CreatePad(Vector3 center, Vector3 normal, float width, float depth)
    {
        Quaternion rot = Quaternion.LookRotation(WallUp(normal), normal); // локальная Y — по нормали
        Vector3 size = new Vector3(width * 0.84f, depth, width * 0.84f);
        if (OverlapsPlayer(center, rot, size)) return false;
        var spot = CreateSolid(center, rot, size);
        spot.Stroke = stroke;
        stroke.AddPad(spot);
        return true;
    }

    // Плита под отрезком мазка. depth > 0 — выступает из поверхности по нормали (стена),
    // depth < 0 — уходит вниз от верха (мост).
    bool CreateSegment(Vector3 a, Vector3 b, Vector3 normal, float width, float depth)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        Vector3 up = Vector3.ProjectOnPlane(normal, d);
        if (up.sqrMagnitude < 1e-4f) return false;
        up.Normalize();

        Quaternion rot = Quaternion.LookRotation(d / len, up); // локальная Y — по нормали, Z — вдоль мазка
        Vector3 center = (a + b) * 0.5f + up * (depth * 0.5f);
        // Ширина — твёрдая часть мазка (мягкие края текстуры свисают), длина чуть больше — без щелей.
        Vector3 size = new Vector3(width * 0.84f, Mathf.Abs(depth), len + 0.05f);
        if (OverlapsPlayer(center, rot, size)) return false;

        var spot = CreateSolid(center, rot, size);
        spot.Stroke = stroke;
        stroke.AddSegment(spot);
        return true;
    }

    // Подтёк: капля внизу — маленький объёмный «пшик», на который можно встать.
    public void CreateDrop(Vector3 surfacePoint, Vector3 wallNormal, Color color)
    {
        var go = new GameObject("PaintDrop");
        go.transform.SetParent(paintRoot, false);
        var drop = go.AddComponent<PaintStroke>();
        drop.Init(color, StrokeStyle.Raised, wallDepth, true, dryTime);

        Vector3 pos = surfacePoint + wallNormal * 0.03f;
        Vector3 center = pos + wallNormal * (wallDepth * 0.5f);
        Quaternion rot = Quaternion.LookRotation(WallUp(wallNormal), wallNormal);
        Vector3 size = new Vector3(dropWidth * 0.84f, wallDepth, dropWidth * 0.84f);
        if (!OverlapsPlayer(center, rot, size))
        {
            var spot = CreateSolid(center, rot, size);
            spot.Stroke = drop;
            drop.AddPad(spot);
        }
        drop.AddPoint(pos, wallNormal, dropWidth);
    }

    PaintSpot CreateSolid(Vector3 center, Quaternion rot, Vector3 size)
    {
        var go = new GameObject("PaintSolid");
        go.AddComponent<BoxCollider>(); // 1×1×1, растягивается масштабом
        go.layer = PaintLayer;
        go.transform.SetParent(paintRoot, false);
        go.transform.SetPositionAndRotation(center, rot);
        go.transform.localScale = size;

        var spot = go.AddComponent<PaintSpot>();
        spot.Init(strokeColor, dryTime, sinkSpeed, maxSink);
        PieceCount++;
        return spot;
    }

    // Не создаём твёрдую краску внутри игрока — иначе он застрянет.
    // По высоте коробку чуть уменьшаем: краска вровень с полом под ногами — это нормально.
    bool OverlapsPlayer(Vector3 center, Quaternion rot, Vector3 size)
    {
        Vector3 half = new Vector3(size.x * 0.5f, Mathf.Max(0.01f, size.y * 0.5f - 0.03f), size.z * 0.5f);
        return Physics.CheckBox(center, half, rot, 1 << PlayerLayer, QueryTriggerInteraction.Ignore);
    }

    // ---------- Подтёки ----------

    void UpdateDrip()
    {
        if (target.mode != SprayMode.Wall) { dripAnchorValid = false; return; }

        if (dripAnchorValid && (target.pos - dripAnchor).sqrMagnitude < 0.12f * 0.12f)
        {
            dripStillTime += Time.deltaTime;
            if (dripStillTime >= dripDelay && !dripSpawnedHere)
            {
                var go = new GameObject("Drip");
                go.transform.SetParent(paintRoot, false);
                go.AddComponent<DripRunner>().Init(this, target.pos, target.normal, strokeColor);
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

    // Струйка подтёка: тонкая выпуклая лента без коллайдеров.
    public PaintStroke CreateDripStreak(Color color)
    {
        var go = new GameObject("PaintDrip");
        go.transform.SetParent(paintRoot, false);
        var streak = go.AddComponent<PaintStroke>();
        streak.Init(color, StrokeStyle.Raised, 0.05f, false, dryTime);
        return streak;
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
