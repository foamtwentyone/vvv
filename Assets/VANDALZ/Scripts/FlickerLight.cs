using UnityEngine;

// Лампа дневного света. Обычно горит ровно, иногда «моргает».
// broken = true — неисправная: мигает часто и нервно, с жужжанием.
public class FlickerLight : MonoBehaviour
{
    public Light lamp;
    public Renderer tube;
    public bool broken;
    public float baseIntensity = 1.4f;

    Material tubeMaterial;
    Color tubeColor;
    float nextFlicker;
    float flickerUntil;
    AudioSource buzz;

    void Start()
    {
        if (tube != null)
        {
            tubeMaterial = new Material(tube.sharedMaterial);
            tubeMaterial.EnableKeyword("_EMISSION");
            tubeColor = lamp != null ? lamp.color : Color.white;
            tube.sharedMaterial = tubeMaterial;
        }
        nextFlicker = Time.time + Random.Range(2f, 12f);

        if (broken)
        {
            buzz = gameObject.AddComponent<AudioSource>();
            buzz.clip = ProcAudio.LampBuzz;
            buzz.loop = true;
            buzz.spatialBlend = 1f;
            buzz.minDistance = 1f;
            buzz.maxDistance = 8f;
            buzz.volume = 0.25f;
            buzz.Play();
        }
    }

    void Update()
    {
        float k = 1f;
        if (broken)
        {
            // Нервное мигание: шум + редкие полные отключения.
            float n = Mathf.PerlinNoise(Time.time * 9f, transform.position.x);
            k = n > 0.55f ? 1f : (n > 0.4f ? 0.35f : 0.05f);
        }
        else
        {
            if (Time.time > nextFlicker)
            {
                flickerUntil = Time.time + Random.Range(0.1f, 0.5f);
                nextFlicker = Time.time + Random.Range(4f, 15f);
            }
            if (Time.time < flickerUntil) k = Random.value > 0.5f ? 1f : 0.15f;
        }

        if (lamp != null) lamp.intensity = baseIntensity * k;
        if (tubeMaterial != null) tubeMaterial.SetColor("_EmissionColor", tubeColor * (1.8f * k));
        if (buzz != null) buzz.volume = 0.25f * k;
    }
}
