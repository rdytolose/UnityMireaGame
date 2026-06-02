using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Переходный «уровень закупки» между Shop и Doom.
/// На экране — только надпись «магазин в телефоне» и обратный отсчёт.
/// Покупки игрок делает в companion-приложении (бэкенд /api/shop/*),
/// поэтому здесь геймплея нет — мы лишь говорим телефону показать shop.html
/// (GameSession.SetScreen) и через buyDuration секунд уходим в следующую сцену.
///
/// Повесь этот компонент на пустой объект в сцене EquipmentShop.
/// </summary>
public class EquipmentPhaseManager : MonoBehaviour
{
    [Header("Фаза закупки")]
    [Tooltip("Сколько секунд даётся на закупку в телефоне")]
    public float buyDuration = 45f;
    [Tooltip("Куда уходим после закупки (сцена боя)")]
    public string nextSceneName = "doom";
    public bool useFadeTransition = true;
    public float fadeDuration = 1.5f;

    [Header("Companion-экран")]
    [Tooltip("Что показать на телефоне: обычно shop")]
    public string phoneScreen = "shop";

    [Header("UI (опционально — создастся само, если пусто)")]
    [Tooltip("Только таймер. Всё остальное (текст, деньги) — на твоём изображении сцены.")]
    public TMP_Text timerText;   // обратный отсчёт (только числа)

    [Header("Цвета таймера")]
    public int fontSize = 48;
    public Color timerColor = Color.white;
    public Color warningColor = Color.red;
    public float warningTime = 10f;

    [Header("Управление")]
    [Tooltip("Клавиша досрочного старта рейда (None — выключено)")]
    public KeyCode skipKey = KeyCode.None;

    float remaining;
    bool running = true;

    void Start()
    {
        // Бэкенд: создаём клиент и переключаем телефон на страницу магазина.
        GameApi.Ensure();
        GameSession.SetScreen(phoneScreen);

        // Гарантируем наличие SceneTransition для плавного перехода.
        if (useFadeTransition && SceneTransition.Instance == null)
        {
            var go = new GameObject("SceneTransition");
            go.AddComponent<SceneTransition>();
        }

        // Тут нет геймплея — освобождаем курсор.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (timerText == null) CreateUI();

        remaining = buyDuration;
        UpdateTimer();
    }

    void Update()
    {
        if (!running) return;

        if (skipKey != KeyCode.None && Input.GetKeyDown(skipKey))
        {
            StartRaidNow();
            return;
        }

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            running = false;
            GoToRaid();
        }
        UpdateTimer();
    }

    void UpdateTimer()
    {
        if (timerText == null) return;
        int sec = Mathf.FloorToInt(remaining);                 // целые секунды
        int cs = Mathf.FloorToInt((remaining - sec) * 100f);   // сотые (00..99)
        timerText.text = $"{sec:00}.{cs:00}";                  // формат SS.cc, напр. 05.22

        timerText.color = remaining <= warningTime
            ? Color.Lerp(warningColor, timerColor, Mathf.PingPong(Time.time * 2f, 1f))
            : timerColor;
    }

    /// <summary>Досрочно начать рейд (повесь на кнопку или вызови из кода).</summary>
    public void StartRaidNow()
    {
        if (!running) return;
        running = false;
        GoToRaid();
    }

    void GoToRaid()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[EquipmentPhase] nextSceneName не задан!");
            return;
        }

        if (useFadeTransition && SceneTransition.Instance != null)
            SceneTransition.Instance.LoadSceneWithFade(nextSceneName);
        else
            SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>
    /// Создаёт безопасный оверлей-Canvas с надписями. Без GraphicRaycaster и с
    /// raycastTarget=false — НИКОГДА не перехватывает клики/ввод.
    /// </summary>
    void CreateUI()
    {
        var canvasObj = new GameObject("EquipmentPhaseCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // GraphicRaycaster НЕ добавляем намеренно.

        TMP_FontAsset font = ResolveFont();

        // Только таймер. Позицию по умолчанию — вверху по центру; если нужно точно
        // над твоим изображением, создай свой TMP_Text и кинь в поле Timer Text.
        if (timerText == null)
            timerText = MakeText(canvas.transform, "TimerText", font, fontSize,
                new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(400, 120));
    }

    TMP_Text MakeText(Transform parent, string name, TMP_FontAsset font, int size,
                      Vector2 anchor, Vector2 pos, Vector2 dims)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var t = obj.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = timerColor;
        t.raycastTarget = false;
        if (font != null) t.font = font;

        var rect = t.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta = dims;
        return t;
    }

    TMP_FontAsset ResolveFont()
    {
        var font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
        if (font == null)
        {
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (all.Length > 0) font = all[0];
        }
        return font;
    }
}
