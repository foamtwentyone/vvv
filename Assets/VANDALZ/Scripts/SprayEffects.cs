using UnityEngine;

// «Ощущение» баллончика. Ничего не меняет в игровой логике, только показывает:
//  - подсказку: полупрозрачную кляксу там, куда ляжет краска, и надпись под прицелом;
//  - баллончик в руке (наклоняется и дрожит при распылении);
//  - струю краски и облачка (частицы);
//  - шипение (звук генерируется кодом), «пустой» щелчок, когда краска кончилась;
//  - лёгкую тряску камеры.
[RequireComponent(typeof(PaintSpray))]
public class SprayEffects : MonoBehaviour
{
    [Header("Подсказка")]
    public bool showPreview = true;
    public bool showHint = true;

    [Header("Частицы")]
    public float streamRate = 220f;   // частиц в секунду
    public float streamSpeed = 14f;   // м/с
    public float streamSpread = 0.06f;

    [Header("Звук")]
    public float hissVolume = 0.35f;

    [Header("Тряска")]
    public float cameraShake = 0.012f;

    const int PlayerLayer = 2;

    PaintSpray spray;
    Transform camT;
    Vector3 camBasePos;

    Transform ghost;
    Material ghostMaterial;
    GUIStyle hintStyle;

    Transform can, nozzle;
    Renderer canCap;
    Vector3 canBasePos;
    Quaternion canBaseRot;

    ParticleSystem particles;
    float emitAccumulator;

    AudioSource hissSource, clickSource;
    float hissLevel;

    void Start()
    {
        spray = GetComponent<PaintSpray>();
        if (spray.cam == null) { enabled = false; return; }
        camT = spray.cam.transform;
        camBasePos = camT.localPosition;

        BuildGhost();
        BuildCan();
        BuildParticles();
        BuildAudio();

        spray.SpotPlaced += OnSpotPlaced;
    }

    void OnDestroy()
    {
        if (spray != null) spray.SpotPlaced -= OnSpotPlaced;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        bool spraying = spray.IsSpraying;
        Color color = spray.CurrentColor;

        UpdateGhost(color, spraying);
        UpdateCan(color, spraying, dt);
        UpdateStream(color, spraying, dt);
        UpdateAudio(spraying, dt);

        // Тряска камеры (только позиция, поворотом управляет контроллер).
        Vector3 shake = Vector3.zero;
        if (spraying)
        {
            float t = Time.time * 40f;
            shake = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (2f * cameraShake);
        }
        camT.localPosition = camBasePos + shake;
    }

    // ---------- Подсказка ----------

    void BuildGhost()
    {
        // Квадрат 1×1 в плоскости XZ с текстурой кляксы.
        var mesh = new Mesh { name = "SprayGhost" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
        };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();

        var go = new GameObject("SprayPreview");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        ghostMaterial = new Material(PaintTextures.Blob);
        mr.sharedMaterial = ghostMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        ghost = go.transform;
    }

    void UpdateGhost(Color color, bool spraying)
    {
        bool visible = showPreview && spray.HasPreview;
        ghost.gameObject.SetActive(visible);
        if (!visible) return;

        Vector3 n = spray.PreviewNormal;
        ghost.SetPositionAndRotation(spray.PreviewPosition + n * 0.045f, Quaternion.FromToRotation(Vector3.up, n));
        ghost.localScale = Vector3.one * (spray.PreviewWidth * 1.2f);

        Color c;
        if (spray.PreviewBlocked) c = new Color(0.6f, 0.6f, 0.6f, 0.25f);
        else
        {
            c = color;
            // Пока не рисуем — заметная подсказка; при распылении еле видна, чтобы не мешала.
            c.a = spraying ? 0.2f : 0.5f;
        }
        ghostMaterial.color = c;
    }

    void OnGUI()
    {
        if (!showHint || spray == null || !spray.HasPreview) return;
        if (hintStyle == null)
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };

