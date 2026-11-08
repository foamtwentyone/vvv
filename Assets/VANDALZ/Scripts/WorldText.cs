using UnityEngine;

// Надписи прямо в мире (таблички, подсказки, название станции) на встроенном шрифте.
public static class WorldText
{
    static Font font;
    static bool fontTried;

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
            go.GetComponent<MeshRenderer>().sharedMaterial = Font.material;
        }
        return tm;
    }
}
