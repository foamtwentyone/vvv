using UnityEngine;

// Надписи прямо в мире (таблички, подсказки, название станции) на встроенном шрифте.
public static class WorldText
{
    static Font font;
    static bool fontTried;
    static Material material;

    // Материал текста с проверкой глубины (свой шейдер). Если шейдера нет — встроенный материал шрифта.
    static Material TextMaterial
    {
        get
        {
            if (material != null) return material;
            if (Font == null) return null;
            var shader = Shader.Find("VANDALZ/WorldText");
            if (shader == null) shader = Resources.Load<Shader>("VandalzWorldText");
            if (shader == null || !shader.isSupported) return material = Font.material;
            material = new Material(shader) { mainTexture = Font.material.mainTexture };
            // Атлас шрифта перестраивается, когда появляются новые буквы, — подхватываем новую текстуру.
            Font.textureRebuilt += f => { if (f == font && material != null) material.mainTexture = f.material.mainTexture; };
            return material;
        }
    }

    public static Font Font
    {
        get
        {
            if (!fontTried)
            {
                fontTried = true;
                // В Unity 2022.2+ встроенный шрифт называется LegacyRuntime.ttf.
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return font;
        }
    }

    // height — примерная высота букв в метрах.
    public static TextMesh Create(Transform parent, string text, Vector3 position, Quaternion rotation, float height, Color color,
        TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
    {
        var go = new GameObject("Text: " + text.Replace('\n', ' '));
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 96;                 // крупный размер в атласе — чёткие буквы
        tm.characterSize = height / 9.6f; // приводим к нужной высоте
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = style;
        tm.color = color;
        if (Font != null)
        {
            tm.font = Font;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial;
        }
        return tm;
    }

    // Надпись «граффити»: неоновый цвет, чёрная обводка (копии чуть позади), лёгкий наклон.
    public static TextMesh CreateOutlined(Transform parent, string text, Vector3 position, Quaternion rotation, float height, Color color,
        float outline = 0.012f)
    {
        var main = Create(parent, text, position, rotation, height, color);
        Vector3 back = rotation * Vector3.forward * 0.004f; // «позади» — дальше от зрителя
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Vector3 off = rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * outline;
            var o = Create(main.transform, text, position + off + back, rotation, height, new Color(0.03f, 0.02f, 0.05f));
            o.transform.SetParent(main.transform, true);
        }
        return main;
    }
}
