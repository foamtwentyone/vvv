using System.Collections.Generic;
using UnityEngine;

// Интерфейс игры (OnGUI, всё рисуется кодом). Масштабируется под высоту экрана (база 1080).
//  - заставка VANDALZ, пауза, экран финиша;
//  - прицел, подсказка режима баллона, подсказка «[E] …»;
//  - панель уровня: название, теги, время;
//  - слоты баллонов с уровнем краски (выбранный приподнят);
//  - всплывающие сообщения, красная вспышка при ударе, затемнение.
public class VandalzHUD : MonoBehaviour
{
    public GameFlow flow;
    public PaintSpray spray;
    public CanInventory inventory;

    class ToastItem { public string text; public Color color; public float born; }
    readonly List<ToastItem> toasts = new List<ToastItem>();

    Texture2D rounded, circle, ring;
    GUIStyle box, label, labelCenter, small, title, big;
    float[] slotLift = new float[8];
    float scale = 1f;
    float W => Screen.width / scale;
    const float H = 1080f;

    static readonly Color Pink = new Color(1f, 0.25f, 0.6f);
    static readonly Color Cyan = new Color(0.2f, 0.9f, 1f);
    static readonly Color Panel = new Color(0.05f, 0.05f, 0.08f, 0.78f);

    void Start()
    {
        if (flow != null) flow.Toast += (t, c) =>
        {
            toasts.Add(new ToastItem { text = t, color = c, born = Time.unscaledTime });
            if (toasts.Count > 4) toasts.RemoveAt(0);
        };
    }

