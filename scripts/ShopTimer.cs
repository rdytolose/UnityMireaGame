using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Таймер для сцены магазина с переходом на следующую сцену
/// </summary>
public class ShopTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float totalTime = 180f; // 3 минуты = 180 секунд
    public string nextSceneName = "EquipmentShop"; // Название следующей сцены
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
    private Canvas timerCanvas;

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
        // ВСЕГДА создаём отдельный Canvas для таймера
        GameObject canvasObj = new GameObject("TimerCanvas");
        
        // НОВОЕ: Устанавливаем слой UI для объекта Canvas
        canvasObj.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // НИЖЕ основного UI магазина
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // НЕ добавляем GraphicRaycaster - таймер не должен перехватывать клики
        timerCanvas = canvas;

        // Создаем текст таймера
        GameObject textObj = new GameObject("ShopTimerText");
        
        // НОВОЕ: Устанавливаем слой UI для объекта текста
        textObj.layer = LayerMask.NameToLayer("UI");
        
        textObj.transform.SetParent(timerCanvas.transform, false);
        
        timerText = textObj.AddComponent<TextMeshProUGUI>();
        timerText.fontSize = fontSize;
        timerText.color = timerColor;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.raycastTarget = false; // НЕ блокируем клики
        
        // Пытаемся загрузить шрифт, если не получается - используем дефолтный
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
        if (font == null)
        {
            // Ищем любой доступный TMP шрифт
            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (fonts.Length > 0)
                font = fonts[0];
        }
        if (font != null)
        {
            timerText.font = font;
        }
        
        RectTransform rect = timerText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = timerPosition;
        rect.sizeDelta = new Vector2(400, 100);

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
        
        // Переход на следующую сцену
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (useFadeTransition && SceneTransition.Instance != null)
            {
                // Используем плавный переход с затуханием
                SceneTransition.Instance.LoadSceneWithFade(nextSceneName);
            }
            else
            {
                // Обычная загрузка сцены
                SceneManager.LoadScene(nextSceneName);
            }
        }
        else
        {
            Debug.LogWarning("Next scene name not set!");
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

    public void SetTime(float seconds)
    {
        remainingTime = seconds;
    }

    public float GetRemainingTime()
    {
        return remainingTime;
    }

    public bool IsRunning()
    {
        return timerRunning;
    }
}
