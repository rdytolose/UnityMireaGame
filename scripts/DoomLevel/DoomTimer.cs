using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Таймер на 4 минуты с переходом на катсцену
public class DoomTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float totalTime = 240f; // 4 минуты = 240 секунд
    public string cutsceneSceneName = "Cutscene"; // Название сцены катсцены
    [Tooltip("Пройдя этот уровень (пережив его doom), игрок побеждает → сцена итогов.")]
    public int winLevel = 6;
    public bool useFadeTransition = true; // Использовать плавный переход
    public float fadeDuration = 1.5f; // Длительность затухания

    [Header("UI")]
    public TMP_Text timerText; // TextMeshPro для отображения времени
    public Vector2 timerPosition = new Vector2(0, 450); // Позиция вверху экрана
    public int fontSize = 48;
    public Color timerColor = Color.white;
    public Color warningColor = Color.red; // Цвет когда осталось мало времени
    public float warningTime = 30f; // Когда начинать мигать красным

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

        // Создаём SceneTransition если его нет
        if (useFadeTransition && SceneTransition.Instance == null)
        {
            GameObject transitionObj = new GameObject("SceneTransition");
            transitionObj.AddComponent<SceneTransition>();
        }

        // Создаем UI если его нет
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

        // Форматируем время как MM:SS
        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);
        timerText.text = string.Format("{0}:{1:00}", minutes, seconds);

        // Меняем цвет если осталось мало времени
        if (remainingTime <= warningTime)
        {
            // Мигание красным
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

        // Двигаем прогресс (level +1) перед переходом — GameApi с DontDestroyOnLoad,
        // поэтому POST успеет уйти даже при немедленном переходе.
        GameSession.CompleteLevel(finished =>
        {
        });

        // Победа: пережил doom уровня winLevel → сцена итогов (магазин уже не нужен).
        int played = (DifficultyManager.Current != null) ? DifficultyManager.Current.level : 1;
        GameStats.SetLevel(played);
        if (played >= winLevel)
        {
            GameStats.End(GameStats.Outcome.Win);
            return;
        }

        // Если в сцене есть переходный чат — отдаём управление ему: покажет видео/«смотри
        // в телефон», дождётся диалога и сам загрузит магазин. Иначе — старое поведение.
        var transition = FindFirstObjectByType<LevelTransitionChat>();
        if (transition != null)
        {
            if (string.IsNullOrEmpty(transition.nextSceneName))
                transition.nextSceneName = cutsceneSceneName;
            transition.Begin();
            return;
        }

        // Переход на катсцену
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

    // Публичные методы для управления таймером
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
