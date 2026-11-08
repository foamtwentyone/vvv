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

        Vector2 input = InputEnabled && Cursor.lockState == CursorLockMode.Locked ? VandalzInput.Move() : Vector2.zero;
        IsSprinting = InputEnabled && VandalzInput.SprintHeld() && input.y > 0.1f;
        float speed = IsSprinting ? sprintSpeed : walkSpeed;
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
    }

    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
