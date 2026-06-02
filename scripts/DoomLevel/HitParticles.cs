using UnityEngine;

// Партиклы крови при попадании по врагу
public class HitParticles : MonoBehaviour
{
    [Header("Blood Particles")]
    public GameObject bloodParticlePrefab; // Префаб партиклов крови
    public int particleCount = 10;
    public float particleSpeed = 5f;
    public float particleLifetime = 1f;
    public Color bloodColor = new Color(0.8f, 0f, 0f, 1f); // Темно-красный

    [Header("Pixel Style")]
    public bool usePixelParticles = true;
    public float pixelSize = 0.1f;

    private ParticleSystem bloodSystem;

    void Start()
    {
        CreateBloodParticleSystem();
    }

    void CreateBloodParticleSystem()
    {
        // Создаем Particle System
        GameObject particleObj = new GameObject("BloodParticles");
        particleObj.transform.parent = transform;
        particleObj.transform.localPosition = Vector3.zero;

        bloodSystem = particleObj.AddComponent<ParticleSystem>();
        
        var main = bloodSystem.main;
        main.startLifetime = particleLifetime;
        main.startSpeed = particleSpeed;
        main.startSize = usePixelParticles ? pixelSize : 0.2f;
        main.startColor = bloodColor;
        main.maxParticles = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        // Emission
        var emission = bloodSystem.emission;
        emission.enabled = false; // Будем вызывать вручную

        // Shape (брызги во все стороны)
        var shape = bloodSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        // Gravity
        var gravity = bloodSystem.forceOverLifetime;
        gravity.enabled = true;
        gravity.y = -9.81f;

        // Renderer
        var renderer = bloodSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        
        // Создаем простую красную текстуру
        Texture2D tex = new Texture2D(4, 4);
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                tex.SetPixel(i, j, bloodColor);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        renderer.material.mainTexture = tex;
    }

    public void PlayHitEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (bloodSystem != null)
        {
            bloodSystem.transform.position = hitPoint;
            bloodSystem.Emit(particleCount);
        }
    }

    // Статический метод для быстрого создания эффекта
    public static void CreateBloodSplash(Vector3 position, Vector3 normal)
    {
        GameObject splashObj = new GameObject("BloodSplash");
        splashObj.transform.position = position;
        
        HitParticles particles = splashObj.AddComponent<HitParticles>();
        particles.PlayHitEffect(position, normal);
        
        Destroy(splashObj, 2f);
    }
}
