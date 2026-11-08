using UnityEngine;

// «Ощущение» баллончика. В игровую логику не вмешивается, только показывает:
//  - подсказку: полупрозрачную кляксу там, куда ляжет краска;
//  - руку-дудл с баллоном и её анимации: покачивание при ходьбе, инерция при повороте,
//    прыжок/приземление, поза распыления, смена баллона, встряхивание (F);
//  - струю краски и облачка (частицы);
//  - шипение, «кашель» пустеющего баллона, щелчок пустого, дребезг шарика;
//  - лёгкую тряску камеры при распылении (через CameraRig).
[RequireComponent(typeof(PaintSpray))]
public class SprayEffects : MonoBehaviour
{
    [Header("Подсказка")]
    public bool showPreview = true;

    [Header("Частицы")]
    public float streamRate = 220f;
    public float streamSpeed = 14f;
    public float streamSpread = 0.06f;

    [Header("Звук")]
    public float hissVolume = 0.35f;

    [Header("Тряска")]
    public float sprayShake = 0.06f;

    const int PlayerLayer = 2;

    PaintSpray spray;
    FirstPersonController controller;
    CameraRig rig;
    Transform camT;

    Transform ghost;
    Material ghostMaterial;

    // Рука с баллоном.
    Transform hand, can, nozzle, capTransform;
    Renderer canCap, canBody, canStripe;
    readonly Vector3 handBasePos = new Vector3(0.26f, -0.27f, 0.5f);
    readonly Vector3 handBaseEuler = new Vector3(-6f, -14f, 4f);
    Vector2 sway, swayVelocity;
    float sprayPose;      // 0..1 — насколько рука в позе распыления
    float sprintPose;
    float kick, kickVelocity; // пружина прыжка/приземления
    SprayCan shownCan;
    float swapT = 1f;     // 0..1 анимация смены баллона
    float shakeT = 1f;    // 0..1 анимация встряхивания

    ParticleSystem particles;
    float emitAccumulator;

    AudioSource hissSource;
    float hissLevel;

    void Start()
    {
        spray = GetComponent<PaintSpray>();
        controller = GetComponent<FirstPersonController>();
        if (spray.cam == null) { enabled = false; return; }
        camT = spray.cam.transform;
        rig = spray.cam.GetComponent<CameraRig>();

        BuildGhost();
        BuildHand();
        BuildParticles();
        BuildAudio();

        spray.SpotPlaced += OnSpotPlaced;
        if (controller != null)
        {
            controller.Jumped += OnJumped;
            controller.Landed += OnLanded;
        }
    }

    void OnDestroy()
    {
        if (spray != null) spray.SpotPlaced -= OnSpotPlaced;
        if (controller != null)
        {
            controller.Jumped -= OnJumped;
            controller.Landed -= OnLanded;
        }
    }

    void OnJumped() => kickVelocity += 0.9f;
    void OnLanded(float speed) => kickVelocity -= Mathf.Clamp(speed * 0.18f, 0.6f, 2.5f);

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        bool spraying = spray.IsSpraying;
        Color color = spray.CurrentColor;

        UpdateGhost(color, spraying);
        UpdateHand(color, spraying, dt);
        UpdateStream(color, spraying, dt);
        UpdateAudio(spraying, dt);

