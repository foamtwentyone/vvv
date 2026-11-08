using UnityEngine;

// Правила уровня: респаун при падении, финиш, рестарт по R и простой интерфейс (OnGUI).
public class VandalzGame : MonoBehaviour
{
    public FirstPersonController player;
    public PaintSpray spray;

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

    GUIStyle bigStyle, smallStyle;

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
            // Краска (пятна) остаётся, запас краски тоже не восстанавливается.
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

    // Полный рестарт: стираем всю краску, заливаем баллончик, игрок на старт.
    void RestartLevel()
    {
        if (spray != null) spray.ResetPaint();
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
        }

        float sw = Screen.width, sh = Screen.height;

        // Прицел
        DrawRect(new Rect(sw / 2 - 3, sh / 2 - 3, 6, 6), spray != null && spray.IsSpraying ? Color.magenta : Color.white);

        // Полоска краски
        if (spray != null)
        {
            const float w = 320f, h = 22f;
            var bg = new Rect((sw - w) / 2, sh - 60, w, h);
            DrawRect(new Rect(bg.x - 3, bg.y - 3, bg.width + 6, bg.height + 6), new Color(0, 0, 0, 0.7f));
            Color fill = spray.Paint01 > 0.25f ? new Color(1f, 0.25f, 0.65f) : new Color(1f, 0.2f, 0.1f);
            DrawRect(new Rect(bg.x, bg.y, bg.width * spray.Paint01, bg.height), fill);
            string label = spray.Paint > 0f ? $"КРАСКА  {spray.Paint:0.0} с" : "КРАСКА КОНЧИЛАСЬ  (R — заново)";
            Shadowed(new Rect(bg.x, bg.y - 26, w, 24), label, smallStyle);
        }

        // Подсказка
        Shadowed(new Rect(12, 10, 700, 120),
            "WASD — ходить, Shift — бег, Пробел — прыжок\n" +
            "ЛКМ (зажать) — баллончик, Esc — курсор, R — заново\n" +
            "Пусто — мост, линия на стене — полка, задержи на стене — подтёк\n" +
            $"Падений: {falls}", smallStyle);

        if (Time.time < respawnMessageUntil && !finished)
            Shadowed(new Rect(0, sh * 0.3f, sw, 60), "Упал! Краска осталась — пробуй ещё", bigStyle);

        if (finished)
            Shadowed(new Rect(0, sh * 0.3f, sw, 120),
                $"ФИНИШ!\n<size=24>Время: {finishTime:0.0} с · падений: {falls} · R — заново</size>", bigStyle);
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
