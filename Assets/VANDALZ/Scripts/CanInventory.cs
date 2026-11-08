using System;
using System.Collections.Generic;
using UnityEngine;

// Баллончики у игрока.
//  E — подобрать баллон, на который смотришь;
//  G — выбросить текущий;
//  1–4 или колёсико мыши — переключиться.
public class CanInventory : MonoBehaviour
{
    public Camera cam;
    [Tooltip("Куда класть выброшенные баллоны (папка в иерархии)")]
    public Transform pickupRoot;
    public int maxSlots = 4;
    public float pickupRange = 2.5f;

    public IReadOnlyList<SprayCan> Cans => cans;
    public int Selected { get; private set; }
    public SprayCan Current => cans.Count > 0 ? cans[Selected] : null;
    public bool IsFull => cans.Count >= maxSlots;
    // Баллон на уровне под прицелом (для подсказки «E — взять»).
    public CanPickup LookedAt { get; private set; }

    public event Action Changed;

    readonly List<SprayCan> cans = new List<SprayCan>();

    void Update()
    {
        LookedAt = null;
        if (cam == null || Cursor.lockState != CursorLockMode.Locked) return;

        // Что под прицелом? (Игрок и краска не мешают.)
        int mask = ~((1 << PaintSpray.PlayerLayer) | (1 << PaintSpray.PaintLayer));
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, pickupRange, mask, QueryTriggerInteraction.Ignore))
            LookedAt = hit.collider.GetComponentInParent<CanPickup>();

        if (LookedAt != null && !IsFull && VandalzInput.InteractPressed())
        {
            Give(LookedAt.can);
            Destroy(LookedAt.gameObject);
            LookedAt = null;
        }

        if (Current != null && VandalzInput.DropPressed()) DropCurrent();

        int slot = VandalzInput.SlotPressed();
        if (slot >= 0 && slot < cans.Count) Select(slot);

        float scroll = VandalzInput.Scroll();
        if (cans.Count > 1 && Mathf.Abs(scroll) > 0.01f)
            Select((Selected + (scroll > 0f ? -1 : 1) + cans.Count) % cans.Count);
    }

    // Положить баллон в инвентарь и сразу взять его в руку.
    public void Give(SprayCan can)
    {
        if (IsFull) return;
        cans.Add(can);
        Selected = cans.Count - 1;
        Changed?.Invoke();
    }

    public void Select(int index)
    {
        if (index == Selected || index < 0 || index >= cans.Count) return;
        Selected = index;
        Changed?.Invoke();
    }

    public void Clear()
    {
        cans.Clear();
        Selected = 0;
        Changed?.Invoke();
    }

    void DropCurrent()
    {
        SprayCan can = Current;
        cans.RemoveAt(Selected);
        Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, cans.Count - 1));
        Changed?.Invoke();

        Transform c = cam.transform;
        var pickup = CanPickup.Spawn(can, c.position + c.forward * 0.6f - Vector3.up * 0.2f,
            Quaternion.LookRotation(Vector3.ProjectOnPlane(c.forward, Vector3.up).normalized + Vector3.forward * 0.001f), true, pickupRoot);
        pickup.Throw(c.forward * 3f + Vector3.up * 1f);
    }
}
