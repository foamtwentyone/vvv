using UnityEngine;

// Эффекты камеры поверх контроллера:
//  - покачивание при ходьбе (сильнее при беге);
//  - «проседание» при приземлении (пружина);
//  - тряска (trauma: удар, толчок, распыление);
//  - угол обзора чуть шире при беге.
// Висит на камере, работает в LateUpdate — после того как контроллер выставил наклон.
public class CameraRig : MonoBehaviour
{
    public FirstPersonController controller;

    public float baseFov = 75f;
    public float sprintFovBoost = 7f;
    public float bobAmount = 0.045f;
    public float landKick = 0.12f;

    [Tooltip("Постоянная лёгкая тряска (например, пока распыляем)")]
    public float continuousShake;

    Camera cam;
    Vector3 basePos;
    float trauma;
    float landOffset, landVelocity;
    float bobWeight;
    float roll;

    void Awake()
    {
        cam = GetComponent<Camera>();
        basePos = transform.localPosition;
    }

    void Start()
    {
        if (controller != null) controller.Landed += OnLanded;
    }

    void OnDestroy()
    {
        if (controller != null) controller.Landed -= OnLanded;
    }

    void OnLanded(float fallSpeed)
    {
        landVelocity -= Mathf.Clamp(fallSpeed * 0.06f, 0.2f, 1.6f) * landKick * 10f;
        AddTrauma(Mathf.Clamp01((fallSpeed - 6f) / 14f) * 0.5f);
    }

    // 0..1 — сила разовой тряски (сама затухает).
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (controller == null) return;

        // Покачивание: восьмёрка, синхронно с шагами.
        float speed01 = Mathf.Clamp01(controller.HorizontalSpeed / controller.sprintSpeed);
        float targetWeight = controller.IsGrounded ? speed01 : 0f;
        bobWeight = Mathf.Lerp(bobWeight, targetWeight, 1f - Mathf.Exp(-10f * dt));
        float phase = controller.StepPhase * Mathf.PI * 2f;
        Vector3 bob = new Vector3(Mathf.Cos(phase) * 0.6f, -Mathf.Abs(Mathf.Sin(phase)), 0f) * (bobAmount * bobWeight);

        // Пружина приземления.
        landVelocity += (-landOffset * 120f - landVelocity * 14f) * dt;
        landOffset += landVelocity * dt;

        // Тряска.
        trauma = Mathf.Max(0f, trauma - dt * 1.5f);
        float shake = trauma * trauma + continuousShake;
        float t = Time.time * 30f;
        Vector3 shakePos = new Vector3(Mathf.PerlinNoise(t, 1.3f) - 0.5f, Mathf.PerlinNoise(2.7f, t) - 0.5f, 0f) * (0.12f * shake);
        float shakeRoll = (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * 6f * shake;

        // Лёгкий крен в сторону движения вбок.
        Vector3 localVel = controller.transform.InverseTransformDirection(controller.Velocity);
        roll = Mathf.Lerp(roll, -localVel.x * 0.35f, 1f - Mathf.Exp(-6f * dt));

        transform.localPosition = basePos + bob + Vector3.up * landOffset + shakePos;
        transform.localRotation = transform.localRotation * Quaternion.Euler(0f, 0f, roll + shakeRoll);

        if (cam != null)
        {
            float targetFov = baseFov + (controller.IsSprinting && controller.HorizontalSpeed > controller.walkSpeed * 0.9f ? sprintFovBoost : 0f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-6f * dt));
        }
    }
}
