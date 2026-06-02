using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Эффект старого ЭЛТ-монитора: мигание яркости (плавный шум + редкие резкие
/// моргания) и лёгкое подрагивание картинки. Повесь на объект экрана с QR.
///
/// Может управлять любым набором целей одновременно (что назначишь / найдётся само):
///   • UI Image/RawImage (QR на Canvas)  — гасит яркость цвета;
///   • CanvasGroup                        — гасит alpha всей панели;
///   • Renderer (квад/меш экрана)         — гасит цвет и эмиссию материала;
///   • Light                              — мигает свет от монитора;
///   • Transform jitterTarget             — дрожание изображения.
/// Если поля пустые — берёт Graphic/Renderer с этого же объекта.
/// </summary>
[DisallowMultipleComponent]
public class CRTScreenFlicker : MonoBehaviour
{
    [Header("Цели (пусто — найдётся на этом объекте)")]
    public Graphic targetGraphic;        // Image / RawImage (например, QR)
    public CanvasGroup canvasGroup;      // вся панель экрана
    public Renderer screenRenderer;      // меш-экран (материал)
    public Light screenLight;            // свет от монитора
    public Transform jitterTarget;       // что дрожит (обычно сам экран)

    [Header("Мигание яркости")]
    [Tooltip("Базовая «постоянная» дрожь яркости (0..1). Чем больше — тем заметнее мерцание.")]
    [Range(0f, 1f)] public float flickerAmount = 0.18f;
    [Tooltip("Скорость плавного мерцания.")]
    public float flickerSpeed = 12f;

    [Header("Резкие моргания (как скачок напряжения)")]
    [Tooltip("Мин/макс пауза между морганиями, сек.")]
    public float blinkMin = 1.5f;
    public float blinkMax = 5f;
    [Tooltip("Насколько темнеет в момент моргания (0 — почти чёрный экран).")]
    [Range(0f, 1f)] public float blinkDarkness = 0.25f;
    [Tooltip("Длительность одного моргания, сек.")]
    public float blinkDuration = 0.08f;

    [Header("Дрожание картинки")]
    public bool jitter = true;
    [Tooltip("Амплитуда дрожания в локальных единицах.")]
    public float jitterAmount = 0.004f;
    public float jitterSpeed = 25f;

    // — внутреннее —
    Color _graphicColor;
    Color _rendererColor;
    Color _emissionColor;
    bool _hasEmission;
    float _lightIntensity;
    float _canvasAlpha;
    Vector3 _jitterBasePos;
    float _noiseSeed;

    float _blinkTimer;        // сколько ещё длится текущее моргание
    float _nextBlinkAt;
    Material _mat;

    void Start()
    {
        if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
        if (screenRenderer == null) screenRenderer = GetComponent<Renderer>();
        if (jitterTarget == null) jitterTarget = transform;

        if (targetGraphic != null) _graphicColor = targetGraphic.color;
        if (canvasGroup != null) _canvasAlpha = canvasGroup.alpha;
        if (screenLight != null) _lightIntensity = screenLight.intensity;
        if (screenRenderer != null)
        {
            _mat = screenRenderer.material; // инстанс — чтобы не портить общий материал
            if (_mat.HasProperty("_Color")) _rendererColor = _mat.color;
            _hasEmission = _mat.HasProperty("_EmissionColor");
            if (_hasEmission)
            {
                _emissionColor = _mat.GetColor("_EmissionColor");
                _mat.EnableKeyword("_EMISSION");
            }
        }

        _jitterBasePos = jitterTarget.localPosition;
        _noiseSeed = Random.value * 100f;
        ScheduleBlink();
    }

    void ScheduleBlink()
    {
        _nextBlinkAt = Time.time + Random.Range(blinkMin, blinkMax);
    }

    void Update()
    {
        // 1) Плавная дрожь яркости через шум Перлина (0.. -> 1-flicker..1).
        float n = Mathf.PerlinNoise(_noiseSeed, Time.time * flickerSpeed);
        float brightness = Mathf.Lerp(1f - flickerAmount, 1f, n);

        // 2) Резкое моргание по таймеру.
        if (Time.time >= _nextBlinkAt)
        {
            _blinkTimer = blinkDuration;
            ScheduleBlink();
        }
        if (_blinkTimer > 0f)
        {
            _blinkTimer -= Time.deltaTime;
            brightness *= blinkDarkness;
        }

        ApplyBrightness(Mathf.Clamp01(brightness));

        // 3) Дрожание картинки.
        if (jitter && jitterTarget != null)
        {
            float jx = (Mathf.PerlinNoise(Time.time * jitterSpeed, _noiseSeed) - 0.5f) * 2f;
            float jy = (Mathf.PerlinNoise(_noiseSeed, Time.time * jitterSpeed) - 0.5f) * 2f;
            jitterTarget.localPosition = _jitterBasePos + new Vector3(jx, jy, 0f) * jitterAmount;
        }
    }

    void ApplyBrightness(float b)
    {
        if (targetGraphic != null)
        {
            var c = _graphicColor;
            targetGraphic.color = new Color(c.r * b, c.g * b, c.b * b, c.a);
        }
        if (canvasGroup != null)
            canvasGroup.alpha = _canvasAlpha * b;
        if (screenLight != null)
            screenLight.intensity = _lightIntensity * b;
        if (_mat != null)
        {
            if (_mat.HasProperty("_Color"))
            {
                var c = _rendererColor;
                _mat.color = new Color(c.r * b, c.g * b, c.b * b, c.a);
            }
            if (_hasEmission)
            {
                var e = _emissionColor;
                _mat.SetColor("_EmissionColor", new Color(e.r * b, e.g * b, e.b * b, e.a));
            }
        }
    }

    void OnDisable()
    {
        // Вернуть исходные значения, чтобы экран не остался затемнённым.
        if (targetGraphic != null) targetGraphic.color = _graphicColor;
        if (canvasGroup != null) canvasGroup.alpha = _canvasAlpha;
        if (screenLight != null) screenLight.intensity = _lightIntensity;
        if (_mat != null)
        {
            if (_mat.HasProperty("_Color")) _mat.color = _rendererColor;
            if (_hasEmission) _mat.SetColor("_EmissionColor", _emissionColor);
        }
        if (jitterTarget != null) jitterTarget.localPosition = _jitterBasePos;
    }
}
