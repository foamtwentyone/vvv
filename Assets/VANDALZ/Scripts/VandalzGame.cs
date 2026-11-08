using UnityEngine;

// Правила уровня: респаун при падении, финиш, рестарт по R и простой интерфейс (OnGUI).
public class VandalzGame : MonoBehaviour
{
    public FirstPersonController player;
    public PaintSpray spray;
    public CanInventory inventory;

    // Рестарт уровня (R): сюда подписывается SubwayLevelBuilder, чтобы вернуть баллоны на места.
    public event System.Action Restarted;

    public Vector3 spawnPosition;
    public float spawnYaw;
    [Tooltip("Ниже этой высоты игрок считается упавшим в пропасть")]
    public float killY = -4f;
    public Bounds finishZone;

    bool finished;
    float finishTime;
    float levelStartTime;
    float respawnMessageUntil;
    int falls;

    GUIStyle bigStyle, smallStyle, centerStyle;

    void Start()
    {
        levelStartTime = Time.time;
    }

    void Update()
    {
        if (player == null) return;

        if (VandalzInput.RestartPressed()) RestartLevel();
        Vector3 p = player.transform.position;

        if (p.y < killY)
        {
            // Краска остаётся, баллоны тоже.
            player.Teleport(spawnPosition, spawnYaw);
            falls++;
            respawnMessageUntil = Time.time + 1.5f;
        }

        if (!finished && finishZone.Contains(p))
        {
            finished = true;
            finishTime = Time.time - levelStartTime;
        }
    }

    // Полный рестарт: стираем всю краску, баллоны — на места, игрок на старт.
    void RestartLevel()
    {
        if (spray != null) spray.ResetPaint();
        Restarted?.Invoke();
        player.Teleport(spawnPosition, spawnYaw);
        finished = false;
        falls = 0;
        respawnMessageUntil = 0f;
        levelStartTime = Time.time;
    }

    void OnGUI()
    {
        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperLeft };
            centerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
        }

        float sw = Screen.width, sh = Screen.height;

        // Прицел
        DrawRect(new Rect(sw / 2 - 3, sh / 2 - 3, 6, 6), spray != null && spray.IsSpraying ? Color.magenta : Color.white);

        // Баллоны: слоты внизу экрана, у каждого — уровень краски.
        if (inventory != null) DrawCans(sw, sh);

        // Подсказка «E — взять».
        if (inventory != null && inventory.LookedAt != null)
        {
            string take = inventory.IsFull ? "руки заняты (G — выбросить)" : "E — взять: " + inventory.LookedAt.can.Title;
            Shadowed(new Rect(sw / 2 - 200, sh / 2 + 40, 400, 26), take, centerStyle);
        }

        // Подсказка
        Shadowed(new Rect(12, 10, 760, 140),
            "WASD — ходить, Shift — бег, Пробел — прыжок, Esc — курсор, R — заново\n" +
            "ЛКМ (зажать) — рисовать, E — взять баллон, G — выбросить, 1–4 / колёсико — сменить\n" +
            "Обычный баллон — стены и пол (линия на стене — полка), воздушный — мост над пустотой\n" +
            $"Падений: {falls}", smallStyle);

        if (Time.time < respawnMessageUntil && !finished)
            Shadowed(new Rect(0, sh * 0.3f, sw, 60), "Упал! Краска осталась — пробуй ещё", bigStyle);

        if (finished)
            Shadowed(new Rect(0, sh * 0.3f, sw, 120),
                $"ФИНИШ!\n<size=24>Время: {finishTime:0.0} с · падений: {falls} · R — заново</size>", bigStyle);
    }

    void DrawCans(float sw, float sh)
    {
        const float size = 58f, gap = 10f;
        var cans = inventory.Cans;
        int slots = inventory.maxSlots;
        float total = slots * size + (slots - 1) * gap;
        float x0 = (sw - total) / 2f, y = sh - size - 24f;

        for (int i = 0; i < slots; i++)
        {
            var r = new Rect(x0 + i * (size + gap), y, size, size);
            bool selected = i < cans.Count && i == inventory.Selected;
            DrawRect(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), selected ? Color.white : new Color(0f, 0f, 0f, 0.7f));
            DrawRect(r, new Color(0.08f, 0.08f, 0.1f, 0.85f));
            Shadowed(new Rect(r.x + 4, r.y + 2, 20, 20), (i + 1).ToString(), smallStyle);
            if (i >= cans.Count) continue;

            SprayCan can = cans[i];
            // Заливка снизу вверх — сколько краски осталось.
            float fill = can.Fill01;
            Color c = can.color;
            DrawRect(new Rect(r.x + 6, r.yMax - 6 - (size - 12) * fill, size - 12, (size - 12) * fill), c);
            string tag = can.type == CanType.Air ? "ВОЗД" : "";
            if (can.IsEmpty) tag = "пусто";
            Shadowed(new Rect(r.x, r.y + size / 2 - 10, size, 22), tag, centerStyle);
        }

        // Подпись текущего баллона.
        SprayCan current = inventory.Current;
        string label = current == null ? "нет баллона — подбери (E)"
            : current.IsEmpty ? current.Title + ": пусто (G — выбросить)"
            : $"{current.Title}: {current.paint:0.0} с";
        Shadowed(new Rect(0, y - 28, sw, 24), label, centerStyle);
    }

    static void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Shadowed(Rect r, string text, GUIStyle style)
    {
        Color old = GUI.color;
        GUI.color = Color.black;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        GUI.color = old;
        GUI.Label(r, text, style);
    }
}
