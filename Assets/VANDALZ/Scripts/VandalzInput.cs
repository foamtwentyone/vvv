using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Единая точка ввода. Работает и со старым Input Manager, и с новым Input System
// (в зависимости от настройки Project Settings > Player > Active Input Handling).
public static class VandalzInput
{
    public static Vector2 Move()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
        float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#else
        return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
    }

    // Смещение мыши за кадр (в тех же единицах, что старый "Mouse X/Y").
    public static Vector2 Look()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
#else
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
    }

    public static bool JumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    public static bool SprintHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift);
#endif
    }

    public static bool SprayHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    public static bool ClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    public static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
