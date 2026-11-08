using System.Collections.Generic;
using UnityEngine;

// Материалы объектов. Если доступен мультяшный шейдер VANDALZ/Toon (cel-освещение + чёрный контур),
// все объекты игры рисуются им. Если нет (шейдер не собрался или другой конвейер) — откат на
// материал по умолчанию: игра выглядит проще, но не «розовая».
public static class ProtoMaterials
{
    static Material baseMaterial;
    static bool toonChecked;
    static Shader toon;
    static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

    public static bool IsToon => Toon != null;

    public static Shader Toon
    {
        get
        {
            if (!toonChecked)
            {
                toonChecked = true;
                toon = Shader.Find("VANDALZ/Toon");
                if (toon == null) toon = Resources.Load<Shader>("VandalzToon");
                if (toon != null && !toon.isSupported) toon = null;
            }
            return toon;
        }
    }

    public static Material Base
    {
        get
        {
            if (baseMaterial != null) return baseMaterial;
            if (Toon != null)
            {
                baseMaterial = new Material(Toon) { name = "VANDALZ Toon" };
            }
            else
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = temp.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(temp);
            }
            return baseMaterial;
        }
    }

    public static Material Get(Color color)
    {
        if (cache.TryGetValue(color, out var cached) && cached != null) return cached;
        var mat = new Material(Base) { color = color };
        cache[color] = mat;
        return mat;
    }

    // Настроить контур: ширина в пикселях, цвет, раздувать от центра (коробки) или по нормалям (гладкие меши).
    public static void SetOutline(Material m, float widthPx, Color color, bool fromCenter = true)
    {
        if (m == null || !m.HasProperty("_OutlineWidth")) return;
        m.SetFloat("_OutlineWidth", widthPx);
        m.SetColor("_OutlineColor", color);
        m.SetFloat("_OutlineFromCenter", fromCenter ? 1f : 0f);
    }

    public static void SetEmission(Material m, Color emission)
    {
        if (m == null) return;
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
    }

    // Светлая кайма по краю силуэта (объём, «глянец» краски).
    public static void SetRim(Material m, Color color, float power = 3f)
    {
        if (m == null || !m.HasProperty("_RimColor")) return;
        m.SetColor("_RimColor", color);
        m.SetFloat("_RimPower", power);
    }

    // Резкий мультяшный блик от ламп (мокрая краска, лужи, металл).
    public static void SetHighlight(Material m, Color color)
    {
        if (m == null || !m.HasProperty("_ToonSpec")) return;
        m.SetColor("_ToonSpec", color);
    }
}
