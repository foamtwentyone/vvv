using UnityEngine;

// Музыка: лоу-фай бит по кругу + тревожный слой, который вступает, когда Закрасчик охотится рядом.
// Оба слоя одной длины и запускаются в одну и ту же долю секунды — играют синхронно.
public class MusicPlayer : MonoBehaviour
{
    public float volume = 0.45f;
    public float dangerVolume = 0.5f;
    [Tooltip("Насколько тише музыка на заставке и в паузе")]
    public float menuDuck = 0.6f;
    public GameFlow flow;

    AudioSource baseSource, dangerSource;
    float danger;

    void Start()
    {
        baseSource = MakeSource(ProcAudio.MusicBase);
        dangerSource = MakeSource(ProcAudio.MusicDanger);
        double at = AudioSettings.dspTime + 0.2;
        baseSource.PlayScheduled(at);
        dangerSource.PlayScheduled(at);
    }

    AudioSource MakeSource(AudioClip clip)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.spatialBlend = 0f;
        s.playOnAwake = false;
        s.volume = 0f;
        return s;
    }

    void Update()
    {
        float threat = 0f;
        foreach (var e in PaintEater.All) if (e != null) threat = Mathf.Max(threat, e.Threat);
        danger = Mathf.MoveTowards(danger, threat, Time.unscaledDeltaTime * (threat > danger ? 1.5f : 0.4f));

        bool menu = flow != null && flow.State != GameState.Playing;
        float duck = menu ? menuDuck : 1f;
        baseSource.volume = volume * duck;
        dangerSource.volume = dangerVolume * danger * duck;
    }
}
