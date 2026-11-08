using UnityEngine;

// Собирает прототип при запуске игры: коридор метро из примитивов, две платформы,
// пропасть, финиш, игрока с камерой и баллончиком, правила уровня.
// Ось Z — вдоль коридора. Пол платформ на высоте y = 0.
public class SubwayLevelBuilder : MonoBehaviour
{
    [Header("Размеры (метры)")]
    public float corridorWidth = 6f;
    public float corridorHeight = 5f;
    public float startPlatformLength = 10f;
    public float gapLength = 7f;
    public float farPlatformLength = 10f;
    public float pitDepth = 9f;

    static readonly Color Concrete = new Color(0.42f, 0.42f, 0.40f);
    static readonly Color DarkConcrete = new Color(0.22f, 0.22f, 0.23f);
    static readonly Color Tiles = new Color(0.62f, 0.66f, 0.60f);
    static readonly Color Ceiling = new Color(0.30f, 0.30f, 0.32f);
    static readonly Color SafetyYellow = new Color(0.95f, 0.8f, 0.1f);
    static readonly Color Rust = new Color(0.35f, 0.22f, 0.15f);
    static readonly Color FinishGreen = new Color(0.1f, 0.9f, 0.3f);

    Transform levelRoot;
    Transform cansRoot;
    CanInventory inventory;

    void Awake()
    {
        levelRoot = new GameObject("Level").transform;

        float w = corridorWidth;
        float gapStart = startPlatformLength;
        float farStart = gapStart + gapLength;
        float farEnd = farStart + farPlatformLength;
        float totalLength = farEnd;
        float bottom = -pitDepth;

        // --- Платформы (высокие блоки, чтобы у пропасти были «стенки») ---
        Box("StartPlatform", new Vector3(0, bottom / 2, startPlatformLength / 2), new Vector3(w, pitDepth, startPlatformLength), Concrete);
        Box("FarPlatform", new Vector3(0, bottom / 2, farStart + farPlatformLength / 2), new Vector3(w, pitDepth, farPlatformLength), Concrete);

        // Жёлтые линии у края — без коллайдера, чисто визуально.
        Box("EdgeLine_Start", new Vector3(0, 0.005f, gapStart - 0.25f), new Vector3(w, 0.01f, 0.3f), SafetyYellow, false);
        Box("EdgeLine_Far", new Vector3(0, 0.005f, farStart + 0.25f), new Vector3(w, 0.01f, 0.3f), SafetyYellow, false);

        // --- Дно пропасти с рельсами (игрок исчезнет раньше, чем долетит) ---
        Box("PitFloor", new Vector3(0, bottom - 0.5f, gapStart + gapLength / 2), new Vector3(w, 1f, gapLength), DarkConcrete);
        Box("Rail_L", new Vector3(-0.75f, bottom + 0.1f, gapStart + gapLength / 2), new Vector3(0.12f, 0.2f, gapLength), Rust, false);
        Box("Rail_R", new Vector3(0.75f, bottom + 0.1f, gapStart + gapLength / 2), new Vector3(0.12f, 0.2f, gapLength), Rust, false);

        // --- Стены, торцы, потолок ---
        float wallHeight = corridorHeight + pitDepth;
        float wallCenterY = (corridorHeight + bottom) / 2;
        Box("Wall_L", new Vector3(-w / 2 - 0.25f, wallCenterY, totalLength / 2), new Vector3(0.5f, wallHeight, totalLength + 1f), Tiles);
        Box("Wall_R", new Vector3(w / 2 + 0.25f, wallCenterY, totalLength / 2), new Vector3(0.5f, wallHeight, totalLength + 1f), Tiles);
        Box("Wall_Back", new Vector3(0, wallCenterY, -0.25f), new Vector3(w, wallHeight, 0.5f), Tiles);
        Box("Wall_End", new Vector3(0, wallCenterY, totalLength + 0.25f), new Vector3(w, wallHeight, 0.5f), Tiles);
        Box("Ceiling", new Vector3(0, corridorHeight + 0.25f, totalLength / 2), new Vector3(w + 1f, 0.5f, totalLength + 1f), Ceiling);

        // --- Колонны на платформах ---
        foreach (float z in new[] { 3f, 7f, farStart + 3f, farStart + 7f })
        foreach (float x in new[] { -w / 2 + 0.6f, w / 2 - 0.6f })
            Box("Column", new Vector3(x, corridorHeight / 2, z), new Vector3(0.5f, corridorHeight, 0.5f), DarkConcrete);

        // --- Свет ---
        for (float z = 3f; z < totalLength; z += 6f)
        {
            var lightGo = new GameObject("Lamp");
            lightGo.transform.SetParent(levelRoot, false);
            lightGo.transform.position = new Vector3(0, corridorHeight - 0.6f, z);
            var lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 12f;
            lamp.intensity = 1.6f;
            lamp.color = new Color(1f, 0.92f, 0.8f);
        }

        // --- Финиш ---
        float finishZ = farEnd - 2.5f;
        Box("FinishPad", new Vector3(0, 0.01f, finishZ), new Vector3(w, 0.02f, 3f), FinishGreen, false);
        Box("FinishPost_L", new Vector3(-w / 2 + 0.3f, 1.5f, finishZ), new Vector3(0.2f, 3f, 0.2f), FinishGreen);
        Box("FinishPost_R", new Vector3(w / 2 - 0.3f, 1.5f, finishZ), new Vector3(0.2f, 3f, 0.2f), FinishGreen);
        Box("FinishBar", new Vector3(0, 3f, finishZ), new Vector3(w - 0.4f, 0.2f, 0.2f), FinishGreen, false);

        // --- Игрок ---
        Vector3 spawn = new Vector3(0, 0.05f, 2f);
        var player = BuildPlayer(spawn, out PaintSpray spray);

        // --- Правила ---
        var game = gameObject.AddComponent<VandalzGame>();
        game.player = player;
        game.spray = spray;
        game.spawnPosition = spawn;
        game.spawnYaw = 0f;
        game.killY = -4f;
        game.finishZone = new Bounds(new Vector3(0, 1f, finishZ), new Vector3(w, 3f, 3f));

        // --- Баллончики ---
        cansRoot = new GameObject("Cans").transform;
        inventory.pickupRoot = cansRoot;
        game.inventory = inventory;
        game.Restarted += ResetCans;
        ResetCans();
    }

