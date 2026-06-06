using UnityEngine;

public class AmbientSound : MonoBehaviour
{
    [Header("Ambient Settings")]
    public AudioClip ambientClip;
    public float volume = 0.3f;
    public bool loop = true;
    public bool playOnStart = true;

    [Header("Fade Settings")]
    public bool fadeIn = true;
    public float fadeInDuration = 2f;

    [Header("Additional Layers")]
    public AudioClip windSound;
    public float windVolume = 0.2f;
    public AudioClip distantSounds;
    public float distantVolume = 0.15f;

    private AudioSource mainAmbient;
    private AudioSource windAmbient;
    private AudioSource distantAmbient;
    private float targetVolume;
    private float currentFadeTime = 0f;

    void Start()
    {
        mainAmbient = gameObject.AddComponent<AudioSource>();
        mainAmbient.clip = ambientClip;
        mainAmbient.loop = loop;
        mainAmbient.spatialBlend = 0f;
        mainAmbient.priority = 0;
        targetVolume = volume;

        if (fadeIn)
        {
            mainAmbient.volume = 0f;
        }
        else
        {
            mainAmbient.volume = volume;
        }

        if (playOnStart && ambientClip != null)
        {
            mainAmbient.Play();
        }

        if (windSound != null)
        {
            windAmbient = gameObject.AddComponent<AudioSource>();
            windAmbient.clip = windSound;
            windAmbient.loop = true;
            windAmbient.volume = fadeIn ? 0f : windVolume;
            windAmbient.spatialBlend = 0f;
            windAmbient.Play();
        }

        if (distantSounds != null)
        {
            distantAmbient = gameObject.AddComponent<AudioSource>();
            distantAmbient.clip = distantSounds;
            distantAmbient.loop = true;
            distantAmbient.volume = fadeIn ? 0f : distantVolume;
            distantAmbient.spatialBlend = 0f;
            distantAmbient.Play();
        }
    }

    void Update()
    {
        if (fadeIn && currentFadeTime < fadeInDuration)
        {
            currentFadeTime += Time.deltaTime;
            float t = currentFadeTime / fadeInDuration;

            if (mainAmbient != null)
                mainAmbient.volume = Mathf.Lerp(0f, targetVolume, t);

            if (windAmbient != null)
                windAmbient.volume = Mathf.Lerp(0f, windVolume, t);

            if (distantAmbient != null)
                distantAmbient.volume = Mathf.Lerp(0f, distantVolume, t);
        }
    }

    public void SetVolume(float newVolume)
    {
        volume = newVolume;
        if (mainAmbient != null)
            mainAmbient.volume = newVolume;
    }

    public void FadeOut(float duration)
    {
        StartCoroutine(FadeOutCoroutine(duration));
    }

    System.Collections.IEnumerator FadeOutCoroutine(float duration)
    {
        float startVolume = mainAmbient.volume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            if (mainAmbient != null)
                mainAmbient.volume = Mathf.Lerp(startVolume, 0f, t);

            if (windAmbient != null)
                windAmbient.volume = Mathf.Lerp(windVolume, 0f, t);

            if (distantAmbient != null)
                distantAmbient.volume = Mathf.Lerp(distantVolume, 0f, t);

            yield return null;
        }

        if (mainAmbient != null) mainAmbient.Stop();
        if (windAmbient != null) windAmbient.Stop();
        if (distantAmbient != null) distantAmbient.Stop();
    }
}
