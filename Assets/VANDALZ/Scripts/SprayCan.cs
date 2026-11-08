using UnityEngine;

public enum CanType
{
    Regular, // обычный: граффити по поверхностям (на стене — объёмное, на нём можно стоять)
    Air,     // воздушный: рисует только в пустоте — мосты
}

// Один баллончик: тип, цвет и сколько в нём краски.
// Живёт либо в инвентаре игрока (CanInventory), либо лежит на уровне (CanPickup).
[System.Serializable]
public class SprayCan
{
    public CanType type;
    public Color color;
    [Tooltip("Сколько секунд распыления в полном баллоне")]
    public float capacity;
    public float paint;
    // Давление 0..1: падает при долгом распылении (мазок тоньше, «плюётся»),
    // само восстанавливается, а встряхивание (F) возвращает его сразу.
    public float pressure = 1f;

    public SprayCan(CanType type, Color color, float capacity)
    {
        this.type = type;
        this.color = color;
        this.capacity = capacity;
        paint = capacity;
    }

    public float Fill01 => capacity > 0f ? paint / capacity : 0f;
    public bool IsEmpty => paint <= 0f;
    public string Title => type == CanType.Air ? "воздушный баллон" : "баллон";

    // Готовые баллоны.
    public static readonly Color AirColor = new Color(0.85f, 0.97f, 1f);
    public static SprayCan Air(float capacity = 8f) => new SprayCan(CanType.Air, AirColor, capacity);
    public static SprayCan Regular(Color color, float capacity = 10f) => new SprayCan(CanType.Regular, color, capacity);
}
