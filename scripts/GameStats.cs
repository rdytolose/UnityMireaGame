using UnityEngine;
using UnityEngine.SceneManagement;

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
    public bool transitionChatShown = false;

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

    public static void AddKill() { Ensure().kills++; }
    public static void AddShot() { Ensure().shotsFired++; }
    public static void AddHit()  { Ensure().hits++; }
    public static void SetWeapon(string w) { if (!string.IsNullOrEmpty(w)) Ensure().lastWeapon = w; }
    public static void SetLevel(int lvl) { var s = Ensure(); if (lvl > s.maxLevel) s.maxLevel = lvl; }

    public static void ResetRun()
    {
        var s = Ensure();
        s.kills = 0; s.shotsFired = 0; s.hits = 0; s.maxLevel = 1;
        s.lastWeapon = "pistol"; s.outcome = Outcome.Lose;
        s.transitionChatShown = false;
    }

    public static void End(Outcome o)
    {
        var s = Ensure();
        s.outcome = o;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        GameApi.Ensure();
        GameSession.SetScreen("over_" + o.ToString().ToLower());

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadSceneWithFade(s.resultsScene);
        else
            SceneManager.LoadScene(s.resultsScene);
    }
}
