using UnityEngine;

// Тайник вандала: ящик с баллонами. E — пополнить краску во всех баллонах.
// Тёплый свет и покачивающиеся баллоны сверху — его видно издалека.
public class PaintStash : MonoBehaviour
{
    public float cooldown = 1f;
    float readyTime;
    Light lamp;

    public static PaintStash Create(Transform parent, Vector3 position, float yaw)
    {
        var go = new GameObject("PaintStash");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

        // Ящик.
        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Crate";
        crate.transform.SetParent(go.transform, false);
        crate.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        crate.transform.localScale = new Vector3(0.9f, 0.6f, 0.6f);
        crate.GetComponent<Renderer>().sharedMaterial = EnvTextures.Make("wood", EnvTextures.Wood, Color.white, new Vector2(1f, 1f));

        // Баллоны в ящике.
        Color[] colors = { new Color(1f, 0.2f, 0.6f), new Color(0.2f, 0.9f, 1f), new Color(1f, 0.85f, 0.1f), SprayCan.AirColor };
        for (int i = 0; i < colors.Length; i++)
        {
            var can = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(can.GetComponent<Collider>());
            can.transform.SetParent(go.transform, false);
            can.transform.localPosition = new Vector3(-0.3f + i * 0.2f, 0.72f, 0.02f * (i % 2));
            can.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);
            can.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1.5f) * 6f);
            can.GetComponent<Renderer>().sharedMaterial = ProtoMaterials.Get(colors[i]);
        }

        // Табличка и тёплый свет.
        WorldText.Create(go.transform, "ТАЙНИК\n[E] краска", new Vector3(0f, 1.25f, 0f), Quaternion.Euler(0f, 180f, 0f), 0.16f, new Color(1f, 0.8f, 0.4f));
        var lightGo = new GameObject("Lamp");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0.5f); // перед ящиком (лицевая сторона — +Z)
        var stash = go.AddComponent<PaintStash>();
        stash.lamp = lightGo.AddComponent<Light>();
        stash.lamp.type = LightType.Point;
        stash.lamp.range = 4f;
        stash.lamp.intensity = 1.4f;
        stash.lamp.color = new Color(1f, 0.6f, 0.25f);
        return stash;
    }

    // Пополнить краску. Возвращает true, если что-то пополнилось.
    public bool Refill(CanInventory inventory)
    {
        if (Time.time < readyTime || inventory == null) return false;
        bool any = false;
        foreach (var can in inventory.Cans)
        {
            if (can.paint < can.capacity - 0.01f) any = true;
            can.paint = can.capacity;
        }
        readyTime = Time.time + cooldown;
        if (any) ProcAudio.Play2D(ProcAudio.Refill, 0.9f);
        return any;
    }

    void Update()
    {
        if (lamp != null) lamp.intensity = 1.3f + 0.15f * Mathf.Sin(Time.time * 2.3f);
    }
}
