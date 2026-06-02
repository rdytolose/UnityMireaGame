using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Накопитель статистики забега + триггер конца игры. Синглтон (DontDestroyOnLoad),
/// живёт между сценами. Создаётся сам при первом обращении (Ensure), отдельный объект
/// в сцену класть не обязательно.
///
/// Исходы:
///   Win  — прошёл winLevel уровней (см. DoomTimer);
///   Lose — умер на doom-уровне (DoomHealth игрока);
///   Quit — вышел через паузу (PauseMenuManager).
/// Любой исход ведёт на сцену итогов (resultsScene) — см. GameOverManager.
/// </summary>
public class GameStats : MonoBehaviour
{
    public static GameStats I { get; private set; }

    public enum Outcome { Win, Lose, Quit }

    [Header("Статистика забега")]
    public int kills;
    public int shotsFired;
    public int hits;
    public int maxLevel = 1;
    public string lastWeapon = "pistol";
    public Outcome outcome = Outcome.Lose;
    public bool transitionChatShown = false;   // переходный диалог doom→shop показан (раз за забег)

    [Header("Сцены")]
    public string resultsScene = "Results";

    public static GameStats Ensure()
    {
        if (I == null)
        {
            var go = new GameObject("GameStats");
            I = go.AddComponent<GameStats>();
            DontDestroyOnLoad(go);
        }
        return I;
    }

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    // ---- Хуки (зовут другие скрипты) ----
    public static void AddKill() { Ensure().kills++; }
    public static void AddShot() { Ensure().shotsFired++; }
    public static void AddHit()  { Ensure().hits++; }
    public static void SetWeapon(string w) { if (!string.IsNullOrEmpty(w)) Ensure().lastWeapon = w; }
    public static void SetLevel(int lvl) { var s = Ensure(); if (lvl > s.maxLevel) s.maxLevel = lvl; }

    /// <summary>Сбросить статы перед новым забегом (зовётся при «начать заново»).</summary>
    public static void ResetRun()
    {
        var s = Ensure();
        s.kills = 0; s.shotsFired = 0; s.hits = 0; s.maxLevel = 1;
        s.lastWeapon = "pistol"; s.outcome = Outcome.Lose;
        s.transitionChatShown = false;
    }

    /// <summary>Завершить игру с исходом и уйти на сцену итогов.</summary>
    public static void End(Outcome o)
    {
        var s = Ensure();
        s.outcome = o;
        Time.timeScale = 1f;           // на случай заморозки (смерть/пауза/переход)
        AudioListener.pause = false;

        // Сообщаем телефону исход — он покажет сообщение и вернётся на главный экран.
        // (токен игры ещё жив; у телефона свой site-токен, опрос /api/state/screen работает)
        GameApi.Ensure();
        GameSession.SetScreen("over_" + o.ToString().ToLower());   // over_win | over_lose | over_quit

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadSceneWithFade(s.resultsScene);
        else
            SceneManager.LoadScene(s.resultsScene);
    }
}
