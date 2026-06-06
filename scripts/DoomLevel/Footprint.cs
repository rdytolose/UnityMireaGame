using UnityEngine;

public class Footprint : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeDelay = 5f;
    public float fadeDuration = 3f;

    private Material material;
    private float spawnTime;
    private Color originalColor;

    void Start()
    {
        spawnTime = Time.time;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
            originalColor = material.color;
        }
    }

    void Update()
    {
        if (material == null) return;

        float timeSinceSpawn = Time.time - spawnTime;

        if (timeSinceSpawn > fadeDelay)
        {
            float fadeProgress = (timeSinceSpawn - fadeDelay) / fadeDuration;
            fadeProgress = Mathf.Clamp01(fadeProgress);

            Color color = originalColor;
            color.a = Mathf.Lerp(originalColor.a, 0f, fadeProgress);
            material.color = color;
        }
    }
}
