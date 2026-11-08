using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState
{
    Title,    // заставка, ждём клика
    Playing,
    Paused,   // Esc
    Finished, // дошли до выхода
}

// Ход игры: заставка → игра → пауза / финиш, чекпоинты, падение в пропасть с затемнением,
// рестарт (R), фоновый звук. Интерфейс рисует VandalzHUD, читая отсюда состояние.
public class GameFlow : MonoBehaviour
{
    [Serializable]
    public struct Checkpoint
    {
        public string name;
        public Bounds zone;    // стоишь здесь на земле — чекпоинт твой
        public Vector3 spawn;
        public float yaw;
    }

    public FirstPersonController player;
    public PaintSpray spray;
    public CanInventory inventory;
    public CameraRig cameraRig;

    public string levelName = "Станция «Заброшенная»";
    public float killY = -3.5f;
    public Bounds finishZone;
    public List<Checkpoint> checkpoints = new List<Checkpoint>();

    public GameState State { get; private set; } = GameState.Title;
    public float Elapsed { get; private set; }
    public int Falls { get; private set; }
    public int TagsDone { get { int n = 0; foreach (var t in TagSpot.All) if (t.Completed) n++; return n; } }
    public int TagsTotal => TagSpot.All.Count;
    public float FadeAlpha { get; private set; } = 1f; // чёрный экран (затемнение)
    public float HurtFlash { get; private set; }        // красная вспышка
    public float StateTime => Time.unscaledTime - stateSince;

    // Сообщение на экран: текст и цвет.
    public event Action<string, Color> Toast;
    // Рестарт (R): сюда подписывается SubwayLevelBuilder — вернуть баллоны и споты.
    public event Action Restarted;

    int checkpointIndex;
    bool respawning;
    float stateSince;
    AudioSource ambience;

    void Start()
    {
        if (inventory != null) inventory.Notify += (text, color) => Toast?.Invoke(text, color);
        TagSpot.TagCompleted += OnTagCompleted;
        PaintEater.PlayerHit += OnPlayerHit;
        if (player != null) player.Landed += OnLanded;

        // Гул заброшенного метро.
        ambience = gameObject.AddComponent<AudioSource>();
        ambience.clip = ProcAudio.AmbientDrone;
        ambience.loop = true;
        ambience.spatialBlend = 0f;
        ambience.volume = 0.35f;
        ambience.Play();

        SetState(GameState.Title);
    }

    void OnDestroy()
    {
        TagSpot.TagCompleted -= OnTagCompleted;
        PaintEater.PlayerHit -= OnPlayerHit;
    }

    void OnTagCompleted(TagSpot spot)
    {
        Toast?.Invoke($"ТЕГ!  {TagsDone}/{TagsTotal}", new Color(1f, 0.82f, 0.2f));
    }

    void OnPlayerHit()
    {
        HurtFlash = 1f;
        Toast?.Invoke("Закрасчик толкнул тебя!", new Color(0.75f, 0.75f, 0.8f));
    }

    void OnLanded(float speed)
    {
        if (speed > 5f) ProcAudio.Play2D(ProcAudio.Land, Mathf.Clamp01(speed / 14f) * 0.9f, UnityEngine.Random.Range(0.9f, 1.05f));
    }

    void SetState(GameState s)
    {
        State = s;
        stateSince = Time.unscaledTime;
        bool playing = s == GameState.Playing;
        if (player != null) player.InputEnabled = playing;
        if (spray != null) spray.InputEnabled = playing;
        if (inventory != null) inventory.InputEnabled = playing;
        FirstPersonController.SetCursorLocked(playing);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!respawning) FadeAlpha = Mathf.MoveTowards(FadeAlpha, 0f, dt * 1.5f);
        HurtFlash = Mathf.MoveTowards(HurtFlash, 0f, dt * 2f);

        switch (State)
        {
            case GameState.Title:
                if (VandalzInput.ClickPressed() && StateTime > 0.3f)
                {
                    ProcAudio.Play2D(ProcAudio.Rattle, 0.6f);
                    SetState(GameState.Playing);
                    Toast?.Invoke("Выберись со станции. Найди и закрась споты для тегов", Color.white);
                }
                break;

            case GameState.Playing:
                Elapsed += Time.deltaTime;
                if (VandalzInput.RestartPressed()) { Restart(); break; }
                // Esc или курсор вырвался (переключились из окна) — пауза.
                if (StateTime > 0.3f && (VandalzInput.PausePressed() || Cursor.lockState != CursorLockMode.Locked))
                {
                    SetState(GameState.Paused);
                    break;
                }
                UpdateCheckpoints();
                if (!respawning && player.transform.position.y < killY) StartCoroutine(Respawn());
                if (finishZone.Contains(player.transform.position)) Finish();
                break;

            case GameState.Paused:
                if (VandalzInput.RestartPressed()) { Restart(); break; }
                if (VandalzInput.ClickPressed() && StateTime > 0.2f) SetState(GameState.Playing);
                break;

            case GameState.Finished:
                if (VandalzInput.RestartPressed()) Restart();
                break;
        }
    }

    void UpdateCheckpoints()
    {
        if (!player.IsGrounded) return;
        Vector3 p = player.transform.position;
        for (int i = checkpointIndex + 1; i < checkpoints.Count; i++)
        {
            if (checkpoints[i].zone.Contains(p))
            {
                checkpointIndex = i;
                Toast?.Invoke("Чекпоинт: " + checkpoints[i].name, new Color(0.5f, 1f, 0.6f));
                ProcAudio.Play2D(ProcAudio.Pickup, 0.5f, 0.8f);
            }
        }
    }

    IEnumerator Respawn()
    {
        respawning = true;
        Falls++;
        ProcAudio.Play2D(ProcAudio.Whoosh, 0.8f);
        while (FadeAlpha < 1f)
        {
            FadeAlpha = Mathf.MoveTowards(FadeAlpha, 1f, Time.unscaledDeltaTime * 4f);
            yield return null;
        }
        TeleportToCheckpoint();
        yield return new WaitForSecondsRealtime(0.15f);
        respawning = false;
        Toast?.Invoke("Упал! Краска осталась — пробуй ещё", new Color(1f, 0.5f, 0.5f));
    }

    void TeleportToCheckpoint()
    {
        if (checkpoints.Count == 0) return;
        var c = checkpoints[Mathf.Clamp(checkpointIndex, 0, checkpoints.Count - 1)];
        player.Teleport(c.spawn, c.yaw);
    }

    void Finish()
    {
        if (State == GameState.Finished) return;
        SetState(GameState.Finished);
        ProcAudio.Play2D(ProcAudio.Finish, 0.9f);
    }

    public void Restart()
    {
        StopAllCoroutines();
        respawning = false;
        if (spray != null) spray.ResetPaint();
        foreach (var t in TagSpot.All) t.ResetTag();
        Restarted?.Invoke();
        checkpointIndex = 0;
        TeleportToCheckpoint();
        Elapsed = 0f;
        Falls = 0;
        FadeAlpha = 1f;
        SetState(GameState.Playing);
        Toast?.Invoke("Заново!", Color.white);
    }
}
