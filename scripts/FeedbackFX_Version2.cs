using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FeedbackFX : MonoBehaviour
{
    [Header("Screen flash")]
    public Image overlay;
    public Color correctColor = new Color(0.2f, 1f, 0.2f, 1f);
    public Color wrongColor   = new Color(1f, 0.2f, 0.2f, 1f);
    [Range(0f, 1f)] public float flashAlpha = 0.38f;
    public float flashFadeIn = 0.05f;
    public float flashHold = 0.08f;
    public float flashFadeOut = 0.22f;

    [Header("Camera shake")]
    public Transform cameraTransform;
    public float shakeDuration = 0.12f;
    public float shakeAmplitude = 0.06f;
    public float shakeFrequency = 22f;

    [Header("Sounds")]
    public AudioSource sfxSource;
    public AudioClip correctSfx;
    public AudioClip wrongSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    Coroutine flashRoutine;
    Coroutine shakeRoutine;

    Vector3 camLocalStartPos;
    bool camPosSaved = false;

    void Awake()
    {
        if (overlay != null)
        {
            var c = overlay.color;
            c.a = 0f;
            overlay.color = c;
            overlay.raycastTarget = false;
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    public void Correct()
    {
        DoFlash(correctColor);
        DoShake();
        PlaySfx(correctSfx);
    }

    public void Wrong()
    {
        DoFlash(wrongColor);
        DoShake();
        PlaySfx(wrongSfx);
    }

    void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    void DoFlash(Color color)
    {
        if (overlay == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(color));
    }

    IEnumerator FlashRoutine(Color color)
    {
        float t = 0f;
        while (t < flashFadeIn)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / flashFadeIn);
            SetOverlay(color, Mathf.Lerp(0f, flashAlpha, k));
            yield return null;
        }
        SetOverlay(color, flashAlpha);

        if (flashHold > 0f) yield return new WaitForSecondsRealtime(flashHold);

        t = 0f;
        while (t < flashFadeOut)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / flashFadeOut);
            SetOverlay(color, Mathf.Lerp(flashAlpha, 0f, k));
            yield return null;
        }
        SetOverlay(color, 0f);

        flashRoutine = null;
    }

    void SetOverlay(Color baseColor, float a)
    {
        baseColor.a = a;
        overlay.color = baseColor;
    }

    void DoShake()
    {
        if (cameraTransform == null) return;

        if (!camPosSaved)
        {
            camLocalStartPos = cameraTransform.localPosition;
            camPosSaved = true;
        }

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.unscaledDeltaTime;

            float damper = 1f - Mathf.Clamp01(t / shakeDuration);
            float n = t * shakeFrequency;

            float x = (Mathf.PerlinNoise(n, 0.1f) * 2f - 1f) * shakeAmplitude * damper;
            float y = (Mathf.PerlinNoise(0.2f, n) * 2f - 1f) * shakeAmplitude * damper;

            cameraTransform.localPosition = camLocalStartPos + new Vector3(x, y, 0f);
            yield return null;
        }

        cameraTransform.localPosition = camLocalStartPos;
        shakeRoutine = null;
    }
}
