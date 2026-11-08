using UnityEngine;

// Общая система частиц для разовых эффектов: пыль при приземлении, искры при подборе,
// серые брызги, пыль с потолка. Одна система на всю игру — дёшево.
public static class FX
{
    static ParticleSystem ps;

    static ParticleSystem System
    {
        get
        {
            if (ps != null) return ps;
            var go = new GameObject("FX");
            ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = PaintTextures.Particle;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }

    // Облачко: count частиц разлетаются от pos, чуть поднимаясь.
    public static void Puff(Vector3 pos, Color color, int count, float speed, float size = 0.2f, float life = 0.6f, float gravity = 0f)
    {
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.6f;
            p.position = pos + Random.insideUnitSphere * 0.1f;
            p.velocity = dir * speed * Random.Range(0.4f, 1f) + Vector3.down * gravity;
            p.startSize = size * Random.Range(0.6f, 1.3f);
            p.startLifetime = life * Random.Range(0.7f, 1.2f);
            p.startColor = color;
            System.Emit(p, 1);
        }
    }

    // Пыль сыплется сверху в области (центр, размер) — для «прошёл поезд».
    public static void DustFall(Vector3 center, Vector3 size, int count)
    {
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            p.position = center + Vector3.Scale(new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f), size);
            p.velocity = new Vector3(Random.Range(-0.1f, 0.1f), -Random.Range(0.6f, 1.6f), Random.Range(-0.1f, 0.1f));
            p.startSize = Random.Range(0.03f, 0.12f);
            p.startLifetime = Random.Range(2.5f, 4.5f);
            p.startColor = new Color(0.75f, 0.7f, 0.6f, 0.7f);
            System.Emit(p, 1);
        }
    }
}
