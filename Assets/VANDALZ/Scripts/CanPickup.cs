using UnityEngine;

// Баллончик, лежащий на уровне. Подбирается клавишей E (см. CanInventory).
// Пока лежит на своём месте — медленно крутится, чтобы его было видно.
// Выброшенный баллон (G) падает с физикой.
public class CanPickup : MonoBehaviour
{
    public SprayCan can;

    Rigidbody body;
    Vector3 basePos;

    public static CanPickup Spawn(SprayCan can, Vector3 position, Quaternion rotation, bool physics, Transform parent)
    {
        var root = new GameObject(can.type == CanType.Air ? "AirCan" : "SprayCan");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(position, rotation);

        // Модель: корпус, крышка цвета краски, распылитель. Чуть крупнее реального — чтобы было видно.
        Color bodyColor = can.type == CanType.Air ? new Color(0.95f, 0.98f, 1f) : new Color(0.7f, 0.7f, 0.74f);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.17f, 0f), new Vector3(0.14f, 0.17f, 0.14f), bodyColor, can.type == CanType.Air);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.37f, 0f), new Vector3(0.12f, 0.035f, 0.12f), can.color, true);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0.02f), new Vector3(0.035f, 0.035f, 0.05f), new Color(0.1f, 0.1f, 0.1f), false);

        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.21f, 0f);
        box.size = new Vector3(0.18f, 0.44f, 0.18f);

        if (can.type == CanType.Air)
        {
            // Воздушный баллон светится — его легко заметить в тёмном метро.
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 2.5f;
            glow.intensity = 1.5f;
            glow.color = can.color;
        }

        var pickup = root.AddComponent<CanPickup>();
        pickup.can = can;
        if (physics)
        {
            pickup.body = root.AddComponent<Rigidbody>();
            pickup.body.mass = 0.4f;
        }
        return pickup;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color, bool glow)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>()); // столкновения — только у общего BoxCollider
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var r = go.GetComponent<Renderer>();
        if (glow)
        {
            var mat = new Material(ProtoMaterials.Get(color));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.5f);
            r.sharedMaterial = mat;
        }
        else
        {
            r.sharedMaterial = ProtoMaterials.Get(color);
        }
    }

    void Start()
    {
        basePos = transform.position;
    }

    void Update()
    {
        if (body != null) return; // выброшенный баллон лежит как лежит
        transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        transform.position = basePos + Vector3.up * (0.05f + 0.05f * Mathf.Sin(Time.time * 2f));
    }

    // Бросить вперёд (для выброшенного баллона).
    public void Throw(Vector3 velocity)
    {
        if (body != null) body.AddForce(velocity, ForceMode.VelocityChange);
    }
}
