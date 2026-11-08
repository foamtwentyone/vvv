using UnityEngine;

// Контроллер от первого лица на CharacterController: WASD, мышь, прыжок, гравитация.
// Позиция объекта = ступни игрока, камера — дочерний объект на уровне глаз.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public Transform cameraPivot;

    [Header("Движение")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.3f;
    public float gravity = -22f;

    [Header("Мышь")]
    public float mouseSensitivity = 2f;
    public float maxPitch = 85f;

    [Header("Прощение ошибок")]
    [Tooltip("Сколько секунд после схода с края ещё можно прыгнуть")]
    public float coyoteTime = 0.12f;
    [Tooltip("Сколько секунд помнится нажатие прыжка до приземления")]
    public float jumpBuffer = 0.12f;

    // На чём игрок стоит прямо сейчас (null — в воздухе). Нужно, чтобы мокрая краска проседала.
    public Collider GroundCollider { get; private set; }

    CharacterController controller;
    float pitch;
    float verticalVelocity;
    float coyoteTimer;
    float jumpBufferTimer;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        SetCursorLocked(true);
    }

    void Update()
    {
        // Esc — отпустить курсор, клик — снова захватить.
        if (VandalzInput.EscapePressed()) SetCursorLocked(false);
        else if (Cursor.lockState != CursorLockMode.Locked && VandalzInput.ClickPressed()) SetCursorLocked(true);

        if (Cursor.lockState == CursorLockMode.Locked) Look();
        Move();
    }

    void Look()
    {
        Vector2 delta = VandalzInput.Look() * mouseSensitivity;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        float dt = Time.deltaTime;

        Vector2 input = VandalzInput.Move();
        float speed = VandalzInput.SprintHeld() ? sprintSpeed : walkSpeed;
        Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

        if (controller.isGrounded)
        {
            coyoteTimer = coyoteTime;
            if (verticalVelocity < 0f) verticalVelocity = -2f; // прижимаем к полу
        }
        else
        {
            coyoteTimer -= dt;
        }

        if (VandalzInput.JumpPressed()) jumpBufferTimer = jumpBuffer;
        else jumpBufferTimer -= dt;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        verticalVelocity += gravity * dt;

        GroundCollider = null; // заполнится в OnControllerColliderHit во время Move
        CollisionFlags flags = controller.Move((horizontal + Vector3.up * verticalVelocity) * dt);
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f; // ударились головой
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.5f) GroundCollider = hit.collider;
    }

    public void Teleport(Vector3 position, float yaw)
    {
        controller.enabled = false; // иначе CharacterController перезапишет позицию
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        controller.enabled = true;
        pitch = 0f;
        verticalVelocity = 0f;
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
