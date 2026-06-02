using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Управляет плавными переходами между сценами с эффектом затухания
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("Fade Settings")]
    public float fadeDuration = 1f;
    public Color fadeColor = Color.black;

    private Image fadeImage;
    private Canvas fadeCanvas;
    private bool isTransitioning = false;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateFadeUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void CreateFadeUI()
    {
        // Создаём Canvas для fade эффекта
        GameObject canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform);
        
        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // Поверх всего

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // НЕ добавляем GraphicRaycaster: фейд-оверлею не нужно принимать клики,
        // ему достаточно рисоваться. Иначе этот Canvas (sortingOrder 9999), переехав
        // из doom в Shop, перехватывал бы нажатия по кнопкам.

        // Создаём Image для затемнения
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        // ВАЖНО: пока оверлей прозрачный, он НЕ должен перехватывать клики UI.
        // Иначе невидимый фуллскрин-Image поверх всего блокирует HUD/кнопки.
        // Включается только на время фейда (см. FadeOut/FadeIn).
        fadeImage.raycastTarget = false;
        RectTransform rect = fadeImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

    }

    /// <summary>
    /// Загрузить сцену с плавным переходом
    /// </summary>
    public void LoadSceneWithFade(string sceneName)
    {
        if (!isTransitioning)
        {
            StartCoroutine(TransitionToScene(sceneName));
        }
    }

    /// <summary>
    /// Загрузить сцену с плавным переходом (по индексу)
    /// </summary>
    public void LoadSceneWithFade(int sceneIndex)
    {
        if (!isTransitioning)
        {
            StartCoroutine(TransitionToScene(sceneIndex));
        }
    }

    IEnumerator TransitionToScene(string sceneName)
    {
        isTransitioning = true;

        // Fade out (затемнение)
        yield return StartCoroutine(FadeOut());

        // Загружаем сцену
        SceneManager.LoadScene(sceneName);

        // Fade in (осветление)
        yield return StartCoroutine(FadeIn());

        isTransitioning = false;
    }

    IEnumerator TransitionToScene(int sceneIndex)
    {
        isTransitioning = true;

        // Fade out (затемнение)
        yield return StartCoroutine(FadeOut());

        // Загружаем сцену
        SceneManager.LoadScene(sceneIndex);

        // Fade in (осветление)
        yield return StartCoroutine(FadeIn());

        isTransitioning = false;
    }

    IEnumerator FadeOut()
    {
        float elapsed = 0f;
        Color startColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        Color endColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);

        // На время перехода блокируем ввод (снимется в FadeIn, когда станет прозрачным).
        fadeImage.raycastTarget = true;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            fadeImage.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        fadeImage.color = endColor;
    }

    IEnumerator FadeIn()
    {
        float elapsed = 0f;
        Color startColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        Color endColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            Color c = Color.Lerp(startColor, endColor, t);
            fadeImage.color = c;
            // Отключаем перехват кликов когда фейд становится прозрачным
            fadeImage.raycastTarget = c.a > 0.001f;
            yield return null;
        }

        fadeImage.color = endColor;
        fadeImage.raycastTarget = endColor.a > 0.001f;
    }

    /// <summary>
    /// Только fade out (для использования перед загрузкой)
    /// </summary>
    public IEnumerator FadeOutOnly()
    {
        yield return StartCoroutine(FadeOut());
    }

    /// <summary>
    /// Только fade in (для использования после загрузки)
    /// </summary>
    public IEnumerator FadeInOnly()
    {
        yield return StartCoroutine(FadeIn());
    }
}
