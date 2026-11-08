using UnityEngine;

// Баллончик. Пока зажата ЛКМ:
//  - луч из центра камеры попал в поверхность ближе range  -> пятно на поверхности;
//    если это стена (или бок другого пятна) — пятно торчит горизонтальным уступом;
//  - луч ни во что не попал -> пятно застывает в воздухе в airDistance перед камерой.
// Пятна сами имеют коллайдер, поэтому луч попадает и в них — мост «растёт» от краски:
// попал в верх пятна -> новое пятно вплотную, в той же плоскости, чуть дальше по взгляду.
public class PaintSpray : MonoBehaviour
{
    public Camera cam;

    [Header("Краска")]
    [Tooltip("Запас краски в секундах распыления")]
    public float maxPaintSeconds = 10f;

    [Header("Луч")]
    public float range = 7f;
    public float airDistance = 3.5f;

    [Header("Пятна")]
    public float spotDiameter = 1.0f;
    public float spotThickness = 0.12f;
    [Tooltip("Не чаще одного пятна за столько секунд")]
    public float spawnInterval = 0.04f;
    [Tooltip("Новое пятно ставится, только если оно дальше этого от предыдущего")]
    public float minSpacing = 0.35f;
    [Tooltip("На сколько (в долях диаметра) мост вырастает вперёд, когда целишься в пятно")]
    public float growStep = 0.4f;

    public float Paint { get; private set; }
    public float Paint01 => maxPaintSeconds > 0f ? Paint / maxPaintSeconds : 0f;
    public bool IsSpraying { get; private set; }
    public int SpotCount { get; private set; }

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

    Transform spotsRoot;
    Material strokeMaterial;
    int strokeIndex;
    bool strokeActive;
    bool hasLastSpot;
    Vector3 lastSpotPos;
    float nextSpawnTime;
    int rayMask;

    void Awake()
    {
        Paint = maxPaintSeconds;
        rayMask = ~(1 << PlayerLayer);
        // Пятна не привязаны к игроку, чтобы оставались на месте.
        spotsRoot = new GameObject("PaintSpots").transform;
    }

    void Update()
    {
        IsSpraying = false;

        bool wantsSpray = VandalzInput.SprayHeld()
                          && Cursor.lockState == CursorLockMode.Locked
                          && Paint > 0f
                          && cam != null;

        if (!wantsSpray)
        {
            strokeActive = false;
            hasLastSpot = false;
            return;
        }

        if (!strokeActive) StartStroke();

        IsSpraying = true;
        Paint = Mathf.Max(0f, Paint - Time.deltaTime);

        if (Time.time >= nextSpawnTime) TryPlaceSpot();
    }

    // Каждое нажатие ЛКМ — новый цвет, чтобы было видно отдельные «мазки».
    void StartStroke()
    {
        strokeActive = true;
        strokeMaterial = ProtoMaterials.Get(palette[strokeIndex % palette.Length]);
        strokeIndex++;
    }

    void TryPlaceSpot()
    {
        Transform camT = cam.transform;
        Ray ray = new Ray(camT.position, camT.forward);

        Vector3 pos;
        Quaternion rot;

        if (Physics.Raycast(ray, out RaycastHit hit, range, rayMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 n = hit.normal;
            if (Mathf.Abs(n.y) < 0.5f)
            {
                // Стена или бок пятна: горизонтальный уступ, выступающий из поверхности.
                Vector3 outward = new Vector3(n.x, 0f, n.z).normalized;
                pos = hit.point + outward * (spotDiameter * 0.5f - 0.05f);
                rot = Quaternion.LookRotation(outward, Vector3.up);
            }
            else if (n.y > 0.5f && hit.collider.GetComponent<PaintSpot>() != null
                     && Vector3.ProjectOnPlane(ray.direction, n).sqrMagnitude > 0.01f)
            {
                // Верх пятна: новое пятно в той же плоскости, сдвинутое по направлению взгляда.
                // Так мост «растёт» вперёд от края краски, а не копится стопкой.
                Vector3 along = Vector3.ProjectOnPlane(ray.direction, n).normalized;
                pos = hit.point - n * (spotThickness * 0.5f) + along * (spotDiameter * growStep);
                rot = Quaternion.LookRotation(along, n);
            }
            else
            {
                // Пол, потолок: плоское пятно по поверхности.
                pos = hit.point + n * (spotThickness * 0.5f);
                rot = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }
        else
        {
            // Пусто: пятно застывает в воздухе, горизонтально, повёрнуто по взгляду.
            pos = ray.origin + ray.direction * airDistance;
            rot = Quaternion.Euler(0f, camT.eulerAngles.y, 0f);
        }

        if (hasLastSpot && (pos - lastSpotPos).sqrMagnitude < minSpacing * minSpacing) return;

        float diameter = spotDiameter * Random.Range(0.9f, 1.15f);
        // Не создаём пятно внутри игрока — иначе он застрянет.
        // По высоте коробку чуть уменьшаем: пятно вровень с полом под ногами — это нормально.
        Vector3 halfExtents = new Vector3(diameter * 0.5f, spotThickness * 0.5f - 0.03f, diameter * 0.5f);
        if (Physics.CheckBox(pos, halfExtents, rot, 1 << PlayerLayer, QueryTriggerInteraction.Ignore)) return;

        SpawnSpot(pos, rot, diameter);
        lastSpotPos = pos;
        hasLastSpot = true;
        nextSpawnTime = Time.time + spawnInterval;
    }

    // Стереть все пятна и заполнить баллончик (рестарт уровня).
    public void ResetPaint()
    {
        foreach (Transform child in spotsRoot) Destroy(child.gameObject);
        SpotCount = 0;
        Paint = maxPaintSeconds;
        strokeActive = false;
        hasLastSpot = false;
    }

    void SpawnSpot(Vector3 pos, Quaternion rot, float diameter)
    {
        // Цилиндр-«блин». Высота меша цилиндра = 2, поэтому по Y масштаб thickness/2.
        var spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spot.name = "PaintSpot";
        spot.transform.SetParent(spotsRoot, false);
        spot.transform.SetPositionAndRotation(pos, rot);
        spot.transform.localScale = new Vector3(diameter, spotThickness * 0.5f, diameter);

        // Капсульный коллайдер цилиндра плохо подходит для плоского блина — меняем на коробку.
        DestroyImmediate(spot.GetComponent<Collider>());
        spot.AddComponent<BoxCollider>();

        spot.GetComponent<Renderer>().sharedMaterial = strokeMaterial;
        spot.AddComponent<PaintSpot>();
        SpotCount++;
    }
}
