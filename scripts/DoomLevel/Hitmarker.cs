using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Хитмаркер в стиле шутеров: при попадании по врагу в центре экрана вспыхивает «✕»
/// (с поп-анимацией) и играет короткий «тук». На добивании — усиленный маркер и свой звук.
///
/// Повесь на любой объект в сцене doom и назначь звуки. UI создаётся сам.
/// DoomWeapon дёргает Hitmarker.Hit() / Hitmarker.Kill() автоматически.
/// </summary>
public class Hitmarker : MonoBehaviour
{
    public static Hitmarker Instance;

    [Header("Звук")]
    public AudioClip hitSound;
    public AudioClip killSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("Вид")]
    public Color hitColor = Color.white;
    public Color killColor = new Color(1f, 0.3f, 0.3f);
    public float tickLength = 16f;   // длина штриха
    public float gap = 7f;           // отступ от центра
    public float thickness = 3f;     // толщина штриха

    [Header("Анимация")]
    public float duration = 0.18f;   // сколько висит/гаснет
    public float popScale = 1.5f;    // во сколько раз «прыгает» в начале

    CanvasGroup _group;
    RectTransform _root;
    AudioSource _audio;
    Image[] _ticks;
    float _t = -1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f; // 2D

        Build();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Build()
    {
        var canvasObj = new GameObject("HitmarkerCanvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // GraphicRaycaster не нужен — маркер кликов не ловит.

        var rootObj = new GameObject("Hitmarker");
        rootObj.transform.SetParent(canvas.transform, false);
        _root = rootObj.AddComponent<RectTransform>();
        _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = Vector2.zero;
        _root.sizeDelta = Vector2.zero;
        _group = rootObj.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        // 4 диагональных штриха — классический «✕» с разрывом по центру.
        _ticks = new Image[4];
        _ticks[0] = MakeTick(new Vector2(1f, 1f), 45f);    // NE  "/"
        _ticks[1] = MakeTick(new Vector2(-1f, 1f), -45f);  // NW  "\"
        _ticks[2] = MakeTick(new Vector2(-1f, -1f), 45f);  // SW  "/"
        _ticks[3] = MakeTick(new Vector2(1f, -1f), -45f);  // SE  "\"
    }

    Image MakeTick(Vector2 dir, float rotZ)
    {
        var o = new GameObject("tick");
        o.transform.SetParent(_root, false);
        var img = o.AddComponent<Image>();
        img.color = hitColor;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(thickness, tickLength);
        float dist = gap + tickLength * 0.5f;
        rt.anchoredPosition = dir.normalized * dist;
        rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        return img;
    }

    void Show(bool kill)
    {
        _t = 0f;
        var c = kill ? killColor : hitColor;
        foreach (var img in _ticks) if (img != null) img.color = c;
        var clip = (kill && killSound != null) ? killSound : hitSound;
        if (_audio != null && clip != null) _audio.PlayOneShot(clip, volume);
    }

    void Update()
    {
        if (_t < 0f) return;
        _t += Time.deltaTime;
        float k = _t / duration;
        if (k >= 1f) { _group.alpha = 0f; _root.localScale = Vector3.one; _t = -1f; return; }
        _group.alpha = 1f - k;
        float sc = Mathf.Lerp(popScale, 1f, k);
        _root.localScale = new Vector3(sc, sc, 1f);
    }

    // Статические хелперы — зовёт DoomWeapon. Если хитмаркера в сцене нет — тихо ничего.
    public static void Hit() { if (Instance != null) Instance.Show(false); }
    public static void Kill() { if (Instance != null) Instance.Show(true); }
}
