using System;
using UnityEngine;

// ЗАКРАСЧИК — серый монстр из краски с валиком.
//  - бродит по своей платформе (area);
//  - замечает свежую краску рядом (senseRadius), ползёт к ней и закатывает валиком — мазок сереет и исчезает;
//  - если игрок подошёл вплотную — отталкивает его (можно улететь в пропасть).
// Анимация: тело колышется и сплющивается, валик крутится, глаза следят за игроком.
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

    Transform body, roller, rollerPivot, pupilL, pupilR, eyeL, eyeR;
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

    void BuildModel()
    {
        var paint = new Material(ProtoMaterials.Get(Gray));
        if (paint.HasProperty("_Glossiness")) paint.SetFloat("_Glossiness", 0.75f);
        if (paint.HasProperty("_Smoothness")) paint.SetFloat("_Smoothness", 0.75f);

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        // Тело — капля краски, пара наплывов и подтёков.
        Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.85f, 0f), new Vector3(1.25f, 1.5f, 1.15f), paint);
        Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.35f, 0f), new Vector3(1.55f, 0.7f, 1.45f), paint);
        Part(PrimitiveType.Sphere, body, new Vector3(0.45f, 0.5f, 0.35f), new Vector3(0.45f, 0.6f, 0.45f), paint);
        Part(PrimitiveType.Sphere, body, new Vector3(-0.5f, 0.6f, -0.2f), new Vector3(0.4f, 0.7f, 0.4f), paint);
        Part(PrimitiveType.Sphere, body, new Vector3(0f, 1.62f, -0.05f), new Vector3(0.5f, 0.35f, 0.5f), paint);

        // Глаза.
        var white = ProtoMaterials.Get(new Color(0.95f, 0.95f, 0.9f));
        var black = ProtoMaterials.Get(new Color(0.05f, 0.05f, 0.06f));
        eyeL = Part(PrimitiveType.Sphere, body, new Vector3(-0.24f, 1.18f, 0.5f), new Vector3(0.3f, 0.36f, 0.2f), white).transform;
        eyeR = Part(PrimitiveType.Sphere, body, new Vector3(0.24f, 1.22f, 0.5f), new Vector3(0.26f, 0.3f, 0.2f), white).transform;
        pupilL = Part(PrimitiveType.Sphere, body, new Vector3(-0.24f, 1.18f, 0.6f), new Vector3(0.12f, 0.14f, 0.06f), black).transform;
        pupilR = Part(PrimitiveType.Sphere, body, new Vector3(0.24f, 1.22f, 0.6f), new Vector3(0.11f, 0.12f, 0.06f), black).transform;

        // Валик на ручке впереди.
        var metal = ProtoMaterials.Get(new Color(0.25f, 0.25f, 0.28f));
        Part(PrimitiveType.Cube, body, new Vector3(0.55f, 0.7f, 0.65f), new Vector3(0.07f, 0.07f, 0.7f), metal).transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        Part(PrimitiveType.Cube, body, new Vector3(0f, 0.38f, 0.98f), new Vector3(1.2f, 0.06f, 0.06f), metal);
        rollerPivot = new GameObject("RollerPivot").transform;
        rollerPivot.SetParent(body, false);
        rollerPivot.localPosition = new Vector3(0f, 0.22f, 1.05f);
        roller = Part(PrimitiveType.Cylinder, rollerPivot, Vector3.zero, new Vector3(0.36f, 0.6f, 0.36f), paint).transform;
        roller.localRotation = Quaternion.Euler(0f, 0f, 90f);

        rollSound = gameObject.AddComponent<AudioSource>();
        rollSound.clip = ProcAudio.EnemyRoll;
        rollSound.loop = true;
        rollSound.spatialBlend = 1f;
        rollSound.minDistance = 2f;
        rollSound.maxDistance = 20f;
        rollSound.volume = 0f;
        rollSound.Play();
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

        // Колыхание и сплющивание.
        float t = Time.time;
        float wobble = Mathf.Sin(t * (5f + moving * 6f));
        body.localScale = new Vector3(1f + wobble * 0.05f, 1f - wobble * 0.07f, 1f + wobble * 0.05f);
        body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 6f)) * 0.04f * moving, 0f);

        // Валик крутится по пройденному пути.
        rollAngle += moved / (0.18f * Mathf.PI * 2f) * 360f;
        roller.localRotation = Quaternion.Euler(rollAngle, 0f, 90f);

        // Глаза следят за игроком, если он рядом.
        Vector3 lookLocal = new Vector3(0f, 0f, 1f);
        if (player != null)
        {
            Vector3 toPlayer = player.transform.position + Vector3.up * 1.5f - eyeL.position;
            if (toPlayer.magnitude < 9f) lookLocal = body.InverseTransformDirection(toPlayer.normalized);
        }
        Vector3 shift = new Vector3(Mathf.Clamp(lookLocal.x, -1f, 1f) * 0.07f, Mathf.Clamp(lookLocal.y, -1f, 1f) * 0.06f, 0f);
        pupilL.localPosition = new Vector3(-0.24f, 1.18f, 0.6f) + shift;
        pupilR.localPosition = new Vector3(0.24f, 1.22f, 0.6f) + shift;
        // Моргание.
        float blink = Mathf.Repeat(t, 4.3f) < 0.12f ? 0.1f : 1f;
        eyeL.localScale = new Vector3(0.3f, 0.36f * blink, 0.2f);
        eyeR.localScale = new Vector3(0.26f, 0.3f * blink, 0.2f);
        pupilL.gameObject.SetActive(blink > 0.5f);
        pupilR.gameObject.SetActive(blink > 0.5f);

        rollSound.volume = Mathf.Lerp(rollSound.volume, 0.5f * moving, 1f - Mathf.Exp(-8f * dt));
        rollSound.pitch = 0.8f + 0.4f * moving;
    }
}