    void BuildStyles()
    {
        rounded = MakeRounded(32, 12);
        circle = MakeCircle(64, false);
        ring = MakeCircle(64, true);

        box = new GUIStyle { normal = { background = rounded }, border = new RectOffset(13, 13, 13, 13) };
        label = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, richText = true };
        labelCenter = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        small = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Normal };
        big = new GUIStyle(labelCenter) { fontSize = 64 };
        title = new GUIStyle(labelCenter) { fontSize = 170, fontStyle = FontStyle.BoldAndItalic };
    }

    void OnGUI()
    {
        if (rounded == null) BuildStyles();
        scale = Screen.height / H;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        if (flow == null) return;
        switch (flow.State)
        {
            case GameState.Title: DrawTitle(); break;
            case GameState.Playing: DrawPlaying(); break;
            case GameState.Paused: DrawPlaying(); DrawPause(); break;
            case GameState.Finished: DrawFinish(); break;
        }

        DrawToasts();
        if (flow.HurtFlash > 0f) Fill(new Rect(0, 0, W, H), new Color(0.8f, 0.1f, 0.1f, 0.35f * flow.HurtFlash));
        if (flow.FadeAlpha > 0f && flow.State != GameState.Title) Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, flow.FadeAlpha));
    }

    // ---------- Экраны ----------

    void DrawTitle()
    {
        Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.04f, 0.82f));
        float t = Time.unscaledTime;
        float cy = H * 0.36f + Mathf.Sin(t * 1.3f) * 6f;

        // Логотип «граффити»: цветные тени + обводка + белые буквы.
        var r = new Rect(0, cy - 110, W, 220);
        Text(Offset(r, 14, 12), "VANDALZ", title, Pink);
        Text(Offset(r, -8, -6), "VANDALZ", title, Cyan * new Color(1, 1, 1, 0.85f));
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Text(Offset(r, Mathf.Cos(a) * 5f, Mathf.Sin(a) * 5f), "VANDALZ", title, Color.black);
        }
        Text(r, "VANDALZ", title, Color.white);

        Text(new Rect(0, cy + 110, W, 40), "заброшенное метро  ·  прототип", labelCenter, new Color(1f, 1f, 1f, 0.7f));

        float pulse = 0.55f + 0.45f * Mathf.Sin(t * 4f);
        Text(new Rect(0, H * 0.62f, W, 50), "КЛИКНИ, ЧТОБЫ НАЧАТЬ", big, new Color(1f, 1f, 1f, pulse));

        DrawControls(new Rect(W / 2 - 430, H * 0.72f, 860, 200));
    }

    void DrawControls(Rect r)
    {
        Box(r, Panel);
        string text =
            "<b>WASD</b> — ходить     <b>Shift</b> — бег     <b>Пробел</b> — прыжок\n" +
            "<b>ЛКМ</b> — рисовать     <b>E</b> — взять / тайник     <b>G</b> — выбросить\n" +
            "<b>1–4 / колёсико</b> — сменить баллон     <b>F</b> — встряхнуть     <b>R</b> — заново\n" +
            "Обычный баллон — стены и пол (линия на стене = полка). Воздушный — мост над пустотой.";
        GUI.Label(new Rect(r.x + 20, r.y + 14, r.width - 40, r.height - 28), text, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, richText = true });
    }

    void DrawPause()
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
        Text(new Rect(0, H * 0.3f, W, 90), "ПАУЗА", big, Color.white);
        Text(new Rect(0, H * 0.3f + 90, W, 40), "кликни, чтобы продолжить  ·  R — заново", labelCenter, new Color(1, 1, 1, 0.8f));
        DrawControls(new Rect(W / 2 - 430, H * 0.55f, 860, 200));
    }

    void DrawFinish()
    {
        float k = Mathf.Clamp01(flow.StateTime / 0.6f);
        Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.04f, 0.75f * k));
        float y = Mathf.Lerp(H * 0.4f, H * 0.28f, k);
        var r = new Rect(0, y, W, 160);
        Text(Offset(r, 10, 8), "ВЫБРАЛСЯ!", title, Pink);
        Text(r, "ВЫБРАЛСЯ!", title, Color.white);

        int tags = flow.TagsDone, total = flow.TagsTotal;
        string stats = $"Время  {FormatTime(flow.Elapsed)}\nТеги  {tags} / {total}\nПадений  {flow.Falls}";
        var panel = new Rect(W / 2 - 260, y + 200, 520, 190);
        Box(panel, Panel);
        GUI.Label(new Rect(panel.x, panel.y + 10, panel.width, panel.height - 20), stats, new GUIStyle(labelCenter) { fontSize = 36 });

        string verdict = tags == total ? "Идеальный вандализм — все споты помечены!" : tags > 0 ? "Неплохо. Но споты ещё остались…" : "Выбрался, но станция осталась серой.";
        Text(new Rect(0, panel.yMax + 24, W, 40), verdict, labelCenter, tags == total ? new Color(1f, 0.82f, 0.2f) : new Color(1, 1, 1, 0.8f));
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
        Text(new Rect(0, panel.yMax + 80, W, 40), "R — ещё раз", labelCenter, new Color(1, 1, 1, pulse));
    }

    void DrawPlaying()
    {
        DrawCrosshair();
        DrawLevelPanel();
        if (inventory != null) DrawCans();
        DrawPrompt();
    }

    // ---------- Игровой интерфейс ----------

    void DrawCrosshair()
    {
        float cx = W / 2f, cy = H / 2f;
        bool spraying = spray != null && spray.IsSpraying;
        bool none = spray == null || !spray.HasPreview || spray.PreviewMode == SprayMode.None;
        Color c = spray != null ? spray.CurrentColor : Color.white;

        float ringSize = spraying ? 34f + Mathf.Sin(Time.unscaledTime * 30f) * 3f : 26f;
        if (!none) Tex(new Rect(cx - ringSize / 2, cy - ringSize / 2, ringSize, ringSize), ring, new Color(c.r, c.g, c.b, spraying ? 0.95f : 0.6f));
        Tex(new Rect(cx - 4, cy - 4, 8, 8), circle, none ? new Color(1, 1, 1, 0.5f) : Color.white);

        // Подсказка режима под прицелом.
        if (spray == null || !spray.HasPreview) return;
        string text;
        if (spray.PreviewBlocked) text = "тут стоишь ты";
        else switch (spray.PreviewMode)
        {
            case SprayMode.Wall: text = "стена — объёмное граффити"; break;
            case SprayMode.Air: text = "воздух — мост"; break;
            case SprayMode.ExtendBridge: text = "продлить мост"; break;
            case SprayMode.Surface: text = spray.PreviewNormal.y > 0f ? "пол — граффити" : "потолок — граффити"; break;
            default:
                text = spray.CurrentCan != null && spray.CurrentCan.type == CanType.Air
                    ? "воздушный — только в пустоту" : "обычный — только по стенам и полу";
                break;
        }
        Color hc = none || spray.PreviewBlocked ? new Color(0.8f, 0.8f, 0.8f, 0.8f) : new Color(c.r, c.g, c.b, 1f);
        Text(new Rect(cx - 300, cy + 26, 600, 30), text, new GUIStyle(labelCenter) { fontSize = 20 }, hc);
    }

    void DrawPrompt()
    {
        if (inventory == null) return;
        string text = null;
        if (inventory.LookedAt != null)
            text = inventory.IsFull ? "Руки заняты — G, чтобы выбросить баллон" : "[E]  взять: " + inventory.LookedAt.can.Title;
        else if (inventory.LookedAtStash != null)
            text = "[E]  пополнить краску";
        if (text == null) return;

        var r = new Rect(W / 2 - 260, H / 2 + 70, 520, 50);
        Box(r, Panel);
        Text(r, text, labelCenter, Color.white);
    }

    void DrawLevelPanel()
    {
        var r = new Rect(28, 26, 420, 128);
        Box(r, Panel);
        Fill(new Rect(r.x, r.y + 14, 6, r.height - 28), Pink);
        Text(new Rect(r.x + 24, r.y + 12, r.width, 34), flow.levelName, label, Color.white);
        Text(new Rect(r.x + 24, r.y + 50, r.width, 30), "Теги", small, new Color(1, 1, 1, 0.7f));
        for (int i = 0; i < flow.TagsTotal; i++)
        {
            bool done = i < flow.TagsDone;
            Tex(new Rect(r.x + 90 + i * 30, r.y + 54, 22, 22), done ? circle : ring, done ? new Color(1f, 0.82f, 0.2f) : new Color(1, 1, 1, 0.6f));
        }
        Text(new Rect(r.x + 24, r.y + 86, r.width, 30), "Время  " + FormatTime(flow.Elapsed), small, new Color(1, 1, 1, 0.85f));
    }

    void DrawCans()
    {
        var cans = inventory.Cans;
        int slots = inventory.maxSlots;
        const float size = 92f, gap = 14f;
        float total = slots * size + (slots - 1) * gap;
        float x0 = (W - total) / 2f, baseY = H - size - 34f;

        for (int i = 0; i < slots; i++)
        {
            bool has = i < cans.Count;
            bool selected = has && i == inventory.Selected;
            slotLift[i] = Mathf.Lerp(slotLift[i], selected ? 16f : 0f, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            var r = new Rect(x0 + i * (size + gap), baseY - slotLift[i], size, size);

            if (selected) Box(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Color.white);
            Box(r, Panel);
            Text(new Rect(r.x + 10, r.y + 4, 30, 26), (i + 1).ToString(), small, new Color(1, 1, 1, 0.6f));
            if (!has) continue;

            SprayCan can = cans[i];
            // Значок баллона: корпус (заливка = остаток краски), крышка цвета краски.
            var body = new Rect(r.x + size / 2 - 17, r.y + 30, 34, 50);
            Box(body, new Color(0.25f, 0.25f, 0.3f));
            float fill = can.Fill01;
            if (fill > 0f)
            {
                var f = new Rect(body.x + 3, body.yMax - 3 - (body.height - 6) * fill, body.width - 6, (body.height - 6) * fill);
                Fill(f, can.color);
            }
            Fill(new Rect(body.x + 8, body.y - 10, body.width - 16, 12), can.type == CanType.Air ? Color.white : can.color);
            if (can.type == CanType.Air) Text(new Rect(r.x, r.yMax - 26, size, 24), "ВОЗДУХ", new GUIStyle(labelCenter) { fontSize = 14 }, Color.white);
            if (can.IsEmpty) Text(new Rect(r.x, r.y + 40, size, 30), "пусто", new GUIStyle(labelCenter) { fontSize = 18 }, new Color(1f, 0.5f, 0.5f));
        }

        SprayCan current = inventory.Current;
        string line = current == null ? "нет баллона — подбери (E)"
            : current.IsEmpty ? current.Title + ": пусто — тайник (E) или выбросить (G)"
            : $"{current.Title}   {current.paint:0.0} с";
        Color lc = current != null ? current.color : Color.white;
        Text(new Rect(0, baseY - 54, W, 30), line, new GUIStyle(labelCenter) { fontSize = 22 }, Color.Lerp(lc, Color.white, 0.4f));
    }

    void DrawToasts()
    {
        float now = Time.unscaledTime;
        toasts.RemoveAll(t => now - t.born > 3.2f);
        float y = 150f;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var t = toasts[i];
            float age = now - t.born;
            float a = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((3.2f - age) / 0.5f);
            float slide = (1f - Mathf.Clamp01(age / 0.25f)) * -30f;
            var style = new GUIStyle(labelCenter) { fontSize = 28 };
            float w = Mathf.Min(W - 80, style.CalcSize(new GUIContent(t.text)).x + 60);
            var r = new Rect(W / 2 - w / 2, y + slide, w, 54);
            Box(r, new Color(Panel.r, Panel.g, Panel.b, Panel.a * a));
            Fill(new Rect(r.x + 10, r.y + 12, 5, r.height - 24), new Color(t.color.r, t.color.g, t.color.b, a));
            Text(r, t.text, style, new Color(Mathf.Lerp(t.color.r, 1f, 0.5f), Mathf.Lerp(t.color.g, 1f, 0.5f), Mathf.Lerp(t.color.b, 1f, 0.5f), a));
            y += 64f;
        }
    }

    // ---------- Рисование ----------

    static string FormatTime(float s) => $"{(int)(s / 60f):00}:{(int)(s % 60f):00}";

    static Rect Offset(Rect r, float dx, float dy) => new Rect(r.x + dx, r.y + dy, r.width, r.height);

    void Text(Rect r, string text, GUIStyle style, Color color)
    {
        Color old = GUI.color;
        // Тень.
        GUI.color = new Color(0f, 0f, 0f, color.a * 0.6f);
        GUI.Label(Offset(r, 2, 2), text, style);
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = old;
    }

    void Box(Rect r, Color c)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color old = GUI.color;
        GUI.color = c;
        box.Draw(r, false, false, false, false);
        GUI.color = old;
    }

    static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Tex(Rect r, Texture2D t, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, t);
        GUI.color = old;
    }

    static Texture2D MakeRounded(int size, float radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
            }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeCircle(int size, bool hollow)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float r = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (hollow) a *= Mathf.Clamp01(d - (r - size * 0.12f) + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }
}
