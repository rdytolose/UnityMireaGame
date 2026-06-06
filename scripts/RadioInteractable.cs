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

        ApplyState(false);
    }

    public void Activate()
    {
        isOn = !isOn;
        ApplyState(true);
    }

    void ApplyState(bool allowRestart)
    {
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
                }
            }
            else
            {
                if (radioSource.isPlaying) radioSource.Stop();
            }
        }

        if (particles != null)
        {
            if (particlesFollowRadio) particles.transform.position = transform.position;

            if (isOn)
            {
                if (!particles.isPlaying) particles.Play(true);
            }
            else
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (indicatorObject != null)
            indicatorObject.SetActive(isOn);
    }
}
