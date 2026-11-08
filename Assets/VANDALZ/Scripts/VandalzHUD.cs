using System.Collections.Generic;
using UnityEngine;

// Интерфейс игры в стиле концепта: «стикеры» с толстой чёрной обводкой и жёсткой тенью,
// неоновые цвета, лёгкий наклон, граффити-логотип с подтёками.
// OnGUI, всё рисуется кодом; масштаб под высоту экрана (база 1080). Стили создаются один раз.
public class VandalzHUD : MonoBehaviour
{
    public GameFlow flow;
    public PaintSpray spray;
    public CanInventory inventory;

    class ToastItem { public string text; public Color color; public float born; public float tilt; }
    readonly List<ToastItem> toasts = new List<ToastItem>();

    // Неоновая палитра концепта.
    static readonly Color Lime = new Color(0.75f, 1f, 0.2f);
    static readonly Color Cyan = new Color(0.25f, 0.95f, 1f);
    static readonly Color Pink = new Color(1f, 0.3f, 0.65f);
    static readonly Color Yellow = new Color(1f, 0.88f, 0.2f);
    static readonly Color Ink = new Color(0.04f, 0.03f, 0.06f);
    static readonly Color Paper = new Color(0.1f, 0.1f, 0.14f, 0.92f);

    Texture2D rounded, circle, ring, drip;
    GUIStyle box, text, textCenter, small, smallCenter, big, title, tiny, toastStyle, lineStyle, pulseStyle;
    readonly Dictionary<int, GUIStyle> logoStyles = new Dictionary<int, GUIStyle>();
    readonly GUIContent content = new GUIContent();
    readonly float[] slotLift = new float[8];
    float scale = 1f;
    float W => Screen.width / scale;
    const float H = 1080f;

    void Start()
    {
        if (flow != null) flow.Toast += (t, c) =>
        {
            toasts.Add(new ToastItem { text = t, color = c, born = Time.unscaledTime, tilt = Random.Range(-2.5f, 2.5f) });
            if (toasts.Count > 4) toasts.RemoveAt(0);
        };
    }