    // Стартовый набор: в руке розовый обычный баллон, на стартовой платформе лежат
    // голубой обычный и воздушный. Вызывается и при рестарте (R).
    void ResetCans()
    {
        foreach (Transform child in cansRoot) Destroy(child.gameObject);
        inventory.Clear();
        inventory.Give(SprayCan.Regular(new Color(1f, 0.2f, 0.6f)));

        CanPickup.Spawn(SprayCan.Regular(new Color(0.2f, 0.9f, 1f)), new Vector3(-1.6f, 0f, 5.5f), Quaternion.identity, false, cansRoot);
        CanPickup.Spawn(SprayCan.Air(), new Vector3(1.6f, 0f, 8f), Quaternion.identity, false, cansRoot);
    }

    FirstPersonController BuildPlayer(Vector3 spawn, out PaintSpray spray)
    {
        var playerGo = new GameObject("Player");
        playerGo.layer = 2; // "Ignore Raycast": луч баллончика не попадает в самого игрока
        playerGo.transform.position = spawn;

        var cc = playerGo.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f; // граффити на стене выступает на 0.3 м — на нём должно хватать места встать
        cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.35f;
        cc.slopeLimit = 50f;

        // Камера: берём Main Camera из сцены, если есть, иначе создаём.
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        cam.transform.SetParent(playerGo.transform, false);
        cam.transform.localPosition = new Vector3(0, 1.6f, 0);
        cam.transform.localRotation = Quaternion.identity;
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 75f;

        var fpc = playerGo.AddComponent<FirstPersonController>();
        fpc.cameraPivot = cam.transform;

        // Инвентарь — до баллончика: PaintSpray берёт его в Awake.
        inventory = playerGo.AddComponent<CanInventory>();
        inventory.cam = cam;

        spray = playerGo.AddComponent<PaintSpray>();
        spray.cam = cam;
        spray.inventory = inventory;

        // Подсказка, частицы, звук, баллончик в руке. Можно выключить — логика не пострадает.
        playerGo.AddComponent<SprayEffects>();

        return fpc;
    }

    GameObject Box(string name, Vector3 center, Vector3 size, Color color, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(levelRoot, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = ProtoMaterials.Get(color);
        if (!collider) Destroy(go.GetComponent<Collider>());
        return go;
    }
}
