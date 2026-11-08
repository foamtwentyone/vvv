using System.Collections.Generic;
using UnityEngine;

// Цветные материалы для примитивов. Берём материал по умолчанию у куба и копируем его,
// поэтому работает и в Built-in, и в URP (без розовых «сломанных» шейдеров).
public static class ProtoMaterials
{
    static Material baseMaterial;
    static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

    public static Material Get(Color color)
    {
        if (cache.TryGetValue(color, out var cached) && cached != null) return cached;

        if (baseMaterial == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMaterial = temp.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(temp);
        }

        var mat = new Material(baseMaterial) { color = color };
        cache[color] = mat;
        return mat;
    }
}
