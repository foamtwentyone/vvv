using UnityEngine;

// Собирает уровень «Станция «Заброшенная»» при запуске игры — всё из кода, без ассетов.
//
// Маршрут (ось Z — вдоль станции, ширина 8 м):
//   A  z  0–12  стартовая платформа: тайник, баллоны, спот «СТАРТ»
//      z 12–20  пропасть над путями (8 м): нужен воздушный баллон или полки на стенах
//   B  z 20–32  средняя платформа: бродит Закрасчик
//      z = 32   стена 3 м: нарисуй полки обычным баллоном и залезь
//   C  z 32–46  верхний ярус (y = 3): спот «ВЕРХ», выход
//
// Атмосфера: туман, тусклый свет, мерцающие лампы, пыль, кафель с грязью, афиши.
public class SubwayLevelBuilder : MonoBehaviour
{
    const float HalfW = 4f;      // половина ширины станции
    const float CeilY = 7f;      // потолок
    const float PitY = -6f;      // дно путей
    const float UpperY = 3f;     // верхний ярус
    const float EndZ = 46f;

    Transform root, decor, cansRoot, gameplayRoot, lampsRoot;
    CanInventory inventory;
    GameFlow flow;
    FirstPersonController player;
    CameraRig rig;

    Material matTiles, matFloor, matSide, matCeiling, matTactile, matRust, matWood, matMetal, matStripe, matUpper, matPoster, matSign, matBrick;

    static readonly Color FogColor = new Color(0.03f, 0.05f, 0.06f);
    static readonly Color LampColor = new Color(1f, 0.8f, 0.52f); // тёплый янтарный свет

    void Awake()
    {
        SetupAtmosphere();
        root = new GameObject("Level").transform;
        decor = new GameObject("Decor").transform;
        decor.SetParent(root, false);
        cansRoot = new GameObject("Cans").transform;
        gameplayRoot = new GameObject("Gameplay").transform; // всё, что двигается или меняется
        lampsRoot = new GameObject("Lamps").transform;

        CreateMaterials();
        BuildStructure();
        BuildTrack();
        BuildDecor();
        BuildRuin();
        BuildLights();
        // Статическое объединение мешей здесь не используем: оно переводит вершины в мировые координаты,
        // и контур мультяшного шейдера («раздувание от центра объекта») перестал бы работать.
        BuildOldTags();
        BuildPlayer(new Vector3(0f, 0.05f, 1.5f));
        BuildGameplay();
    }

    // ---------- Атмосфера ----------

