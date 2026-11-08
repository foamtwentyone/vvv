using System;
using UnityEngine;

// Контроллер от первого лица на CharacterController.
//  - плавный разгон и торможение (на земле быстро, в воздухе слабее);
//  - прыжок с «временем койота» и буфером нажатия;
//  - события шага, прыжка и приземления — для звука, покачивания камеры и анимаций;
//  - внешние толчки (Закрасчик отталкивает игрока).
// Позиция объекта = ступни игрока, камера — дочерний объект на уровне глаз.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public Transform cameraPivot;

    [Header("Движение")]
    public float walkSpeed = 4.6f;
    public float sprintSpeed = 7.2f;
    public float groundAcceleration = 45f;
    public float airAcceleration = 14f;
    public float jumpHeight = 1.35f;
    public float gravity = -24f;

    [Header("Мышь")]
    public float mouseSensitivity = 2f;
    public float maxPitch = 85f;

    [Header("Прощение ошибок")]
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;

    [Header("Шаги")]
    public float stepDistance = 1.9f;

    // Множитель скорости (например, липкий серый след Закрасчика).
    public float SpeedMultiplier { get; set; } = 1f;
    // Сейчас подтягивается на уступ.
    public bool IsMantling => mantleT < 1f;
    public event Action Mantled;

    [Header("Подтягивание на уступ")]
    public float mantleMinHeight = 0.45f;
    public float mantleMaxHeight = 1.75f;
    public float mantleDuration = 0.32f;

    // Управление включено (выключается на заставке и финише).
    public bool InputEnabled { get; set; } = true;
    public Collider GroundCollider { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    // Фаза шага 0..1 (для покачивания камеры и баллона).
    public float StepPhase => stepTravel / stepDistance;
    public float Pitch => pitch;
    // Смещение мыши за кадр (для инерции баллона в руке).
    public Vector2 LookDelta { get; private set; }

    public event Action Footstep;
    public event Action Jumped;
    public event Action<float> Landed; // скорость падения в момент приземления

    CharacterController controller;
    float pitch;
    float verticalVelocity;
    Vector3 horizontalVelocity;
    Vector3 impulse;
    float coyoteTimer;
    float jumpBufferTimer;
    float stepTravel;
    bool wasGrounded = true;
    float lastAirSpeed;
    float mantleT = 1f;
    Vector3 mantleFrom, mantleTo;
    float mantleCooldown;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        LookDelta = Vector2.zero;
        if (InputEnabled && Cursor.lockState == CursorLockMode.Locked) Look();
        // Наклон камеры задаём каждый кадр целиком — эффекты камеры добавляются поверх (CameraRig).
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        Move();
    }

    void Look()
    {
        Vector2 delta = VandalzInput.Look() * mouseSensitivity;
        LookDelta = delta;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
    }

    void Move()
    {
        float dt = Time.deltaTime;
        mantleCooldown -= dt;

        // Подтягивание: плавно вверх, потом вперёд на уступ.
        if (mantleT < 1f)
        {
            mantleT = Mathf.Min(1f, mantleT + dt / mantleDuration);
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(mantleT / 0.7f));
            float fwd = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((mantleT - 0.4f) / 0.6f));
            Vector3 p = new Vector3(Mathf.Lerp(mantleFrom.x, mantleTo.x, fwd), Mathf.Lerp(mantleFrom.y, mantleTo.y, up), Mathf.Lerp(mantleFrom.z, mantleTo.z, fwd));
            controller.enabled = false;
            transform.position = p;
            controller.enabled = true;
            if (mantleT >= 1f)
            {
                verticalVelocity = -2f;
                horizontalVelocity = transform.forward * walkSpeed * 0.5f;
            }
            return;
        }

        Vector2 input = InputEnabled && Cursor.lockState == CursorLockMode.Locked ? VandalzInput.Move() : Vector2.zero;
        IsSprinting = InputEnabled && VandalzInput.SprintHeld() && input.y > 0.1f;
        float speed = (IsSprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier;
        Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

        // Плавный разгон/торможение.
        float accel = IsGrounded ? groundAcceleration : airAcceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wish, accel * dt);

        if (IsGrounded)
        {
            coyoteTimer = coyoteTime;
            if (verticalVelocity < 0f) verticalVelocity = -2f; // прижимаем к полу
        }
        else
        {
            coyoteTimer -= dt;
        }

        if (InputEnabled && VandalzInput.JumpPressed()) jumpBufferTimer = jumpBuffer;
        else jumpBufferTimer -= dt;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            Jumped?.Invoke();
        }

        verticalVelocity += gravity * dt;
        if (!IsGrounded) lastAirSpeed = -verticalVelocity;

        // Идём вперёд на уступ/ящик/полку в пределах досягаемости — подтягиваемся.
        if (input.y > 0.5f && mantleCooldown <= 0f && verticalVelocity < 3f && TryStartMantle()) return;

        // Внешний толчок затухает.
        impulse = Vector3.MoveTowards(impulse, Vector3.zero, 12f * dt);

        GroundCollider = null; // заполнится в OnControllerColliderHit во время Move
        CollisionFlags flags = controller.Move((horizontalVelocity + impulse + Vector3.up * verticalVelocity) * dt);
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f; // ударились головой

        IsGrounded = controller.isGrounded;
        if (IsGrounded && !wasGrounded && lastAirSpeed > 3f) Landed?.Invoke(lastAirSpeed);
        wasGrounded = IsGrounded;

        // Шаги.
        if (IsGrounded && HorizontalSpeed > 0.5f)
        {
            stepTravel += HorizontalSpeed * dt;
            if (stepTravel >= stepDistance)
            {
                stepTravel -= stepDistance;
                Footstep?.Invoke();
            }
        }
    }

    bool TryStartMantle()
    {
        int mask = ~(1 << PaintSpray.PlayerLayer);
        Vector3 pos = transform.position;
        Vector3 fwd = transform.forward;
        float r = controller.radius;

        // Впереди препятствие на уровне пояса или груди?
        bool blocked = Physics.Raycast(pos + Vector3.up * 0.6f, fwd, r + 0.35f, mask, QueryTriggerInteraction.Ignore)
                    || Physics.Raycast(pos + Vector3.up * 1.2f, fwd, r + 0.35f, mask, QueryTriggerInteraction.Ignore);
        if (!blocked) return false;

        // Есть ли сверху площадка, куда встать?
        Vector3 probe = pos + fwd * (r + 0.3f) + Vector3.up * (mantleMaxHeight + 0.3f);
        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, mantleMaxHeight + 0.3f - mantleMinHeight + 0.05f, mask, QueryTriggerInteraction.Ignore)) return false;
        if (top.normal.y < 0.7f) return false;
        float h = top.point.y - pos.y;
        if (h < mantleMinHeight || h > mantleMaxHeight) return false;

        // Хватает ли места встать (и пролезть над краем)?
        Vector3 standAt = top.point + Vector3.up * 0.02f;
        if (Physics.CheckCapsule(standAt + Vector3.up * (r + 0.05f), standAt + Vector3.up * (controller.height - r), r * 0.9f, mask, QueryTriggerInteraction.Ignore)) return false;
        if (Physics.CheckCapsule(pos + Vector3.up * (h + r + 0.05f), pos + Vector3.up * (h + controller.height - r), r * 0.9f, mask, QueryTriggerInteraction.Ignore)) return false;

        mantleFrom = pos;
        mantleTo = standAt;
        mantleT = 0f;
        mantleCooldown = 0.4f;
        verticalVelocity = 0f;
        Mantled?.Invoke();
        return true;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.5f) GroundCollider = hit.collider;
    }

    // Толчок (например, от Закрасчика). Вертикальная часть подбрасывает вверх.
    public void AddImpulse(Vector3 velocity)
    {
        impulse += new Vector3(velocity.x, 0f, velocity.z);
        if (velocity.y > 0f) verticalVelocity = Mathf.Max(verticalVelocity, velocity.y);
    }

    public void Teleport(Vector3 position, float yaw)
    {
        controller.enabled = false; // иначе CharacterController перезапишет позицию
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        controller.enabled = true;
        pitch = 0f;
        verticalVelocity = 0f;
        horizontalVelocity = Vector3.zero;
        impulse = Vector3.zero;
        wasGrounded = true;
        lastAirSpeed = 0f;
        mantleT = 1f;
        SpeedMultiplier = 1f;
    }

    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
