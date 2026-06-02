using UnityEngine;

// Звуки шагов по снегу
public class FootstepSounds : MonoBehaviour
{
    [Header("Footstep Settings")]
    public AudioClip[] snowFootsteps; // Массив звуков шагов
    public float stepDistance = 1f;
    public float volumeMin = 0.3f;
    public float volumeMax = 0.5f;
    public float pitchMin = 0.9f;
    public float pitchMax = 1.1f;

    private AudioSource audioSource;
    private Vector3 lastStepPosition;
    private CharacterController characterController;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // 2D звук
        
        characterController = GetComponent<CharacterController>();
        lastStepPosition = transform.position;
    }

    void Update()
    {
        if (characterController == null || !characterController.isGrounded)
            return;

        if (characterController.velocity.magnitude < 0.1f)
            return;

        float distance = Vector3.Distance(transform.position, lastStepPosition);
        
        if (distance >= stepDistance)
        {
            PlayFootstep();
            lastStepPosition = transform.position;
        }
    }

    void PlayFootstep()
    {
        if (snowFootsteps == null || snowFootsteps.Length == 0)
            return;

        AudioClip clip = snowFootsteps[Random.Range(0, snowFootsteps.Length)];
        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.volume = Random.Range(volumeMin, volumeMax);
        audioSource.PlayOneShot(clip);
    }
}
