using UnityEngine;

public class DoomDarkness : MonoBehaviour
{
    [Header("Darkness Settings")]
    [Range(0f, 1f)]
    public float darknessAmount = 0.5f;

    public Color darknessColor = new Color(0, 0, 0, 0.5f);

    [Header("Vignette (затемнение по краям)")]
    public bool useVignette = true;
    [Range(0f, 1f)]
    public float vignetteIntensity = 0.4f;

    private Material darknessMaterial;

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (darknessMaterial == null)
        {
            darknessMaterial = new Material(Shader.Find("Hidden/DoomDarkness"));
        }

        darknessMaterial.SetFloat("_Darkness", darknessAmount);
        darknessMaterial.SetColor("_DarknessColor", darknessColor);
        darknessMaterial.SetFloat("_VignetteIntensity", useVignette ? vignetteIntensity : 0f);

        Graphics.Blit(source, destination, darknessMaterial);
    }
}
