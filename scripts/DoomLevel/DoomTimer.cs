using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class DoomTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float totalTime = 240f;
    public string cutsceneSceneName = "Cutscene";
    [Tooltip("Пройдя этот уровень (пережив его doom), игрок побеждает → сцена итогов.")]
    public int winLevel = 6;
    public bool useFadeTransition = true;
    public float fadeDuration = 1.5f;

    [Header("UI")]
    public TMP_Text timerText;
    public Vector2 timerPosition = new Vector2(0, 450);
    public int fontSize = 48;
    public Color timerColor = Color.white;
    public Color warningColor = Color.red;
    public float warningTime = 30f;

    private float remainingTime;
    private bool timerRunning = true;

    public void SetTotalTime(float t)
{
    totalTime = t;
    remainingTime = t;
    UpdateTimerDisplay();
}

    void Start()
    {
        remainingTime = totalTime;

        if (useFadeTransition && SceneTransition.Instance == null)
        {
            GameObject transitionObj = new GameObject("SceneTransition");
            transitionObj.AddComponent<SceneTransition>();
        }

        if (timerText == null)
        {
            CreateTimerUI();
        }

        UpdateTimerDisplay();
    }

    void CreateTimerUI()
    {
        Debug.LogWarning("DoomTimer: timerText not assigned! Please assign your TextMeshPro text in Inspector.");
    }

    void Update()
    {
        if (!timerRunning) return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            timerRunning = false;
            OnTimerEnd();
        }

        UpdateTimerDisplay();
    }

    void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);
        timerText.text = string.Format("{0}:{1:00}", minutes, seconds);

        if (remainingTime <= warningTime)
        {
            float blink = Mathf.PingPong(Time.time * 2f, 1f);
            timerText.color = Color.Lerp(warningColor, timerColor, blink);
        }
        else
        {
            timerText.color = timerColor;
        }
    }

    void OnTimerEnd()
    {

        GameSession.CompleteLevel(finished =>
        {
        });

        int played = (DifficultyManager.Current != null) ? DifficultyManager.Current.level : 1;
        GameStats.SetLevel(played);
        if (played >= winLevel)
        {
            GameStats.End(GameStats.Outcome.Win);
            return;
        }

        var transition = FindFirstObjectByType<LevelTransitionChat>();
        if (transition != null)
        {
            if (string.IsNullOrEmpty(transition.nextSceneName))
                transition.nextSceneName = cutsceneSceneName;
            transition.Begin();
            return;
        }

        if (!string.IsNullOrEmpty(cutsceneSceneName))
        {
            if (useFadeTransition && SceneTransition.Instance != null)
            {
                SceneTransition.Instance.LoadSceneWithFade(cutsceneSceneName);
            }
            else
            {
                SceneManager.LoadScene(cutsceneSceneName);
            }
        }
        else
        {
            Debug.LogWarning("Cutscene scene name not set!");
        }
    }

    public void PauseTimer()
    {
        timerRunning = false;
    }

    public void ResumeTimer()
    {
        timerRunning = true;
    }

    public void AddTime(float seconds)
    {
        remainingTime += seconds;
    }

    public float GetRemainingTime()
    {
        return remainingTime;
    }
}