    void SetupAtmosphere()
    {
        // Отключаем «солнце» из стандартной сцены — мы под землёй.
#pragma warning disable 618
        foreach (var l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) l.enabled = false;
#pragma warning restore 618

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.04f;
        RenderSettings.fogColor = FogColor;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.11f, 0.16f, 0.18f); // бирюзовая темнота
        RenderSettings.reflectionIntensity = 0.12f; // не отражать небо в подземелье
        QualitySettings.pixelLightCount = 6;        // ближайшие лампы — попиксельно, остальные дешевле
    }

    void CreateMaterials()
    {
        matTiles = EnvTextures.Make("tiles", EnvTextures.Tiles, Color.white, Vector2.one, 0.45f);
        matFloor = EnvTextures.Make("floor", EnvTextures.Concrete, new Color(0.8f, 0.8f, 0.78f), Vector2.one, 0.15f);
        matSide = EnvTextures.Make("side", EnvTextures.DarkConcrete, Color.white, Vector2.one, 0.1f);
        matCeiling = EnvTextures.Make("ceiling", EnvTextures.DarkConcrete, new Color(0.6f, 0.6f, 0.65f), Vector2.one, 0.05f);
        matUpper = EnvTextures.Make("upper", EnvTextures.Concrete, new Color(0.55f, 0.56f, 0.58f), Vector2.one, 0.1f);
        matTactile = EnvTextures.Make("tactile", EnvTextures.Tactile, Color.white, Vector2.one, 0.3f);
        matRust = EnvTextures.Make("rust", EnvTextures.Rust, Color.white, Vector2.one, 0.3f);
        matWood = EnvTextures.Make("wood", EnvTextures.Wood, Color.white, Vector2.one, 0.1f);
        matPoster = EnvTextures.Make("poster", EnvTextures.Poster, Color.white, Vector2.one, 0.1f);
        matBrick = EnvTextures.Make("brick", EnvTextures.Brick, Color.white, Vector2.one, 0.1f);
        matMetal = ProtoMaterials.Get(new Color(0.28f, 0.29f, 0.32f));
        matStripe = ProtoMaterials.Get(new Color(0.08f, 0.22f, 0.5f));
        matSign = ProtoMaterials.Get(new Color(0.06f, 0.12f, 0.32f));
    }

    // ---------- Конструкция ----------

    void BuildStructure()
    {
        // Платформы: верх — бетонный пол, бока — тёмный бетон.
        Platform("A", 0f, 12f, 0f, true, false);
        Platform("B", 20f, 32f, 0f, true, false);
        Platform("C", 32f, EndZ, UpperY, false, false);

        // Дно путей.
        Block("PitFloor", new Vector3(0f, PitY - 0.5f, 16f), new Vector3(HalfW * 2f, 1f, 8f), matSide, 2f);

        // Боковые стены: кафель внизу, синяя полоса, бетон сверху.
        float wallH = CeilY - PitY;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (HalfW + 0.25f);
            Block("Wall", new Vector3(x, (CeilY + PitY) * 0.5f, EndZ * 0.5f), new Vector3(0.5f, wallH, EndZ + 1f), matTiles, 1.2f, true, 6f);
            float inner = side * (HalfW - 0.02f);
            // Синяя полоса на уровне глаз и бетон под потолком (тонкие панели поверх кафеля).
            Block("Stripe", new Vector3(inner, 1.35f, 6f), new Vector3(0.04f, 0.3f, 12f), matStripe, 1f, false);
            Block("Stripe", new Vector3(inner, 1.35f, 26f), new Vector3(0.04f, 0.3f, 12f), matStripe, 1f, false);
            Block("Stripe", new Vector3(inner, UpperY + 1.35f, 39f), new Vector3(0.04f, 0.3f, 14f), matStripe, 1f, false);
            Block("UpperWall", new Vector3(inner, 5.9f, EndZ * 0.5f), new Vector3(0.04f, 2.2f, EndZ), matUpper, 3f, false, 6f);
        }

        // Торцы.
        Block("BackWall", new Vector3(0f, CeilY * 0.5f, -0.25f), new Vector3(HalfW * 2f, CeilY, 0.5f), matTiles, 1.2f);
        Block("EndWall", new Vector3(0f, (CeilY + UpperY) * 0.5f, EndZ + 0.25f), new Vector3(HalfW * 2f, CeilY - UpperY, 0.5f), matTiles, 1.2f);
        // Стена подъёма на верхний ярус — кафель поверх бетона (по ней рисуют полки).
        Block("ClimbWall", new Vector3(0f, UpperY * 0.5f, 31.97f), new Vector3(HalfW * 2f, UpperY, 0.06f), matTiles, 1.2f, false);

        // Потолок и балки.
        Block("Ceiling", new Vector3(0f, CeilY + 0.25f, EndZ * 0.5f), new Vector3(HalfW * 2f + 1f, 0.5f, EndZ + 1f), matCeiling, 4f, true, 6f);
        for (float z = 2f; z < EndZ; z += 4f)
            Block("Beam", new Vector3(0f, CeilY - 0.2f, z), new Vector3(HalfW * 2f, 0.4f, 0.35f), matSide, 2f, false);

        // Трубы под потолком вдоль стен.
        foreach (float side in new[] { -1f, 1f })
        {
            Pipe(new Vector3(side * 3.55f, 6.35f, EndZ * 0.5f), 0.13f, EndZ, matRust);
            Pipe(new Vector3(side * 3.25f, 6.55f, EndZ * 0.5f), 0.07f, EndZ, matMetal);
        }
    }

    void Platform(string name, float z0, float z1, float topY, bool edgeAtEnd, bool unused)
    {
        float len = z1 - z0;
        float depth = topY - PitY;
        Vector3 center = new Vector3(0f, topY - depth * 0.5f, (z0 + z1) * 0.5f);
        Vector3 size = new Vector3(HalfW * 2f, depth, len);
        Block(name + "_Floor", center, size, matFloor, 3f, true, 6f, Face.Top);
        Block(name + "_Sides", center, size, matSide, 2f, false, 6f, Face.Sides | Face.Bottom);

        // Жёлтая тактильная полоса у края над пропастью.
        if (z0 > 0.1f && topY < 0.1f) Block(name + "_EdgeIn", new Vector3(0f, topY + 0.005f, z0 + 0.35f), new Vector3(HalfW * 2f, 0.01f, 0.6f), matTactile, 0.6f, false);
        if (edgeAtEnd && z1 < 20.1f) Block(name + "_EdgeOut", new Vector3(0f, topY + 0.005f, z1 - 0.35f), new Vector3(HalfW * 2f, 0.01f, 0.6f), matTactile, 0.6f, false);
    }

    void BuildTrack()
    {
        // Шпалы и рельсы на дне пропасти, тусклая красная лампа — видна глубина.
        for (float z = 12.3f; z < 20f; z += 0.7f)
            Block("Sleeper", new Vector3(0f, PitY + 0.06f, z), new Vector3(2.6f, 0.12f, 0.26f), matWood, 1f, false);
        foreach (float x in new[] { -0.75f, 0.75f })
            Block("Rail", new Vector3(x, PitY + 0.2f, 16f), new Vector3(0.1f, 0.16f, 8f), matRust, 1f, false);
        // Контактный рельс у стены.
        Block("ThirdRail", new Vector3(-2.6f, PitY + 0.35f, 16f), new Vector3(0.18f, 0.12f, 8f), matMetal, 1f, false);

        var warn = new GameObject("PitLight").AddComponent<Light>();
        warn.transform.SetParent(root, false);
        warn.transform.position = new Vector3(3.5f, PitY + 1.2f, 16f);
        warn.type = LightType.Point;
        warn.range = 9f;
        warn.intensity = 1.6f;
        warn.color = new Color(1f, 0.25f, 0.12f);
        Block("PitLamp", new Vector3(3.9f, PitY + 1.2f, 16f), new Vector3(0.1f, 0.2f, 0.3f), Emissive(new Color(1f, 0.3f, 0.15f), 2f), 1f, false);
    }

    void BuildDecor()
    {
        // Колонны на платформах A и B.
        foreach (float z in new[] { 3.5f, 9f, 23.5f, 28.5f })
            foreach (float x in new[] { -2.6f, 2.6f })
                Block("Column", new Vector3(x, CeilY * 0.5f, z), new Vector3(0.6f, CeilY, 0.6f), matTiles, 1.2f, true, 0f, Face.All, decor);

        // Скамейки.
        Bench(new Vector3(-3.35f, 0f, 6f), 90f);
        Bench(new Vector3(3.35f, 0f, 25.5f), -90f);
        Bench(new Vector3(3.35f, UpperY, 38f), -90f);

        // Ящики и мусор (на ящик у стены подъёма удобно запрыгнуть).
        Block("Crate", new Vector3(3.2f, 0.35f, 31.3f), new Vector3(0.8f, 0.7f, 0.8f), matWood, 1f, true, 0f, Face.All, decor);
        Block("Crate", new Vector3(-3.3f, 0.3f, 21f), new Vector3(0.6f, 0.6f, 0.6f), matWood, 1f, true, 0f, Face.All, decor);
        Block("Crate", new Vector3(-3.2f, UpperY + 0.4f, 44f), new Vector3(0.8f, 0.8f, 0.8f), matWood, 1f, true, 0f, Face.All, decor);

        // Афиши на стенах.
        Poster(new Vector3(-3.97f, 2.4f, 2.2f), 1f, -3f);
        Poster(new Vector3(3.97f, 2.3f, 6.5f), -1f, 4f);
        Poster(new Vector3(-3.97f, 2.5f, 26f), 1f, 2f);
        Poster(new Vector3(3.97f, 2.4f, 30f), -1f, -5f);
        Poster(new Vector3(3.97f, UpperY + 2.3f, 41.5f), -1f, 3f);

        // Название станции.
        StationSign(new Vector3(3.97f, 3.3f, 2.5f), -1f);
        StationSign(new Vector3(-3.97f, 3.3f, 29.5f), 1f);

        // Подсказки на колоннах (смотрят на игрока, идущего вперёд по +Z).
        Hint(new Vector3(2.6f, 2.3f, 3.18f), "ЛКМ — рисовать\nE — взять баллон");
        Hint(new Vector3(-2.6f, 2.3f, 8.68f), "Впереди пропасть!\nВоздушный баллон\nрисует мост");
        Hint(new Vector3(2.6f, 2.3f, 23.18f), "Закрасчик стирает\nкраску. Не стой\nрядом с ним!");
        Hint(new Vector3(0f, 2.55f, 31.9f), "Нарисуй полки обычным баллоном и залезь наверх", 0.17f);

        // Выход: турникеты, табличка, зелёный свет.
        for (float x = -3.3f; x <= 3.31f; x += 1.65f)
        {
            Block("Turnstile", new Vector3(x, UpperY + 0.5f, 43f), new Vector3(0.25f, 1f, 0.9f), matMetal, 1f, true, 0f, Face.All, decor);
            Block("TurnstileTop", new Vector3(x, UpperY + 1.02f, 43f), new Vector3(0.3f, 0.04f, 0.95f), Emissive(new Color(0.2f, 1f, 0.4f), 0.8f), 1f, false);
        }
        Block("ExitSign", new Vector3(0f, UpperY + 3.2f, 45.9f), new Vector3(2.6f, 0.7f, 0.1f), Emissive(new Color(0.1f, 0.85f, 0.35f), 1.2f), 1f, false);
        WorldText.Create(decor, "ВЫХОД", new Vector3(0f, UpperY + 3.2f, 45.83f), Quaternion.identity, 0.45f, Color.white);
        var exitLight = new GameObject("ExitLight").AddComponent<Light>();
        exitLight.transform.SetParent(root, false);
        exitLight.transform.position = new Vector3(0f, UpperY + 2.5f, 44.8f);
        exitLight.type = LightType.Point;
        exitLight.range = 8f;
        exitLight.intensity = 2.2f;
        exitLight.color = new Color(0.3f, 1f, 0.5f);
    }

    readonly System.Random ruinRandom = new System.Random(42);
    float R(float a, float b) => a + (float)ruinRandom.NextDouble() * (b - a);

    // Разруха: отбитая плитка с кирпичом, обломки у стен, ржавый вагон в тоннеле.
    void BuildRuin()
    {
        var rnd = ruinRandom;

        // Пятна отбитой плитки.
        Vector3[] patches =
        {
            new Vector3(-1f, 1.0f, 10.5f), new Vector3(1f, 3.6f, 4.5f), new Vector3(-1f, 4.2f, 14f),
            new Vector3(1f, -1.5f, 13.5f), new Vector3(-1f, -2.5f, 18f), new Vector3(1f, 0.9f, 28.5f),
            new Vector3(-1f, 3.8f, 24f), new Vector3(1f, 4.8f, 36f), new Vector3(-1f, 5.5f, 44f),
        };
        foreach (var p in patches)
        {
            float side = p.x;
            Patch(new Vector3(side * (HalfW - 0.015f), p.y, p.z), new Vector3(-side, 0f, 0f), R(0.6f, 1.2f), matBrick, rnd);
        }
        Patch(new Vector3(2.5f, 1.6f, 31.95f), Vector3.back, 0.7f, matBrick, rnd);

        // Обломки у стен и у края платформ.
        for (int i = 0; i < 26; i++)
        {
            float z = R(0.5f, EndZ - 0.5f);
            if (z > 11.6f && z < 20.4f) continue;              // не над пропастью
            if (z > 31f && z < 33f) continue;                   // не у стены подъёма
            if (z > 42f && z < 44.5f) continue;                 // не в турникетах
            float side = rnd.NextDouble() > 0.5 ? 1f : -1f;
            float floorY = z > 32f ? UpperY : 0f;
            float s = R(0.12f, 0.35f);
            var chunk = Block("Rubble", new Vector3(side * R(3.3f, 3.8f), floorY + s * 0.4f, z), new Vector3(s * R(0.8f, 1.6f), s, s * R(0.8f, 1.4f)),
                rnd.NextDouble() > 0.5 ? matSide : matTiles, 1f, false, 0f, Face.All, decor);
            chunk.transform.rotation = Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f));
        }

        // Ржавый вагон, застрявший в тоннеле у правой стены пропасти (только декор — крыша ниже зоны падения).
        var car = Block("TrainCar", new Vector3(2.55f, PitY + 1.05f, 16f), new Vector3(2.9f, 2.1f, 7.6f), matRust, 1.5f, true, 0f, Face.All, decor);
        car.transform.rotation = Quaternion.Euler(0f, 0f, 4f);
        var windowMat = Emissive(new Color(1f, 0.75f, 0.35f), 0.6f);
        for (float z = 13.2f; z < 19f; z += 1.4f)
        {
            var w = Block("CarWindow", new Vector3(-1.46f, 0.25f, z - 16f), new Vector3(0.04f, 0.6f, 0.9f), windowMat, 1f, false, 0f, Face.All, car.transform);
            w.transform.localPosition = new Vector3(-1.46f, 0.25f, z - 16f);
        }
        Block("CarStripe", new Vector3(-1.47f, -0.35f, 0f), new Vector3(0.03f, 0.18f, 7.4f), ProtoMaterials.Get(new Color(0.7f, 0.15f, 0.12f)), 1f, false, 0f, Face.All, car.transform)
            .transform.localPosition = new Vector3(-1.47f, -0.35f, 0f);
    }

    // Неровное пятно (многоугольник с «рваным» краем) на стене — например, кирпич под отбитой плиткой.
    void Patch(Vector3 center, Vector3 normal, float radius, Material mat, System.Random rnd)
    {
        const int n = 14;
        Vector3 up = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
        Vector3 right = Vector3.Cross(up, -normal).normalized;
        up = Vector3.Cross(-normal, right).normalized * -1f;
        var verts = new Vector3[n + 1];
        var uvs = new Vector2[n + 1];
        var norms = new Vector3[n + 1];
        verts[0] = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            float r = radius * (0.6f + (float)rnd.NextDouble() * 0.55f);
            verts[i + 1] = (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * r;
        }
        for (int i = 0; i <= n; i++)
        {
            Vector3 w = center + verts[i];
            uvs[i] = new Vector2(Vector3.Dot(w, right), Vector3.Dot(w, up)) / 0.8f;
            norms[i] = normal;
        }
        var tris = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            int a = i + 1, b = (i + 1) % n + 1;
            // Обе стороны — порядок обхода не важен.
            tris[i * 3] = 0; tris[i * 3 + 1] = a; tris[i * 3 + 2] = b;
        }
        var mesh = new Mesh { name = "Patch", vertices = verts, uv = uvs, normals = norms };
        var allTris = new int[tris.Length * 2];
        for (int i = 0; i < tris.Length; i += 3)
        {
            allTris[i] = tris[i]; allTris[i + 1] = tris[i + 1]; allTris[i + 2] = tris[i + 2];
            allTris[tris.Length + i] = tris[i]; allTris[tris.Length + i + 1] = tris[i + 2]; allTris[tris.Length + i + 2] = tris[i + 1];
        }
        mesh.triangles = allTris;
        mesh.RecalculateBounds();
        var go = new GameObject("BrokenTiles");
        go.transform.SetParent(decor, false);
        go.transform.position = center;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = PatchMaterial(mat);
    }

    // Плоские пятна — без контура (контур у плоской двусторонней фигуры даёт «рябь»).
    Material patchMaterial;
    Material PatchMaterial(Material source)
    {
        if (patchMaterial == null)
        {
            patchMaterial = new Material(source);
            ProtoMaterials.SetOutline(patchMaterial, 0f, Color.black);
        }
        return patchMaterial;
    }

    void BuildLights()
    {
        // Лампы дневного света по центру потолка. Две — неисправные (мигают и жужжат).
        int i = 0;
        for (float z = 3f; z < EndZ; z += 5f, i++)
        {
            bool broken = Mathf.Abs(z - 18f) < 0.1f || Mathf.Abs(z - 28f) < 0.1f;
            Vector3 pos = new Vector3(0f, CeilY - 0.45f, z);
            Block("LampHousing", pos + Vector3.up * 0.06f, new Vector3(0.35f, 0.08f, 1.5f), matMetal, 1f, false);
            // Трубка лампы мигает (меняет материал) — держим её вне объединённой геометрии.
            var tube = Block("LampTube", pos, new Vector3(0.14f, 0.05f, 1.35f), Emissive(LampColor, 1.8f), 1f, false, 0f, Face.All, lampsRoot);

            var lightGo = new GameObject("Lamp");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.position = pos + Vector3.down * 0.3f;
            var lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 10f;
            lamp.color = LampColor;
            lamp.intensity = 1.7f;

            var flicker = lightGo.AddComponent<FlickerLight>();
            flicker.lamp = lamp;
            flicker.tube = tube.GetComponent<Renderer>();
            flicker.broken = broken;
            flicker.baseIntensity = 1.7f;
        }
    }

    // ---------- Игрок ----------

    void BuildPlayer(Vector3 spawn)
    {
        var playerGo = new GameObject("Player");
        playerGo.layer = PaintSpray.PlayerLayer; // луч баллончика не попадает в самого игрока
        playerGo.transform.position = spawn;

        var cc = playerGo.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f; // граффити на стене выступает на 0.3 м — на нём должно хватать места встать
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.35f;
        cc.slopeLimit = 50f;

        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        cam.transform.SetParent(playerGo.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cam.transform.localRotation = Quaternion.identity;
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 90f;
        cam.fieldOfView = 75f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = FogColor;

        player = playerGo.AddComponent<FirstPersonController>();
        player.cameraPivot = cam.transform;

        rig = cam.gameObject.AddComponent<CameraRig>();
        cam.gameObject.AddComponent<VandalzPost>(); // свечение неона, цвет, виньетка, зерно
        rig.controller = player;

        // Инвентарь — до баллончика: PaintSpray берёт его в Awake.
        inventory = playerGo.AddComponent<CanInventory>();
        inventory.cam = cam;
        inventory.pickupRoot = cansRoot;

        var spray = playerGo.AddComponent<PaintSpray>();
        spray.cam = cam;
        spray.inventory = inventory;
        playerGo.AddComponent<SprayEffects>();

        // Шаги.
        int step = 0;
        player.Footstep += () => ProcAudio.Play2D(ProcAudio.Footstep(step++), player.IsSprinting ? 0.55f : 0.4f, Random.Range(0.9f, 1.1f));
        player.Jumped += () => ProcAudio.Play2D(ProcAudio.Jump, 0.35f, Random.Range(0.95f, 1.05f));

        DustMotes.Create(playerGo.transform);

        // Ход игры и интерфейс.
        flow = gameObject.AddComponent<GameFlow>();
        flow.player = player;
        flow.spray = spray;
        flow.inventory = inventory;
        flow.cameraRig = rig;

        var hud = gameObject.AddComponent<VandalzHUD>();
        hud.flow = flow;
        hud.spray = spray;
        hud.inventory = inventory;
    }

    // ---------- Игровые объекты ----------

    void BuildGameplay()
    {
        flow.killY = PitY + 2.5f;
        flow.finishZone = new Bounds(new Vector3(0f, UpperY + 1.5f, 44.8f), new Vector3(HalfW * 2f, 3f, 2.4f));
        flow.checkpoints.Add(new GameFlow.Checkpoint { name = "старт", zone = new Bounds(new Vector3(0f, 1f, 6f), new Vector3(8f, 3f, 12f)), spawn = new Vector3(0f, 0.05f, 1.5f), yaw = 0f });
        flow.checkpoints.Add(new GameFlow.Checkpoint { name = "за пропастью", zone = new Bounds(new Vector3(0f, 1f, 26f), new Vector3(8f, 3f, 12f)), spawn = new Vector3(-3.2f, 0.05f, 22f), yaw = 0f }); // вне досягаемости Закрасчика
        flow.checkpoints.Add(new GameFlow.Checkpoint { name = "верхний ярус", zone = new Bounds(new Vector3(0f, UpperY + 1f, 39f), new Vector3(8f, 3f, 14f)), spawn = new Vector3(0f, UpperY + 0.05f, 33f), yaw = 0f });

        // Тайники с краской.
        PaintStash.Create(gameplayRoot, new Vector3(-3.2f, 0f, 1.4f), 90f);
        PaintStash.Create(gameplayRoot, new Vector3(3.2f, 0f, 20.9f), -90f);
        PaintStash.Create(gameplayRoot, new Vector3(-3.2f, UpperY, 34.5f), 90f);

        // Споты для тегов.
        TagSpot.Create(gameplayRoot, new Vector3(-HalfW + 0.02f, 2.1f, 6.8f), Vector3.right, new Vector2(2.4f, 1.4f), "СТАРТ");
        TagSpot.Create(gameplayRoot, new Vector3(HalfW - 0.02f, 1.9f, 16f), Vector3.left, new Vector2(2.6f, 1.4f), "НАД ПУТЯМИ");
        TagSpot.Create(gameplayRoot, new Vector3(-HalfW + 0.02f, UpperY + 2f, 39.5f), Vector3.right, new Vector2(2.4f, 1.4f), "ВЕРХ");

        // Закрасчик на средней платформе.
        PaintEater.Create(gameplayRoot, new Vector3(0f, 0f, 27f), new Vector2(-1.6f, 20.8f), new Vector2(1.6f, 31.2f), player, rig);

        flow.Restarted += ResetCans;
        ResetCans();
    }

    // Стартовый набор баллонов. Вызывается и при рестарте (R).
    void ResetCans()
    {
        foreach (Transform child in cansRoot) Destroy(child.gameObject);
        inventory.Clear();
        inventory.Give(SprayCan.Regular(new Color(1f, 0.2f, 0.6f)));

        CanPickup.Spawn(SprayCan.Regular(new Color(0.2f, 0.9f, 1f)), new Vector3(-3.35f, 0.5f, 6.6f), Quaternion.identity, false, cansRoot);
        CanPickup.Spawn(SprayCan.Air(), new Vector3(2.9f, 0f, 10.6f), Quaternion.identity, false, cansRoot);
        CanPickup.Spawn(SprayCan.Regular(new Color(1f, 0.85f, 0.1f)), new Vector3(3.35f, 0.5f, 25f), Quaternion.identity, false, cansRoot);
    }

    // ---------- Конструктор геометрии ----------

    [System.Flags]
    enum Face { PosX = 1, NegX = 2, PosZ = 4, NegZ = 8, Top = 16, Bottom = 32, Sides = 15, All = 63 }

    // Коробка из своего меша: UV в метрах (tile — размер одного повтора текстуры), поэтому текстура
    // не растягивается на больших объектах. chunk > 0 — режем по длине (Z) на куски, чтобы
    // каждый кусок освещался ближайшими лампами.
    GameObject Block(string name, Vector3 center, Vector3 size, Material mat, float tile,
        bool collider = true, float chunk = 0f, Face faces = Face.All, Transform parent = null)
    {
        if (chunk > 0f && size.z > chunk * 1.5f)
        {
            int n = Mathf.CeilToInt(size.z / chunk);
            float part = size.z / n;
            GameObject first = null;
            for (int i = 0; i < n; i++)
            {
                Vector3 c = new Vector3(center.x, center.y, center.z - size.z * 0.5f + part * (i + 0.5f));
                Face f = faces;
                if (i > 0) f &= ~Face.NegZ;        // внутренние торцы кусков не нужны
                if (i < n - 1) f &= ~Face.PosZ;
                var go = Block(name, c, new Vector3(size.x, size.y, part), mat, tile, collider, 0f, f, parent);
                if (first == null) first = go;
            }
            return first;
        }

        var obj = new GameObject(name);
        obj.transform.SetParent(parent != null ? parent : root, false);
        obj.transform.position = center;

        var mesh = new Mesh { name = name };
        var verts = new System.Collections.Generic.List<Vector3>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        Vector3 h = size * 0.5f;

        if ((faces & Face.PosX) != 0) AddFace(verts, norms, uvs, tris, center, h, tile, Vector3.right, Vector3.up);
        if ((faces & Face.NegX) != 0) AddFace(verts, norms, uvs, tris, center, h, tile, Vector3.left, Vector3.up);
        if ((faces & Face.PosZ) != 0) AddFace(verts, norms, uvs, tris, center, h, tile, Vector3.forward, Vector3.up);
        if ((faces & Face.NegZ) != 0) AddFace(verts, norms, uvs, tris, center, h, tile, Vector3.back, Vector3.up);
        if ((faces & Face.Top) != 0) AddFace(verts, norms, uvs, tris, center, h, tile, Vector3.up, Vector3.forward);
        if ((faces & Face.Bottom) != 0) AddFace(verts, norms, uvs, tris, center, h, tile, Vector3.down, Vector3.forward);

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterial = mat;

        if (collider)
        {
            // Центр задаём явно: Unity подгоняет коллайдер под меш, а у меша могут быть
            // не все грани (например, только верх пола) — тогда коробка «уезжает».
            var box = obj.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
        }
        return obj;
    }

    static void AddFace(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector3> norms,
        System.Collections.Generic.List<Vector2> uvs, System.Collections.Generic.List<int> tris,
        Vector3 center, Vector3 h, float tile, Vector3 n, Vector3 v)
    {
        Vector3 u = Vector3.Cross(v, -n); // «вправо», если смотреть на грань снаружи
        float hn = Mathf.Abs(n.x) * h.x + Mathf.Abs(n.y) * h.y + Mathf.Abs(n.z) * h.z;
        float hu = Mathf.Abs(u.x) * h.x + Mathf.Abs(u.y) * h.y + Mathf.Abs(u.z) * h.z;
        float hv = Mathf.Abs(v.x) * h.x + Mathf.Abs(v.y) * h.y + Mathf.Abs(v.z) * h.z;
        int s = verts.Count;
        Vector3[] corners =
        {
            n * hn - u * hu - v * hv,
            n * hn + u * hu - v * hv,
            n * hn + u * hu + v * hv,
            n * hn - u * hu + v * hv,
        };
        foreach (var c in corners)
        {
            verts.Add(c);
            norms.Add(n);
            Vector3 w = center + c;
            uvs.Add(new Vector2(Vector3.Dot(w, u) / tile, Vector3.Dot(w, v) / tile));
        }
        // По часовой стрелке, если смотреть снаружи, — лицевая сторона.
        tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
        tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
    }

    // Плоская картинка с UV 0..1 (афиши, таблички). normal — куда смотрит лицевая сторона.
    GameObject Picture(string name, Vector3 center, Vector3 normal, float width, float height, Material mat, float tiltDeg)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(decor, false);
        go.transform.position = center;
        // Лицевая сторона Quad смотрит на -Z.
        go.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(0f, 0f, tiltDeg);
        go.transform.localScale = new Vector3(width, height, 1f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    void Poster(Vector3 pos, float facing, float tilt)
    {
        Picture("Poster", pos, new Vector3(facing, 0f, 0f), 1.05f, 1.55f, matPoster, tilt);
    }

    void StationSign(Vector3 pos, float facing)
    {
        Vector3 n = new Vector3(facing, 0f, 0f);
        Picture("StationSign", pos, n, 3.2f, 0.6f, matSign, 0f);
        WorldText.Create(decor, "ЗАБРОШЕННАЯ", pos + n * 0.02f, Quaternion.LookRotation(-n, Vector3.up), 0.32f, new Color(0.95f, 0.95f, 0.9f));
    }

    // Подсказка неоновым граффити (с чёрной обводкой), смотрит на игрока, идущего по +Z.
    static readonly Color[] HintColors = { new Color(0.75f, 1f, 0.2f), new Color(0.25f, 0.95f, 1f), new Color(1f, 0.35f, 0.7f), new Color(1f, 0.88f, 0.2f) };
    int hintIndex;

    void Hint(Vector3 pos, string text, float height = 0.1f)
    {
        Color c = HintColors[hintIndex++ % HintColors.Length];
        float tilt = (hintIndex % 2 == 0 ? 1f : -1f) * 3f;
        WorldText.CreateOutlined(decor, text, pos + Vector3.back * 0.01f, Quaternion.Euler(0f, 0f, tilt), height, c, height * 0.07f);
    }

    // Старый нарисованный тег (декор): объёмный неоновый мазок по точкам. Закрасчик его не трогает.
    void DecorStroke(Vector3[] pts, Vector3 normal, float width, Color color, float depth = 0.1f)
    {
        var go = new GameObject("OldTag");
        go.transform.SetParent(gameplayRoot, false);
        var st = go.AddComponent<PaintStroke>();
        st.Init(color, StrokeStyle.Raised, depth, false, 0f);
        st.MarkDecor();
        foreach (var p in pts) st.AddPoint(p + normal * 0.02f, normal, width);
    }

    // Декор: лестница-подсказка (как на концепте), стрелка к воздушному баллону, тег у старта.
    void BuildOldTags()
    {
        Color cyan = new Color(0.25f, 0.95f, 1f), lime = new Color(0.7f, 1f, 0.2f), pink = new Color(1f, 0.3f, 0.65f);

        // Лестница на правой стене у старта: две стойки и перекладины.
        Vector3 n = Vector3.left;
        float x = HalfW - 0.02f;
        DecorStroke(new[] { new Vector3(x, 0.3f, 4.75f), new Vector3(x, 1.4f, 4.73f), new Vector3(x, 2.6f, 4.77f) }, n, 0.1f, cyan);
        DecorStroke(new[] { new Vector3(x, 0.3f, 5.5f), new Vector3(x, 1.4f, 5.52f), new Vector3(x, 2.6f, 5.48f) }, n, 0.1f, cyan);
        for (float y = 0.6f; y < 2.5f; y += 0.5f)
            DecorStroke(new[] { new Vector3(x, y, 4.75f), new Vector3(x, y + 0.02f, 5.12f), new Vector3(x, y, 5.5f) }, n, 0.09f, cyan);
        WorldText.CreateOutlined(decor, "полки = лестница", new Vector3(x - 0.03f, 2.95f, 5.1f), Quaternion.LookRotation(Vector3.right, Vector3.up) * Quaternion.Euler(0f, 0f, -4f), 0.13f, cyan);

        // Стрелка на полу к воздушному баллону.
        Vector3 up = Vector3.up;
        DecorStroke(new[] { new Vector3(-0.2f, 0.01f, 9.2f), new Vector3(0.8f, 0.01f, 9.7f), new Vector3(1.8f, 0.01f, 10.2f) }, up, 0.22f, lime, 0.04f);
        DecorStroke(new[] { new Vector3(1.3f, 0.01f, 10.45f), new Vector3(1.85f, 0.01f, 10.22f), new Vector3(1.55f, 0.01f, 9.7f) }, up, 0.2f, lime, 0.04f);

        // Розовый тег-завитушка на задней стене у старта.
        var tag = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i <= 18; i++)
        {
            float t = i / 18f;
            tag.Add(new Vector3(-1.2f + t * 2.4f, 2.4f + Mathf.Sin(t * Mathf.PI * 3f) * 0.35f + t * 0.2f, 0.02f));
        }
        DecorStroke(tag.ToArray(), Vector3.forward, 0.16f, pink);
    }

    void Bench(Vector3 pos, float yaw)
    {
        var parent = new GameObject("Bench").transform;
        parent.SetParent(decor, false);
        parent.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seat.transform.SetParent(parent, false);
        seat.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        seat.transform.localScale = new Vector3(1.8f, 0.07f, 0.5f);
        seat.GetComponent<Renderer>().sharedMaterial = matWood;
        foreach (float x in new[] { -0.75f, 0.75f })
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(leg.GetComponent<Collider>());
            leg.transform.SetParent(parent, false);
            leg.transform.localPosition = new Vector3(x, 0.21f, 0f);
            leg.transform.localScale = new Vector3(0.06f, 0.42f, 0.45f);
            leg.GetComponent<Renderer>().sharedMaterial = matMetal;
        }
    }

    void Pipe(Vector3 center, float radius, float length, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Pipe";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(decor, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material Emissive(Color color, float strength)
    {
        var m = new Material(ProtoMaterials.Get(color));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * strength);
        return m;
    }
}
