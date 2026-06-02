using UnityEngine;

public class RadioInteractable : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource radioSource;

    [Header("Particles (optional)")]
    public ParticleSystem particles;
    public bool particlesFollowRadio = true;

    [Header("Optional indicator object (light, emissive mesh, etc.)")]
    public GameObject indicatorObject;

    [Header("State")]
    public bool isOn = false;

    void Awake()
    {
        if (radioSource == null) radioSource = GetComponentInParent<AudioSource>();
        if (particles == null) particles = GetComponentInChildren<ParticleSystem>(true);

        ApplyState(false); // применяем начальное состояние без "переигрывания"
    }

    public void Activate()
    {
        isOn = !isOn;
        ApplyState(true);
    }

    void ApplyState(bool allowRestart)
    {
        // Audio
        if (radioSource != null)
        {
            if (isOn)
            {
                if (allowRestart)
                {
                    if (!radioSource.isPlaying) radioSource.Play();
                }
                else
                {
                    // если в инспекторе Play On Awake выключен, то просто оставим как есть
                }
            }
            else
            {
                if (radioSource.isPlaying) radioSource.Stop();
            }
        }

        // Particles
        if (particles != null)
        {
            if (particlesFollowRadio) particles.transform.position = transform.position;

            if (isOn)
            {
                if (!particles.isPlaying) particles.Play(true);
            }
            else
            {
                // Stop(true, StopEmittingAndClear) — сразу очищает частицы
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        // Indicator
        if (indicatorObject != null)
            indicatorObject.SetActive(isOn);
    }
}