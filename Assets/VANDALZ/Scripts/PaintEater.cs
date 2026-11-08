using System;
using UnityEngine;

// ЗАКРАСЧИК — серый монстр из краски с валиком.
//  - бродит по своей платформе (area);
//  - замечает свежую краску рядом (senseRadius), ползёт к ней и закатывает валиком — мазок сереет и исчезает;
//  - если игрок подошёл вплотную — отталкивает его (можно улететь в пропасть).
// Анимация: ходит на ногах, тело колышется, руки покачивают валиками, глаза следят за игроком, капли тянутся.
public class PaintEater : MonoBehaviour
{
    [Header("Где ходит")]
    public Vector2 areaMinXZ;
    public Vector2 areaMaxXZ;
    public float floorY;

    [Header("Поведение")]
    public float patrolSpeed = 1.3f;
    public float chaseSpeed = 2.3f;
    public float senseRadius = 6.5f;
    [Tooltip("Достаёт валиком: по горизонтали")]
    public float eraseReach = 1.1f;
    [Tooltip("Достаёт валиком: по высоте (уступы выше — в безопасности)")]
    public float eraseHeight = 1.7f;
    public float pushForce = 9f;

    public FirstPersonController player;
    public CameraRig cameraRig;

    // Игрока толкнули (для интерфейса: вспышка, сообщение).
    public static event Action PlayerHit;

    Transform body, roller, rollerPivot;
    Vector3 patrolTarget;
    PaintStroke target;
    float scanTimer, chewTimer, hitCooldown, growlCooldown;
    float rollAngle;
    AudioSource rollSound;
    Vector3 lastPos;

    static readonly Color Gray = new Color(0.42f, 0.43f, 0.46f);

    public static PaintEater Create(Transform parent, Vector3 position, Vector2 areaMin, Vector2 areaMax, FirstPersonController player, CameraRig rig)
    {
        var go = new GameObject("Закрасчик");
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var e = go.AddComponent<PaintEater>();
        e.areaMinXZ = areaMin;
        e.areaMaxXZ = areaMax;
        e.floorY = position.y;
        e.player = player;
        e.cameraRig = rig;
        e.BuildModel();
        return e;
    }

    // ---------- Модель: высокий гуманоид из стекающей серой краски ----------

    class Eye { public Transform pivot; public Vector3 basePos; }
    class Hanging { public Transform t; public Vector3 anchorLocal; public Transform parent; public float length, width, phase; }

    Transform legL, legR, kneeL, kneeR, armRaised, armDown, head;
    readonly System.Collections.Generic.List<Eye> eyes = new System.Collections.Generic.List<Eye>();
    readonly System.Collections.Generic.List<Hanging> hangs = new System.Collections.Generic.List<Hanging>();
    float walkPhase;