        if (rig != null) rig.continuousShake = spraying ? sprayShake : 0f;
    }

    // ---------- Подсказка ----------

    void BuildGhost()
    {
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
        bool visible = showPreview && spray.HasPreview && spray.PreviewMode != SprayMode.None;
        ghost.gameObject.SetActive(visible);
        if (!visible) return;

        Vector3 n = spray.PreviewNormal;
        // Клякса медленно вращается — видно, что это подсказка, а не краска.
        ghost.SetPositionAndRotation(spray.PreviewPosition + n * 0.045f,
            Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, Time.time * 40f, 0f));
        float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 5f);
        ghost.localScale = Vector3.one * (spray.PreviewWidth * 1.2f * pulse);

        Color c;
        if (spray.PreviewBlocked) c = new Color(0.6f, 0.6f, 0.6f, 0.25f);
        else
        {
            c = color;
            c.a = spraying ? 0.18f : 0.5f;
        }
        ghostMaterial.color = c;
    }

    // ---------- Рука с баллоном ----------

    void BuildHand()
    {
        hand = new GameObject("Hand").transform;
        hand.SetParent(camT, false);
        hand.localPosition = handBasePos;
        hand.localRotation = Quaternion.Euler(handBaseEuler);

        // Игрок — дудл: зелёная рука-«сосиска» с толстым контуром.
        Color glove = new Color(0.45f, 0.88f, 0.3f);
        Color sleeve = new Color(0.4f, 0.82f, 0.27f);

        // Рука уходит за край экрана, кулак-дудл держит баллон.
        Part(PrimitiveType.Cylinder, hand, new Vector3(0.05f, -0.1f, -0.2f), new Vector3(0.11f, 0.17f, 0.11f), sleeve).transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        Part(PrimitiveType.Sphere, hand, new Vector3(0.0f, -0.035f, -0.01f), new Vector3(0.105f, 0.09f, 0.11f), glove);
        Part(PrimitiveType.Sphere, hand, new Vector3(-0.035f, -0.005f, 0.035f), new Vector3(0.05f, 0.045f, 0.05f), glove); // большой палец

        can = new GameObject("Can").transform;
        can.SetParent(hand, false);
        can.localPosition = new Vector3(0f, 0.02f, 0.02f);

        canBody = Part(PrimitiveType.Cylinder, can, new Vector3(0, 0, 0), new Vector3(0.07f, 0.1f, 0.07f), new Color(0.75f, 0.75f, 0.78f));
        canStripe = Part(PrimitiveType.Cylinder, can, new Vector3(0, 0.03f, 0), new Vector3(0.072f, 0.025f, 0.072f), Color.white);
        canCap = Part(PrimitiveType.Cylinder, can, new Vector3(0, 0.12f, 0), new Vector3(0.06f, 0.025f, 0.06f), Color.white);
        capTransform = canCap.transform;
        nozzle = Part(PrimitiveType.Cube, can, new Vector3(0, 0.152f, 0.02f), new Vector3(0.022f, 0.02f, 0.03f), new Color(0.1f, 0.1f, 0.1f)).transform;
        // Указательный палец на распылителе.
        Part(PrimitiveType.Sphere, can, new Vector3(0f, 0.175f, -0.005f), new Vector3(0.04f, 0.03f, 0.06f), glove);
    }

    Renderer Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        DestroyImmediate(go.GetComponent<Collider>()); // рука ни с чем не сталкивается
        go.layer = PlayerLayer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = ProtoMaterials.Get(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    void ApplyCanLook(SprayCan held)
    {
        if (held == null) return;
        canCap.sharedMaterial = ProtoMaterials.Get(held.color);
        canStripe.sharedMaterial = ProtoMaterials.Get(held.color);
        canBody.sharedMaterial = ProtoMaterials.Get(held.type == CanType.Air ? new Color(0.95f, 0.98f, 1f) : new Color(0.72f, 0.72f, 0.76f));
    }

    void UpdateHand(Color color, bool spraying, float dt)
    {
        SprayCan held = spray.CurrentCan;

        // Смена баллона: рука уходит вниз, баллон меняется в нижней точке, рука возвращается.
        if (held != shownCan)
        {
            if (shownCan == null) { shownCan = held; ApplyCanLook(held); swapT = 0.5f; }
            else if (swapT >= 1f) swapT = 0f;
        }
        if (swapT < 1f)
        {
            float before = swapT;
            swapT = Mathf.Min(1f, swapT + dt / 0.35f);
            if (before < 0.5f && swapT >= 0.5f) { shownCan = held; ApplyCanLook(held); }
        }
        can.gameObject.SetActive(shownCan != null);

        // Встряхивание (F).
        if (VandalzInput.ShakePressed() && shownCan != null && spray.InputEnabled && Cursor.lockState == CursorLockMode.Locked && shakeT >= 1f)
        {
            shakeT = 0f;
            ProcAudio.Play2D(ProcAudio.Rattle, 0.7f);
        }
        if (shakeT < 1f) shakeT = Mathf.Min(1f, shakeT + dt / 0.6f);

        float k = 1f - Mathf.Exp(-14f * dt);
        sprayPose = Mathf.Lerp(sprayPose, spraying ? 1f : 0f, k);
        sprintPose = Mathf.Lerp(sprintPose, controller != null && controller.IsSprinting && !spraying ? 1f : 0f, 1f - Mathf.Exp(-8f * dt));

        // Инерция: рука отстаёт от поворота камеры (пружина).
        Vector2 look = controller != null ? controller.LookDelta : Vector2.zero;
        Vector2 swayTarget = Vector2.ClampMagnitude(-look * 0.004f, 0.05f);
        swayVelocity += (swayTarget - sway) * 160f * dt - swayVelocity * 14f * dt;
        sway += swayVelocity * dt;

        // Пружина прыжка/приземления.
        kickVelocity += (-kick * 90f - kickVelocity * 11f) * dt;
        kick += kickVelocity * dt;

        // Покачивание при ходьбе и «дыхание».
        float bobW = controller != null && controller.IsGrounded ? Mathf.Clamp01(controller.HorizontalSpeed / controller.sprintSpeed) : 0f;
        float phase = controller != null ? controller.StepPhase * Mathf.PI * 2f : 0f;
        Vector3 bob = new Vector3(Mathf.Cos(phase) * 0.014f, -Mathf.Abs(Mathf.Sin(phase)) * 0.02f, 0f) * bobW;
        float breathe = Mathf.Sin(Time.time * 1.6f) * 0.004f;

        Vector3 pos = handBasePos + bob + new Vector3(sway.x, sway.y + breathe + kick * 0.05f, 0f);
        Vector3 euler = handBaseEuler + new Vector3(-sway.y * 300f + kick * 8f, sway.x * 300f, -sway.x * 200f);

        // Поза распыления: вперёд к прицелу, наклон, дрожь.
        pos += new Vector3(-0.05f, 0.05f, 0.06f) * sprayPose;
        euler += new Vector3(-20f, -6f, 0f) * sprayPose;
        if (spraying) pos += Random.insideUnitSphere * 0.0025f;

        // Бег: рука опущена и повёрнута.
        pos += new Vector3(0.03f, -0.06f, -0.04f) * sprintPose;
        euler += new Vector3(18f, -20f, 10f) * sprintPose;

        // Смена баллона: опускаем руку вниз по дуге.
        float swapDip = swapT < 1f ? Mathf.Sin(swapT * Mathf.PI) : 0f;
        pos += new Vector3(0f, -0.3f, -0.05f) * swapDip;
        euler += new Vector3(40f, 0f, 0f) * swapDip;

        // Встряхивание: быстрые рывки вверх-вниз.
        if (shakeT < 1f)
        {
            float env = Mathf.Sin(shakeT * Mathf.PI);
            pos += new Vector3(0f, Mathf.Sin(shakeT * Mathf.PI * 14f) * 0.045f * env, 0f);
            euler += new Vector3(Mathf.Sin(shakeT * Mathf.PI * 14f) * 12f * env, 0f, 0f);
        }

        // «Кашель» пустого баллона.
        if (spray.IsSprayingEmpty) pos += Random.insideUnitSphere * 0.003f;

        hand.localPosition = pos;
        hand.localRotation = Quaternion.Euler(euler);

        // Палец давит на распылитель.
        capTransform.localPosition = new Vector3(0f, 0.12f - 0.006f * sprayPose, 0f);
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
        hissSource.clip = ProcAudio.Hiss;
        hissSource.loop = true;
        hissSource.playOnAwake = false;
        hissSource.spatialBlend = 0f;
        hissSource.volume = 0f;
        hissSource.Play();
    }

    void UpdateAudio(bool spraying, float dt)
    {
        float target = spraying ? 1f : 0f;
        float speed = spraying ? 30f : 14f;
        hissLevel = Mathf.MoveTowards(hissLevel, target, speed * dt);

        float vol = hissLevel * hissVolume;
        // Когда краски мало — шипение «кашляет».
        if (spraying && spray.Paint01 < 0.2f)
            vol *= Mathf.PerlinNoise(Time.time * 12f, 3.7f) > 0.45f ? 1f : 0.25f;
        hissSource.volume = vol;
        hissSource.pitch = Mathf.Lerp(0.85f, 1.05f, spray.Paint01);

        // Нажал на пустой баллон — щелчок.
        if (spray.IsSprayingEmpty && VandalzInput.ClickPressed())
            ProcAudio.Play2D(ProcAudio.EmptyClick, 0.6f, Random.Range(0.9f, 1.1f));
    }
}