        string text;
        if (spray.PreviewBlocked) text = "тут стоишь ты";
        else switch (spray.PreviewMode)
        {
            case SprayMode.Wall: text = "стена — граффити + уступ"; break;
            case SprayMode.Air: text = "воздух — мост"; break;
            case SprayMode.ExtendBridge: text = "продлить мост"; break;
            case SprayMode.Surface: text = spray.PreviewNormal.y > 0f ? "пол — граффити" : "потолок — граффити"; break;
            default: return;
        }

        var r = new Rect(Screen.width / 2f - 200f, Screen.height / 2f + 14f, 400f, 24f);
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, hintStyle);
        Color c = spray.PreviewBlocked ? Color.gray : spray.CurrentColor;
        c.a = 1f;
        GUI.color = c;
        GUI.Label(r, text, hintStyle);
        GUI.color = old;
    }

    // ---------- Баллончик в руке ----------

    void BuildCan()
    {
        can = new GameObject("SprayCan").transform;
        can.SetParent(camT, false);
        canBasePos = new Vector3(0.23f, -0.24f, 0.45f);
        canBaseRot = Quaternion.Euler(-8f, -12f, 0f);
        can.localPosition = canBasePos;
        can.localRotation = canBaseRot;

        Part(PrimitiveType.Cylinder, can, new Vector3(0, 0, 0), new Vector3(0.07f, 0.1f, 0.07f), new Color(0.75f, 0.75f, 0.78f));
        canCap = Part(PrimitiveType.Cylinder, can, new Vector3(0, 0.12f, 0), new Vector3(0.06f, 0.025f, 0.06f), Color.white);
        nozzle = Part(PrimitiveType.Cube, can, new Vector3(0, 0.15f, 0.02f), new Vector3(0.02f, 0.02f, 0.03f), new Color(0.1f, 0.1f, 0.1f)).transform;
    }

    Renderer Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        DestroyImmediate(go.GetComponent<Collider>()); // баллончик не должен ни с чем сталкиваться
        go.layer = PlayerLayer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = ProtoMaterials.Get(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    void UpdateCan(Color color, bool spraying, float dt)
    {
        canCap.sharedMaterial = ProtoMaterials.Get(color);

        // При распылении баллончик выдвигается вперёд, наклоняется к прицелу и дрожит.
        Vector3 targetPos = spraying ? canBasePos + new Vector3(-0.04f, 0.04f, 0.05f) : canBasePos;
        Quaternion targetRot = spraying ? Quaternion.Euler(-25f, -18f, 0f) : canBaseRot;
        if (spraying) targetPos += Random.insideUnitSphere * 0.004f;

        float k = 1f - Mathf.Exp(-18f * dt);
        can.localPosition = Vector3.Lerp(can.localPosition, targetPos, k);
        can.localRotation = Quaternion.Slerp(can.localRotation, targetRot, k);
    }

    // ---------- Частицы ----------

    void BuildParticles()
    {
        var go = new GameObject("SprayParticles");
        particles = go.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;
        main.startSpeed = 0f;
        main.startLifetime = 0.4f;
        main.startSize = 0.08f;

        var emission = particles.emission;
        emission.enabled = false; // испускаем вручную через Emit
        var shape = particles.shape;
        shape.enabled = false;

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f));

        var colorOverLife = particles.colorOverLifetime;
        colorOverLife.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = g;

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = PaintTextures.Particle;
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;

        particles.Play();
    }

    void UpdateStream(Color color, bool spraying, float dt)
    {
        if (!spraying) { emitAccumulator = 0f; return; }

        Vector3 from = nozzle.position;
        Vector3 to = spray.AimPoint;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;
        dir /= dist;

        emitAccumulator += streamRate * dt;
        var p = new ParticleSystem.EmitParams();
        while (emitAccumulator >= 1f)
        {
            emitAccumulator -= 1f;
            Vector3 d = (dir + Random.insideUnitSphere * streamSpread).normalized;
            float speed = streamSpeed * Random.Range(0.8f, 1.2f);
            p.position = from;
            p.velocity = d * speed;
            p.startLifetime = Mathf.Clamp(dist / speed, 0.05f, 0.6f); // долетает до цели и исчезает
            p.startSize = Random.Range(0.04f, 0.1f);
            p.startColor = color;
            particles.Emit(p, 1);
        }
    }

    void OnSpotPlaced(Vector3 pos, Quaternion rot, Color color)
    {
        // Облачко краски у нового куска мазка.
        Vector3 up = rot * Vector3.up;
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < 4; i++)
        {
            Vector3 v = Vector3.ProjectOnPlane(Random.onUnitSphere, up).normalized * Random.Range(0.6f, 1.6f)
                        + up * Random.Range(0.1f, 0.5f);
            p.position = pos + up * 0.03f;
            p.velocity = v;
            p.startLifetime = Random.Range(0.25f, 0.45f);
            p.startSize = Random.Range(0.1f, 0.2f);
            p.startColor = color;
            particles.Emit(p, 1);
        }
    }

    // ---------- Звук ----------

    void BuildAudio()
    {
        hissSource = gameObject.AddComponent<AudioSource>();
        hissSource.clip = MakeHissClip();
        hissSource.loop = true;
        hissSource.playOnAwake = false;
        hissSource.spatialBlend = 0f;
        hissSource.volume = 0f;
        hissSource.Play();

        clickSource = gameObject.AddComponent<AudioSource>();
        clickSource.clip = MakeEmptyClickClip();
        clickSource.playOnAwake = false;
        clickSource.spatialBlend = 0f;
        clickSource.volume = 0.6f;
    }

    void UpdateAudio(bool spraying, float dt)
    {
        // Быстрая атака, чуть более мягкое затухание.
        float target = spraying ? 1f : 0f;
        float speed = spraying ? 30f : 14f;
        hissLevel = Mathf.MoveTowards(hissLevel, target, speed * dt);

        float vol = hissLevel * hissVolume;
        // Когда краски мало — шипение «кашляет».
        if (spraying && spray.Paint01 < 0.2f)
            vol *= Mathf.PerlinNoise(Time.time * 12f, 3.7f) > 0.45f ? 1f : 0.25f;
        hissSource.volume = vol;
        hissSource.pitch = Mathf.Lerp(0.85f, 1.05f, spray.Paint01);

        // Нажал на пустой баллончик — щелчок.
        if (spray.IsSprayingEmpty && VandalzInput.ClickPressed())
        {
            clickSource.pitch = Random.Range(0.9f, 1.1f);
            clickSource.Play();
        }
    }

    // ---------- Генерация ресурсов кодом ----------

    static AudioClip MakeHissClip()
    {
        // Шипение = белый шум, у которого убраны низкие частоты.
        const int rate = 44100;
        int samples = rate * 2;
        var data = new float[samples];
        float prev = 0f, low = 0f;
        for (int i = 0; i < samples; i++)
        {
            float white = Random.Range(-1f, 1f);
            low += (white - low) * 0.05f;      // низкочастотная часть
            float high = white - low;          // остаётся «ш-ш-ш»
            prev += (high - prev) * 0.7f;      // чуть смягчаем самые резкие верха
            data[i] = prev * 0.5f;
        }
        var clip = AudioClip.Create("SprayHiss", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeEmptyClickClip()
    {
        // Короткий «пшик-щелчок»: два быстро затухающих всплеска шума.
        const int rate = 44100;
        int samples = rate / 6;
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 90f) + 0.5f * Mathf.Exp(-Mathf.Max(0f, t - 0.07f) * 120f) * (t > 0.07f ? 1f : 0f);
            data[i] = Random.Range(-1f, 1f) * env * 0.6f;
        }
        var clip = AudioClip.Create("SprayEmpty", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