    void BuildStyles()
    {
        rounded = MakeRounded(32, 12);
        circle = MakeCircle(64, false);
        ring = MakeCircle(64, true);
        drip = MakeDrip();

        box = new GUIStyle { normal = { background = rounded }, border = new RectOffset(13, 13, 13, 13) };
        text = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = Color.white } };
        textCenter = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
        small = new GUIStyle(text) { fontSize = 20 };
        smallCenter = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
        tiny = new GUIStyle(textCenter) { fontSize = 15 };
        big = new GUIStyle(textCenter) { fontSize = 60 };
        toastStyle = new GUIStyle(textCenter) { fontSize = 28 };
        title = new GUIStyle(textCenter) { fontSize = 190, fontStyle = FontStyle.BoldAndItalic };
        lineStyle = new GUIStyle(smallCenter) { fontSize = 22 };
        pulseStyle = new GUIStyle(textCenter);
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
        if (flow.HurtFlash > 0f) Fill(new Rect(0, 0, W, H), new Color(0.75f, 0.75f, 0.8f, 0.4f * flow.HurtFlash)); // серая «клякса» от удара
        if (flow.FadeAlpha > 0f && flow.State != GameState.Title) Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, flow.FadeAlpha));
    }

    // ---------- Экраны ----------

    void DrawTitle()
    {
        Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.02f, 0.03f, 0.78f));
        float t = Time.unscaledTime;
        float cy = H * 0.34f + Mathf.Sin(t * 1.3f) * 5f;
        Logo(new Rect(0, cy - 120, W, 240), "VANDALZ", t);

        Sticker(new Rect(W / 2 - 300, cy + 140, 600, 54), "заброшенное метро  ·  прототип", Cyan, -2f, smallCenter);

        float pulse = 1f + 0.04f * Mathf.Sin(t * 5f);
        var r = new Rect(W / 2 - 260 * pulse, H * 0.62f, 520 * pulse, 76 * pulse);
        pulseStyle.fontSize = Mathf.RoundToInt(34 * pulse);
        Sticker(r, "КЛИКНИ, ЧТОБЫ НАЧАТЬ", Yellow, 2f, pulseStyle, Ink);

        DrawControls(new Rect(W / 2 - 450, H * 0.74f, 900, 190));
    }

    void DrawControls(Rect r)
    {
        Panel(r, 0f, Paper, Lime);
        string s =
            "<b>WASD</b> ходить   <b>Shift</b> бег   <b>Пробел</b> прыжок (к краю — подтянешься)\n" +
            "<b>ЛКМ</b> рисовать   <b>F</b> встряхнуть (давление)   <b>E</b> взять / тайник   <b>G</b> выбросить\n" +
            "<b>1–4 / колёсико</b> баллон   <b>Esc</b> пауза   <b>R</b> заново\n" +
            "Обычный баллон — стены и пол. Воздушный — мост. Пшикни Закрасчику в глаза!";
        Outlined(new Rect(r.x + 20, r.y + 10, r.width - 40, r.height - 20), s, smallCenter, Color.white, 1.5f);
    }

    void DrawPause()
    {
        Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.55f));
        Logo(new Rect(0, H * 0.22f, W, 160), "ПАУЗА", Time.unscaledTime, 120);
        Sticker(new Rect(W / 2 - 330, H * 0.42f, 660, 56), "кликни, чтобы продолжить   ·   R — заново", Cyan, -1.5f, smallCenter);
        DrawControls(new Rect(W / 2 - 450, H * 0.55f, 900, 190));
    }

    void DrawFinish()
    {
        float k = Mathf.Clamp01(flow.StateTime / 0.6f);
        Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.02f, 0.03f, 0.75f * k));
        float y = Mathf.Lerp(H * 0.4f, H * 0.2f, k);
        Logo(new Rect(0, y, W, 200), "ВЫБРАЛСЯ!", Time.unscaledTime, 150);

        int tags = flow.TagsDone, total = flow.TagsTotal;
        float py = y + 230;
        Sticker(new Rect(W / 2 - 330, py, 200, 90), "ВРЕМЯ\n" + FormatTime(flow.Elapsed), Cyan, -3f, textCenter, Ink);
        Sticker(new Rect(W / 2 - 100, py + 8, 200, 90), $"ТЕГИ\n{tags} / {total}", Yellow, 2f, textCenter, Ink);
        Sticker(new Rect(W / 2 + 130, py, 200, 90), "ПАДЕНИЙ\n" + flow.Falls, Pink, -2f, textCenter, Ink);

        string verdict = tags == total ? "Идеальный вандализм — все споты помечены!" : tags > 0 ? "Неплохо. Но споты ещё остались…" : "Выбрался, но станция осталась серой.";
        Outlined(new Rect(0, py + 130, W, 40), verdict, textCenter, tags == total ? Yellow : Color.white, 2f);
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
        Outlined(new Rect(0, py + 190, W, 40), "R — ещё раз", textCenter, new Color(1, 1, 1, pulse), 2f);
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
        SprayMode mode = spray != null && spray.HasPreview ? spray.PreviewMode : SprayMode.None;
        bool none = mode == SprayMode.None;
        Color c = spray != null ? spray.CurrentColor : Color.white;

        // Кольцо давления: сколько «точек» горит — столько давления.
        if (spray != null && spray.CurrentCan != null)
        {
            const int dots = 24;
            float p = spray.Pressure;
            float radius = spraying ? 26f : 22f;
            for (int i = 0; i < dots; i++)
            {
                float a = -Mathf.PI / 2f + i * Mathf.PI * 2f / dots;
                bool on = i < Mathf.CeilToInt(p * dots);
                Color dc = on ? (p < 0.25f ? new Color(1f, 0.35f, 0.3f) : new Color(c.r, c.g, c.b, 0.9f)) : new Color(1f, 1f, 1f, 0.15f);
                float sz = on ? 6f : 4f;
                Tex(new Rect(cx + Mathf.Cos(a) * radius - sz / 2, cy + Mathf.Sin(a) * radius - sz / 2, sz, sz), circle, dc);
            }
        }
        Tex(new Rect(cx - 5, cy - 5, 10, 10), circle, Ink);
        Tex(new Rect(cx - 3.5f, cy - 3.5f, 7, 7), circle, none ? new Color(1, 1, 1, 0.5f) : Color.white);

        // Подсказка режима под прицелом.
        if (spray == null || !spray.HasPreview) return;
        string hint;
        Color hc = new Color(c.r, c.g, c.b, 1f);
        if (spray.PreviewBlocked) { hint = "тут стоишь ты"; hc = Color.gray; }
        else switch (mode)
        {
            case SprayMode.Wall: hint = "стена — объёмное граффити"; break;
            case SprayMode.Air: hint = "воздух — мост"; break;
            case SprayMode.ExtendBridge: hint = "продлить мост"; break;
            case SprayMode.Surface: hint = spray.PreviewNormal.y > 0f ? "пол — граффити" : "потолок — граффити"; break;
            case SprayMode.Enemy: hint = "в глаза ему!"; hc = Lime; break;
            default:
                hint = spray.CurrentCan != null && spray.CurrentCan.type == CanType.Air ? "воздушный — только в пустоту" : "обычный — только по стенам и полу";
                hc = new Color(0.8f, 0.8f, 0.8f);
                break;
        }
        if (spray.CurrentCan != null && spray.Pressure < 0.25f) { hint = "давление на нуле — встряхни (F)"; hc = new Color(1f, 0.45f, 0.4f); }
        Outlined(new Rect(cx - 300, cy + 36, 600, 30), hint, smallCenter, hc, 2f);
    }

    void DrawPrompt()
    {
        if (inventory == null) return;
        string s = null;
        if (inventory.LookedAt != null)
            s = inventory.IsFull ? "Руки заняты — G, чтобы выбросить баллон" : "[E]  взять: " + inventory.LookedAt.can.Title;
        else if (inventory.LookedAtStash != null)
            s = "[E]  пополнить краску";
        if (s == null) return;
        Sticker(new Rect(W / 2 - 270, H / 2 + 80, 540, 56), s, Yellow, -1.5f, textCenter, Ink);
    }

    void DrawLevelPanel()
    {
        var r = new Rect(30, 28, 440, 132);
        Panel(r, -1.5f, Paper, Pink);
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(-1.5f, r.center * scale);
        Outlined(new Rect(r.x + 24, r.y + 12, r.width, 36), flow.levelName, text, Color.white, 2f);
        Outlined(new Rect(r.x + 24, r.y + 54, 80, 30), "Теги", small, new Color(1, 1, 1, 0.75f), 1.5f);
        for (int i = 0; i < flow.TagsTotal; i++)
        {
            bool done = i < flow.TagsDone;
            var dr = new Rect(r.x + 98 + i * 34, r.y + 56, 26, 26);
            Tex(new Rect(dr.x - 2, dr.y - 2, dr.width + 4, dr.height + 4), circle, Ink);
            Tex(dr, done ? circle : ring, done ? Yellow : new Color(1, 1, 1, 0.6f));
        }
        Outlined(new Rect(r.x + 24, r.y + 90, r.width, 30), "Время  " + FormatTime(flow.Elapsed), small, new Color(1, 1, 1, 0.85f), 1.5f);
        GUI.matrix = m;
    }

    void DrawCans()
    {
        var cans = inventory.Cans;
        int slots = inventory.maxSlots;
        const float size = 96f, gap = 16f;
        float total = slots * size + (slots - 1) * gap;
        float x0 = (W - total) / 2f, baseY = H - size - 34f;

        for (int i = 0; i < slots; i++)
        {
            bool has = i < cans.Count;
            bool selected = has && i == inventory.Selected;
            slotLift[i] = Mathf.Lerp(slotLift[i], selected ? 18f : 0f, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            var r = new Rect(x0 + i * (size + gap), baseY - slotLift[i], size, size);
            float tilt = selected ? -3f : (i % 2 == 0 ? 1.5f : -1f);
            SprayCan can = has ? cans[i] : null;
            Panel(r, tilt, Paper, selected ? (can != null ? can.color : Color.white) : new Color(0.3f, 0.3f, 0.35f));

            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(tilt, r.center * scale);
            Outlined(new Rect(r.x + 10, r.y + 6, 30, 26), (i + 1).ToString(), small, new Color(1, 1, 1, 0.7f), 1.5f);
            if (can != null)
            {
                // Баллон-значок: корпус с уровнем краски, крышка цвета краски.
                var body = new Rect(r.x + size / 2 - 18, r.y + 30, 36, 54);
                Box(new Rect(body.x - 3, body.y - 3, body.width + 6, body.height + 6), Ink);
                Box(body, can.type == CanType.Air ? new Color(0.85f, 0.9f, 0.95f) : new Color(0.45f, 0.45f, 0.5f));
                float fill = can.Fill01;
                if (fill > 0f) Fill(new Rect(body.x + 4, body.yMax - 4 - (body.height - 8) * fill, body.width - 8, (body.height - 8) * fill), can.color);
                Box(new Rect(body.x + 7, body.y - 13, body.width - 14, 14), Ink);
                Fill(new Rect(body.x + 9, body.y - 11, body.width - 18, 10), can.color);
                if (can.type == CanType.Air) Outlined(new Rect(r.x, r.yMax - 24, size, 22), "ВОЗДУХ", tiny, Cyan, 1.5f);
                if (can.IsEmpty) Outlined(new Rect(r.x, r.y + 40, size, 30), "пусто", smallCenter, new Color(1f, 0.45f, 0.4f), 2f);
                // Подтёк краски под выбранным баллоном.
                if (selected) Tex(new Rect(r.x + size * 0.62f, r.yMax - 2, 12, 22 + Mathf.Sin(Time.unscaledTime * 2f) * 4f), drip, can.color);
            }
            GUI.matrix = m;
        }

        SprayCan current = inventory.Current;
        string line = current == null ? "нет баллона — подбери (E)"
            : current.IsEmpty ? current.Title + ": пусто — тайник (E) или выбросить (G)"
            : $"{current.Title}   {current.paint:0.0} с";
        Color lc = current != null ? Color.Lerp(current.color, Color.white, 0.3f) : Color.white;
        Outlined(new Rect(0, baseY - 56, W, 30), line, lineStyle, lc, 2f);
    }

    void DrawToasts()
    {
        float now = Time.unscaledTime;
        toasts.RemoveAll(t => now - t.born > 3.2f);
        float y = 176f;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var t = toasts[i];
            float age = now - t.born;
            float a = Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01((3.2f - age) / 0.5f);
            float pop = 1f + 0.15f * Mathf.Clamp01(1f - age / 0.18f);
            content.text = t.text;
            float w = Mathf.Min(W - 80, toastStyle.CalcSize(content).x + 70) * pop;
            var r = new Rect(W / 2 - w / 2, y, w, 58 * pop);
            Sticker(r, t.text, t.color, t.tilt, toastStyle, Ink, a);
            y += 72f;
        }
    }

    // ---------- Стикеры и текст ----------

    // Граффити-логотип: цветные «тени», толстая чёрная обводка, белые буквы и неоновые подтёки.
    void Logo(Rect r, string word, float t, int size = 190)
    {
        if (!logoStyles.TryGetValue(size, out var st)) logoStyles[size] = st = new GUIStyle(title) { fontSize = size };
        Outlined(Offset(r, 16, 14), word, st, Pink, 0f);
        Outlined(Offset(r, -9, -6), word, st, new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f), 0f);
        Outlined(r, word, st, Color.white, 6f);
        content.text = word;
        float w = st.CalcSize(content).x;
        float x0 = W / 2 - w / 2;
        Color[] cols = { Lime, Cyan, Pink, Yellow, Lime };
        for (int i = 0; i < 5; i++)
        {
            float x = x0 + w * (0.12f + i * 0.19f);
            float len = 40f + 30f * Mathf.Sin(t * 0.8f + i * 1.7f) + i * 6f;
            Tex(new Rect(x - 8, r.y + r.height * 0.78f, 16, len + 8), drip, Ink);
            Tex(new Rect(x - 6, r.y + r.height * 0.78f, 12, len), drip, cols[i]);
        }
    }

    // Стикер: чёрная подложка-обводка, жёсткая тень, цветная плашка, текст. Слегка повёрнут.
    void Sticker(Rect r, string s, Color color, float tilt, GUIStyle style, Color? textColor = null, float alpha = 1f)
    {
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(tilt, r.center * scale);
        Box(Offset(Grow(r, 4), 7, 7), new Color(0f, 0f, 0f, 0.55f * alpha));
        Box(Grow(r, 4), new Color(Ink.r, Ink.g, Ink.b, alpha));
        Box(r, new Color(color.r, color.g, color.b, alpha));
        Color tc = textColor ?? Color.white;
        tc.a *= alpha;
        if (textColor.HasValue) Label(r, s, style, tc);
        else Outlined(r, s, style, tc, 2f);
        GUI.matrix = m;
    }

    // Панель: тёмная плашка с цветной полосой слева и чёрной обводкой.
    void Panel(Rect r, float tilt, Color fill, Color accent)
    {
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(tilt, r.center * scale);
        Box(Offset(Grow(r, 4), 6, 6), new Color(0f, 0f, 0f, 0.5f));
        Box(Grow(r, 4), Ink);
        Box(r, fill);
        Fill(new Rect(r.x + 8, r.y + 14, 6, r.height - 28), accent);
        GUI.matrix = m;
    }

    // Текст с чёрной обводкой (8 копий вокруг) — читается на любом фоне.
    void Outlined(Rect r, string s, GUIStyle style, Color color, float thickness)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (thickness > 0f)
        {
            Color o = new Color(Ink.r, Ink.g, Ink.b, color.a);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Label(Offset(r, Mathf.Cos(a) * thickness, Mathf.Sin(a) * thickness), s, style, o);
            }
        }
        Label(r, s, style, color);
    }

    void Label(Rect r, string s, GUIStyle style, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.Label(r, s, style);
        GUI.color = old;
    }

    static string FormatTime(float s) => $"{(int)(s / 60f):00}:{(int)(s % 60f):00}";
    static Rect Offset(Rect r, float dx, float dy) => new Rect(r.x + dx, r.y + dy, r.width, r.height);
    static Rect Grow(Rect r, float d) => new Rect(r.x - d, r.y - d, r.width + d * 2, r.height + d * 2);

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

    // Капля-подтёк: узкая полоса со скруглённой каплей внизу (сверху — y = height).
    static Texture2D MakeDrip()
    {
        const int w = 16, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = x + 0.5f - w / 2f;
                float a;
                if (y < w) // капля внизу (в текстуре y=0 — низ)
                {
                    float cy = y + 0.5f - w / 2f;
                    a = Mathf.Clamp01(w / 2f - Mathf.Sqrt(cx * cx + cy * cy) + 0.5f);
                }
                else
                {
                    float half = Mathf.Lerp(w * 0.32f, w * 0.42f, (y - w) / (float)(h - w));
                    a = Mathf.Clamp01(half - Mathf.Abs(cx) + 0.5f);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }
}
