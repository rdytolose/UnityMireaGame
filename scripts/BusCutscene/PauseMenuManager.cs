using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    [Header("Input")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("Audio")]
    public AudioMixer audioMixer;
    public string masterVolumeParameter = "MasterVolume";

    GameObject pausePanel;
    Slider volumeSlider;
    TMP_Text volumeValueText;
    float previousTimeScale = 1f;
    CursorLockMode previousLockState;
    bool previousCursorVisible;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        CreatePauseUI();
        ApplySavedVolume();
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsPaused)
        {
            IsPaused = false;
            if (pausePanel != null) pausePanel.SetActive(false);
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (!IsPaused && SecurityCamerasController.TryCloseActivePanel())
                return;

            if (IsPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        if (IsPaused) return;

        IsPaused = true;
        previousTimeScale = Time.timeScale;
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        Time.timeScale = 0f;
        AudioListener.pause = true;
        pausePanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        AudioListener.pause = false;
        pausePanel.SetActive(false);

        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
    }

    public void ExitGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        pausePanel.SetActive(false);
        GameStats.End(GameStats.Outcome.Quit);
    }

    public void SetVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();

        AudioListener.volume = value;

        if (audioMixer != null)
        {
            float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
            audioMixer.SetFloat(masterVolumeParameter, db);
        }

        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    void ApplySavedVolume()
    {
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = savedVolume;
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(savedVolume);
        SetVolume(savedVolume);
    }

    void CreatePauseUI()
    {
        GameObject canvasObject = new GameObject("PauseMenuCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        pausePanel = new GameObject("PauseMenuPanel");
        pausePanel.transform.SetParent(canvasObject.transform, false);

        Image background = pausePanel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.82f);

        RectTransform panelRect = pausePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        CreateText(pausePanel.transform, "ПАУЗА", 64, new Vector2(0f, 260f), new Vector2(700f, 90f));
        CreateButton(pausePanel.transform, "Продолжить", new Vector2(0f, 120f), ResumeGame);
        CreateButton(pausePanel.transform, "Выйти", new Vector2(0f, 10f), ExitGame);
        CreateVolumeBlock(pausePanel.transform);

        pausePanel.SetActive(false);
    }

    TMP_Text CreateText(Transform parent, string text, int fontSize, Vector2 position, Vector2 size)
    {
        GameObject textObject = new GameObject(text);
        textObject.transform.SetParent(parent, false);

        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return label;
    }

    Button CreateButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(text + "Button");
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.28f, 0.28f, 1f);
        colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        button.colors = colors;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(420f, 82f);

        TMP_Text label = CreateText(buttonObject.transform, text, 34, Vector2.zero, rect.sizeDelta);
        label.raycastTarget = false;

        return button;
    }

    void CreateVolumeBlock(Transform parent)
    {
        CreateText(parent, "Громкость", 34, new Vector2(0f, -130f), new Vector2(420f, 55f));

        GameObject sliderObject = new GameObject("VolumeSlider");
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(0f, -195f);
        sliderRect.sizeDelta = new Vector2(420f, 35f);

        volumeSlider = sliderObject.AddComponent<Slider>();
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        volumeSlider.onValueChanged.AddListener(SetVolume);

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(sliderObject.transform, false);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fillAreaObject = new GameObject("Fill Area");
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(10f, 0f);
        fillAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        Image fill = fillObject.AddComponent<Image>();
        fill.color = new Color(0.35f, 0.7f, 1f, 1f);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleAreaObject = new GameObject("Handle Slide Area");
        handleAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleAreaObject.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handleObject = new GameObject("Handle");
        handleObject.transform.SetParent(handleAreaObject.transform, false);
        Image handle = handleObject.AddComponent<Image>();
        handle.color = Color.white;
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(28f, 44f);

        volumeSlider.fillRect = fillRect;
        volumeSlider.handleRect = handleRect;
        volumeSlider.targetGraphic = handle;

        volumeValueText = CreateText(parent, "100%", 26, new Vector2(0f, -240f), new Vector2(180f, 40f));
    }
}
