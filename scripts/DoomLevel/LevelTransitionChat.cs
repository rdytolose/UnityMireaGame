using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelTransitionChat : MonoBehaviour
{
    [Header("Сцена после диалога")]
    public string nextSceneName = "Shop";

    [Header("Анимация на экране (опц.)")]
    [Tooltip("Видеоролик-заставка (webm/VP8 для Linux-редактора). Пусто — чёрный экран.")]
    public VideoClip videoClip;
    public bool loopVideo = true;
    [Tooltip("Размер окна видео в пикселях (референс 1920x1080). НЕ на весь экран.")]
    public Vector2 videoSize = new Vector2(960, 540);
    [Tooltip("Смещение окна видео от центра экрана.")]
    public Vector2 videoOffset = new Vector2(0, 40);
    [Tooltip("Проигрывать звук ролика.")]
    public bool playVideoSound = true;
    [Range(0f, 1f)] public float videoVolume = 0.8f;

    [Header("Текст-подсказка (только в первый раз, во время диалога)")]
    public string hintMessage = "СМОТРИ В ТЕЛЕФОН";

    [Header("Фейды/тайминги")]
    public float fadeDuration = 0.6f;
    public float pollInterval = 1.5f;
    [Tooltip("Макс. ожидание диалога (сек) в ПЕРВЫЙ раз, потом грузим в любом случае.")]
    public float maxWait = 180f;
    [Tooltip("Сколько секунд показывать видео БЕЗ диалога (все разы после первого).")]
    public float videoOnlyDuration = 10f;

    bool _started = false;
    bool _gateDone = false;
    Canvas _canvas;
    Image _fader;

    public void Begin()
    {
        if (_started) return;
        _started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        GameApi.Ensure();

        bool firstTime = !GameStats.Ensure().transitionChatShown;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildCanvas();
        yield return Fade(0f, 1f);
        BuildContent(firstTime);
        yield return Fade(1f, 0f);

        if (firstTime)
        {
            GameStats.I.transitionChatShown = true;
            GameApi.Instance.Post("/api/gate/reset", "{}", (ok, body) => { });
            GameSession.SetScreen("talk");

            yield return new WaitForSecondsRealtime(0.8f);
            float waited = 0f;
            while (!_gateDone && waited < maxWait)
            {
                GameApi.Instance.Get("/api/gate/status", (ok, body) =>
                {
                    if (ok && JsonUtility.FromJson<IntroStatusResp>(body).done) _gateDone = true;
                });
                yield return new WaitForSecondsRealtime(pollInterval);
                waited += pollInterval;
            }
        }
        else
        {
            GameSession.SetScreen("wait");
            yield return new WaitForSecondsRealtime(videoOnlyDuration);
        }

        yield return Fade(0f, 1f);
        Time.timeScale = 1f;
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadSceneWithFade(nextSceneName);
        else
            SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator Fade(float from, float to)
    {
        if (_fader == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _fader.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        _fader.color = new Color(0f, 0f, 0f, to);
    }

    void BuildCanvas()
    {
        var canvasObj = new GameObject("TransitionCanvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9000;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var faderObj = new GameObject("Fader");
        faderObj.transform.SetParent(_canvas.transform, false);
        _fader = faderObj.AddComponent<Image>();
        _fader.color = new Color(0f, 0f, 0f, 0f);
        _fader.raycastTarget = false;
        Stretch(_fader.rectTransform);
    }

    void BuildContent(bool showHint)
    {
        var bgObj = new GameObject("Black");
        bgObj.transform.SetParent(_canvas.transform, false);
        var bg = bgObj.AddComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = false;
        Stretch(bg.rectTransform);

        if (videoClip != null)
        {
            var rt = new RenderTexture(1280, 720, 0);
            var vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.clip = videoClip;
            vp.isLooping = loopVideo;

            if (playVideoSound)
            {
                var aud = gameObject.AddComponent<AudioSource>();
                aud.playOnAwake = false;
                aud.spatialBlend = 0f;
                aud.volume = videoVolume;
                vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
                vp.EnableAudioTrack(0, true);
                vp.SetTargetAudioSource(0, aud);
            }
            else
            {
                vp.audioOutputMode = VideoAudioOutputMode.None;
            }
            vp.Play();

            var imgObj = new GameObject("Video");
            imgObj.transform.SetParent(_canvas.transform, false);
            var raw = imgObj.AddComponent<RawImage>();
            raw.texture = rt;
            raw.raycastTarget = false;
            var vr = raw.rectTransform;
            vr.anchorMin = new Vector2(0.5f, 0.5f);
            vr.anchorMax = new Vector2(0.5f, 0.5f);
            vr.pivot = new Vector2(0.5f, 0.5f);
            vr.anchoredPosition = videoOffset;
            vr.sizeDelta = videoSize;
        }

        if (showHint)
        {
            var textObj = new GameObject("Hint");
            textObj.transform.SetParent(_canvas.transform, false);
            var label = textObj.AddComponent<TextMeshProUGUI>();
            label.text = hintMessage;
            label.fontSize = 60;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            var font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
            if (font == null)
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (all.Length > 0) font = all[0];
            }
            if (font != null) label.font = font;
            var r = label.rectTransform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(0, 120);
            r.sizeDelta = new Vector2(1400, 140);
        }

        _fader.transform.SetAsLastSibling();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
