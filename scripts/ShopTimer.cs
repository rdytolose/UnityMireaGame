using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ShopTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float totalTime = 180f;
    public string nextSceneName = "EquipmentShop";
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
    private Canvas timerCanvas;

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
        GameObject canvasObj = new GameObject("TimerCanvas");

        canvasObj.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        timerCanvas = canvas;

        GameObject textObj = new GameObject("ShopTimerText");

        textObj.layer = LayerMask.NameToLayer("UI");

        textObj.transform.SetParent(timerCanvas.transform, false);

        timerText = textObj.AddComponent<TextMeshProUGUI>();
        timerText.fontSize = fontSize;
        timerText.color = timerColor;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.raycastTarget = false;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
        if (font == null)
        {
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

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (useFadeTransition && SceneTransition.Instance != null)
            {
                SceneTransition.Instance.LoadSceneWithFade(nextSceneName);
            }
            else
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }
        else
        {
            Debug.LogWarning("Next scene name not set!");
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
