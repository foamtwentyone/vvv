using System;
using System.Collections.Generic;
using UnityEngine;

// Золотая крышка от баллона — спрятанная находка. Лежит в труднодоступных местах:
// без краски (полки, мосты) не достать. Подбирается касанием. Влияет на оценку уровня.
public class GoldenCap : MonoBehaviour
{
    public static readonly List<GoldenCap> All = new List<GoldenCap>();
    public static event Action<GoldenCap> Collected;

    public bool IsCollected { get; private set; }
    public Transform player;

    Vector3 basePos;
    Transform model;
    Light glow;

    public static GoldenCap Create(Transform parent, Vector3 position, Transform player)
    {
        var go = new GameObject("GoldenCap");
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var cap = go.AddComponent<GoldenCap>();
        cap.player = player;
        cap.basePos = position;

        cap.model = new GameObject("Model").transform;
        cap.model.SetParent(go.transform, false);
        var gold = new Material(ProtoMaterials.Get(new Color(1f, 0.78f, 0.15f)));
        ProtoMaterials.SetEmission(gold, new Color(1f, 0.6f, 0.1f) * 0.9f);
        ProtoMaterials.SetHighlight(gold, Color.white);
        Part(PrimitiveType.Cylinder, cap.model, Vector3.zero, new Vector3(0.26f, 0.08f, 0.26f), gold);
        Part(PrimitiveType.Cylinder, cap.model, new Vector3(0f, 0.1f, 0f), new Vector3(0.12f, 0.04f, 0.12f), gold);

        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(go.transform, false);
        cap.glow = lightGo.AddComponent<Light>();
        cap.glow.type = LightType.Point;
        cap.glow.range = 3f;
        cap.glow.intensity = 1.6f;
        cap.glow.color = new Color(1f, 0.75f, 0.3f);
        return cap;
    }

    static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        if (IsCollected) return;
        float t = Time.time;
        model.localRotation = Quaternion.Euler(20f, t * 120f, 0f);
        transform.position = basePos + Vector3.up * (0.1f * Mathf.Sin(t * 2.2f));
        glow.intensity = 1.4f + 0.4f * Mathf.Sin(t * 5f);

        if (player != null && (player.position + Vector3.up * 0.9f - transform.position).sqrMagnitude < 1.1f * 1.1f)
            Collect();
    }

    void Collect()
    {
        IsCollected = true;
        FX.Puff(transform.position, new Color(1f, 0.85f, 0.3f), 30, 3f, 0.12f, 0.8f);
        ProcAudio.Play2D(ProcAudio.Collect, 0.9f);
        model.gameObject.SetActive(false);
        glow.enabled = false;
        Collected?.Invoke(this);
    }

    // Вернуть на место (рестарт уровня).
    public void ResetCap()
    {
        IsCollected = false;
        model.gameObject.SetActive(true);
        glow.enabled = true;
    }
}