    void BuildModel()
    {
        var paint = new Material(ProtoMaterials.Get(Gray));
        if (paint.HasProperty("_Glossiness")) paint.SetFloat("_Glossiness", 0.75f);
        if (paint.HasProperty("_Smoothness")) paint.SetFloat("_Smoothness", 0.75f);
        ProtoMaterials.SetOutline(paint, 2.6f, new Color(0.03f, 0.03f, 0.05f), true);
        var dark = ProtoMaterials.Get(new Color(0.3f, 0.3f, 0.33f));

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        // Ноги: бедро на шарнире, голень на «колене».
        legL = Joint(body, new Vector3(-0.24f, 1.15f, 0f));
        legR = Joint(body, new Vector3(0.24f, 1.15f, 0f));
        foreach (var leg in new[] { legL, legR })
        {
            Part(PrimitiveType.Capsule, leg, new Vector3(0f, -0.3f, 0f), new Vector3(0.26f, 0.34f, 0.26f), paint);
            var knee = Joint(leg, new Vector3(0f, -0.6f, 0f));
            Part(PrimitiveType.Capsule, knee, new Vector3(0f, -0.28f, 0f), new Vector3(0.22f, 0.32f, 0.22f), paint);
            Part(PrimitiveType.Sphere, knee, new Vector3(0f, -0.55f, 0.08f), new Vector3(0.3f, 0.14f, 0.4f), paint); // ступня-лужица
            if (leg == legL) kneeL = knee; else kneeR = knee;
        }

        // Туловище — сутулое, из нескольких наплывов краски.
        Part(PrimitiveType.Capsule, body, new Vector3(0f, 1.6f, 0f), new Vector3(0.66f, 0.58f, 0.46f), paint).transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
        Part(PrimitiveType.Sphere, body, new Vector3(0f, 1.95f, 0.08f), new Vector3(0.82f, 0.62f, 0.56f), paint);
        Part(PrimitiveType.Sphere, body, new Vector3(0.18f, 1.35f, 0.12f), new Vector3(0.4f, 0.45f, 0.35f), paint);

        // Голова — бесформенный ком, наклонён вперёд.
        head = Joint(body, new Vector3(0f, 2.35f, 0.18f));
        Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.25f, 0.05f), new Vector3(0.6f, 0.68f, 0.58f), paint);
        Part(PrimitiveType.Sphere, head, new Vector3(-0.12f, 0.5f, -0.05f), new Vector3(0.32f, 0.3f, 0.3f), paint);

        // Руки: одна поднята с валиком над головой, другая катит валик по полу.
        armRaised = Joint(body, new Vector3(-0.46f, 2.05f, 0.05f));
        armRaised.localRotation = Quaternion.Euler(-150f, 0f, -10f);
        Part(PrimitiveType.Capsule, armRaised, new Vector3(0f, -0.55f, 0f), new Vector3(0.18f, 0.58f, 0.18f), paint);
        var topRoller = Joint(armRaised, new Vector3(0f, -1.15f, 0.05f));
        Part(PrimitiveType.Cube, topRoller, new Vector3(0.25f, 0f, 0f), new Vector3(0.5f, 0.05f, 0.05f), dark);
        var topDrum = Part(PrimitiveType.Cylinder, topRoller, new Vector3(0.55f, -0.05f, 0f), new Vector3(0.36f, 0.42f, 0.36f), paint).transform;
        topDrum.localRotation = Quaternion.Euler(0f, 0f, 90f);

        armDown = Joint(body, new Vector3(0.48f, 2.0f, 0.05f));
        armDown.localRotation = Quaternion.Euler(-38f, 0f, 8f);
        Part(PrimitiveType.Capsule, armDown, new Vector3(0f, -0.6f, 0f), new Vector3(0.18f, 0.62f, 0.18f), paint);
        Part(PrimitiveType.Sphere, armDown, new Vector3(0f, -1.22f, 0f), new Vector3(0.24f, 0.24f, 0.24f), paint); // кисть
        // Ручка от кисти к валику у пола.
        Part(PrimitiveType.Cube, body, new Vector3(0.36f, 0.62f, 0.86f), new Vector3(0.05f, 0.7f, 0.05f), dark).transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        Part(PrimitiveType.Cube, body, new Vector3(0f, 0.27f, 1.0f), new Vector3(0.9f, 0.05f, 0.05f), dark);
        rollerPivot = Joint(body, new Vector3(0f, 0.2f, 1.02f));
        roller = Part(PrimitiveType.Cylinder, rollerPivot, Vector3.zero, new Vector3(0.36f, 0.42f, 0.36f), paint).transform;
        roller.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // Глаза: разных размеров и цветов, по голове и груди.
        Color[] iris = { new Color(0.2f, 0.55f, 1f), new Color(1f, 0.85f, 0.15f), new Color(1f, 0.3f, 0.6f), new Color(0.4f, 1f, 0.35f), new Color(1f, 0.5f, 0.15f) };
        AddEye(head, new Vector3(-0.15f, 0.3f, 0.31f), 0.17f, iris[0]);
        AddEye(head, new Vector3(0.14f, 0.38f, 0.28f), 0.13f, iris[1]);
        AddEye(head, new Vector3(0.03f, 0.12f, 0.33f), 0.11f, iris[2]);
        AddEye(head, new Vector3(0.24f, 0.17f, 0.24f), 0.09f, iris[3]);
        AddEye(head, new Vector3(-0.27f, 0.12f, 0.22f), 0.08f, iris[4]);
        AddEye(head, new Vector3(-0.05f, 0.52f, 0.2f), 0.07f, iris[3]);
        AddEye(body, new Vector3(0.2f, 2.0f, 0.36f), 0.1f, iris[1]);
        AddEye(body, new Vector3(-0.22f, 1.82f, 0.33f), 0.08f, iris[2]);
        AddEye(body, new Vector3(0.06f, 1.6f, 0.3f), 0.07f, iris[0]);

        // Свисающие капли краски.
        Hang(head, new Vector3(0.2f, 0f, 0.25f), 0.3f, 0.06f, paint);
        Hang(head, new Vector3(-0.22f, -0.02f, 0.18f), 0.22f, 0.05f, paint);
        Hang(body, new Vector3(0.3f, 1.62f, 0.25f), 0.35f, 0.07f, paint);
        Hang(body, new Vector3(-0.28f, 1.5f, 0.2f), 0.28f, 0.06f, paint);
        Hang(topRoller, new Vector3(0.4f, -0.2f, 0f), 0.5f, 0.06f, paint);
        Hang(topRoller, new Vector3(0.7f, -0.2f, 0f), 0.35f, 0.05f, paint);
        Hang(rollerPivot, new Vector3(0.25f, -0.15f, 0f), 0.12f, 0.05f, paint);
        Hang(armDown, new Vector3(0.05f, -1.3f, 0f), 0.25f, 0.05f, paint);

        rollSound = gameObject.AddComponent<AudioSource>();
        rollSound.clip = ProcAudio.EnemyRoll;
        rollSound.loop = true;
        rollSound.spatialBlend = 1f;
        rollSound.minDistance = 2f;
        rollSound.maxDistance = 20f;
        rollSound.volume = 0f;
        rollSound.Play();
    }

    static Transform Joint(Transform parent, Vector3 localPos)
    {
        var t = new GameObject("Joint").transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    void AddEye(Transform parent, Vector3 pos, float size, Color irisColor)
    {
        var pivot = Joint(parent, pos);
        Part(PrimitiveType.Sphere, pivot, Vector3.zero, Vector3.one * size, ProtoMaterials.Get(new Color(0.96f, 0.95f, 0.88f)));
        var irisMat = ProtoMaterials.Get(irisColor);
        Part(PrimitiveType.Sphere, pivot, new Vector3(0f, 0f, size * 0.32f), new Vector3(size * 0.62f, size * 0.62f, size * 0.4f), irisMat);
        Part(PrimitiveType.Sphere, pivot, new Vector3(0f, 0f, size * 0.47f), new Vector3(size * 0.3f, size * 0.3f, size * 0.12f), ProtoMaterials.Get(new Color(0.02f, 0.02f, 0.03f)));
        eyes.Add(new Eye { pivot = pivot, basePos = pos });
    }

    void Hang(Transform parent, Vector3 anchor, float length, float width, Material mat)
    {
        var go = Part(PrimitiveType.Sphere, parent, anchor, new Vector3(width, length, width), mat);
        hangs.Add(new Hanging { t = go.transform, anchorLocal = anchor, parent = parent, length = length, width = width, phase = UnityEngine.Random.value * 10f });
    }

    static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    void Start()
    {
        lastPos = transform.position;
        PickPatrolTarget();
    }

    void PickPatrolTarget()
    {
        patrolTarget = new Vector3(UnityEngine.Random.Range(areaMinXZ.x, areaMaxXZ.x), floorY, UnityEngine.Random.Range(areaMinXZ.y, areaMaxXZ.y));
    }

    Vector3 ClampToArea(Vector3 p)
    {
        return new Vector3(Mathf.Clamp(p.x, areaMinXZ.x, areaMaxXZ.x), floorY, Mathf.Clamp(p.z, areaMinXZ.y, areaMaxXZ.y));
    }

    void Update()
    {
        float dt = Time.deltaTime;
        hitCooldown -= dt;
        growlCooldown -= dt;

        // Ищем ближайшую краску, до которой можно дотянуться валиком.
        scanTimer -= dt;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.3f;
            PaintStroke best = null;
            float bestD = senseRadius;
            foreach (var s in PaintStroke.All)
            {
                if (s == null || s.IsErasing || s.Count == 0) continue;
                Vector3 p = s.ClosestPoint(transform.position);
                if (p.y < floorY - 0.6f || p.y > floorY + eraseHeight) continue; // не достать
                Vector3 reach = ClampToArea(p);
                float d = Vector3.Distance(new Vector3(reach.x, 0f, reach.z), new Vector3(transform.position.x, 0f, transform.position.z))
                          + Vector2.Distance(new Vector2(reach.x, reach.z), new Vector2(p.x, p.z)); // краска за краем платформы — дальше
                if (d < bestD) { bestD = d; best = s; }
            }
            if (best != null && target == null && growlCooldown <= 0f)
            {
                ProcAudio.PlayAt(ProcAudio.EnemyGrowl, transform.position, 0.9f);
                growlCooldown = 4f;
            }
            target = best;
        }

        // Куда идём.
        Vector3 dest;
        float speed;
        if (target != null && !target.IsErasing)
        {
            dest = ClampToArea(target.ClosestPoint(transform.position));
            speed = chaseSpeed;
        }
        else
        {
            target = null;
            if (Vector3.Distance(transform.position, patrolTarget) < 0.4f) PickPatrolTarget();
            dest = patrolTarget;
            speed = patrolSpeed;
        }

        Vector3 to = dest - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 200f * dt);
            // Едет туда, куда смотрит (плавные развороты).
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, to.normalized));
            transform.position = ClampToArea(transform.position + transform.forward * (speed * facing * dt));
        }

        // Закатываем краску у валика.
        Vector3 rollerPos = rollerPivot.position;
        chewTimer -= dt;
        foreach (var s in PaintStroke.All)
        {
            if (s == null || s.IsErasing || s.Count == 0) continue;
            Vector3 p = s.ClosestPoint(rollerPos);
            Vector2 flat = new Vector2(p.x - rollerPos.x, p.z - rollerPos.z);
            if (flat.magnitude <= eraseReach && p.y >= floorY - 0.6f && p.y <= floorY + eraseHeight)
            {
                s.Erase();
                if (chewTimer <= 0f)
                {
                    ProcAudio.PlayAt(ProcAudio.Erase, p, 1f);
                    chewTimer = 0.25f;
                }
                break; // по одному мазку за кадр
            }
        }

        // Толкаем игрока.
        if (player != null && hitCooldown <= 0f)
        {
            Vector3 d = player.transform.position - transform.position;
            if (Mathf.Abs(d.y) < 1.8f && new Vector2(d.x, d.z).magnitude < 1.15f)
            {
                Vector3 away = new Vector3(d.x, 0f, d.z).normalized;
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                player.AddImpulse(away * pushForce + Vector3.up * 5f);
                if (cameraRig != null) cameraRig.AddTrauma(0.7f);
                ProcAudio.Play2D(ProcAudio.Hurt, 0.9f);
                hitCooldown = 1.2f;
                PlayerHit?.Invoke();
            }
        }

        Animate(dt);
    }

    void Animate(float dt)
    {
        float moved = Vector3.Distance(transform.position, lastPos);
        lastPos = transform.position;
        float moving = Mathf.Clamp01(moved / Mathf.Max(0.0001f, dt) / chaseSpeed);
        float t = Time.time;

        // Шаги: фаза по пройденному пути.
        walkPhase += moved * 2.6f;
        float sw = Mathf.Sin(walkPhase) * moving;
        legL.localRotation = Quaternion.Euler(sw * 32f, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-sw * 32f, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Sin(walkPhase)) * 40f * moving, 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(walkPhase)) * 40f * moving, 0f, 0f);

        // Тело колышется, как краска; при ходьбе подпрыгивает.
        float wobble = Mathf.Sin(t * 3.1f);
        body.localScale = new Vector3(1f + wobble * 0.03f, 1f - wobble * 0.025f, 1f + wobble * 0.03f);
        body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Cos(walkPhase)) * 0.06f * moving, 0f);
        head.localRotation = Quaternion.Euler(10f + Mathf.Sin(t * 1.7f) * 6f, Mathf.Sin(t * 0.9f) * 12f, Mathf.Sin(t * 1.3f) * 5f);

        // Руки: поднятая покачивает валиком, нижняя катит.
        armRaised.localRotation = Quaternion.Euler(-150f + Mathf.Sin(t * 1.4f) * 8f, 0f, -10f + Mathf.Sin(t * 0.8f) * 6f);
        armDown.localRotation = Quaternion.Euler(-38f + sw * 6f, 0f, 8f);

        // Нижний валик крутится по пройденному пути.
        rollAngle += moved / (0.18f * Mathf.PI * 2f) * 360f;
        roller.localRotation = Quaternion.Euler(rollAngle, 0f, 90f);

        // Глаза следят за игроком (каждый сам по себе), иногда моргают.
        Vector3 target = player != null ? player.transform.position + Vector3.up * 1.5f : transform.position + transform.forward * 5f;
        bool near = player != null && Vector3.Distance(target, head.position) < 10f;
        for (int i = 0; i < eyes.Count; i++)
        {
            var e = eyes[i];
            Quaternion look = near
                ? Quaternion.LookRotation(target - e.pivot.position, Vector3.up)
                : e.pivot.parent.rotation * Quaternion.Euler(Mathf.Sin(t * 0.7f + i) * 15f, Mathf.Sin(t * 0.5f + i * 2f) * 25f, 0f);
            // Не даём глазу «смотреть внутрь головы».
            Quaternion forward = e.pivot.parent.rotation;
            look = Quaternion.RotateTowards(forward, look, 55f);
            e.pivot.rotation = Quaternion.Slerp(e.pivot.rotation, look, 1f - Mathf.Exp(-(6f + i) * dt));
            float blink = Mathf.Repeat(t + i * 0.73f, 3.7f + i * 0.3f) < 0.12f ? 0.15f : 1f;
            e.pivot.localScale = new Vector3(1f, blink, 1f);
        }

        // Капли тянутся и покачиваются.
        foreach (var h in hangs)
        {
            float stretch = 1f + 0.25f * Mathf.Sin(t * 2.2f + h.phase) + 0.3f * moving;
            float len = h.length * stretch;
            h.t.localScale = new Vector3(h.width, len, h.width);
            // Капля всегда висит строго вниз от точки крепления.
            h.t.position = h.parent.TransformPoint(h.anchorLocal) + Vector3.down * (len * 0.5f);
            h.t.rotation = Quaternion.identity;
        }

        rollSound.volume = Mathf.Lerp(rollSound.volume, 0.5f * moving, 1f - Mathf.Exp(-8f * dt));
        rollSound.pitch = 0.8f + 0.4f * moving;
    }
}
