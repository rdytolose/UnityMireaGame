using UnityEngine;
using UnityEngine.UI;

public class DamageScreenEffect : MonoBehaviour
{
    [Header("Damage Flash")]
    public Image damageImage;
    public Color damageColor = new Color(1f, 0f, 0f, 0.5f);
    public float flashDuration = 0.3f;

    private float flashTimer = 0f;
    private DoomHealth playerHealth;
    private int lastHealth = -1;

    void Start()
    {
        if (damageImage == null)
        {
            CreateDamageOverlay();
        }
        if (damageImage != null) damageImage.raycastTarget = false;

        playerHealth = GetComponent<DoomHealth>();
        if (playerHealth != null)
        {
            lastHealth = playerHealth.currentHealth;

            playerHealth.OnHealthChanged.AddListener(OnPlayerDamaged);
        }
    }

    void CreateDamageOverlay()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DamageCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        GameObject imageObj = new GameObject("DamageOverlay");
        imageObj.transform.SetParent(canvas.transform, false);

        damageImage = imageObj.AddComponent<Image>();
        damageImage.color = new Color(damageColor.r, damageColor.g, damageColor.b, 0f);
        damageImage.raycastTarget = false;

        RectTransform rect = damageImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        if (damageImage == null) return;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float alpha = Mathf.Lerp(0f, damageColor.a, flashTimer / flashDuration);
            damageImage.color = new Color(damageColor.r, damageColor.g, damageColor.b, alpha);
        }
        else
        {
            damageImage.color = new Color(damageColor.r, damageColor.g, damageColor.b, 0f);
        }
    }

    void OnPlayerDamaged(int currentHealth)
    {
        if (lastHealth > 0 && currentHealth < lastHealth)
        {
            ShowDamageFlash();
        }

        lastHealth = currentHealth;
    }

    public void ShowDamageFlash()
    {
        flashTimer = flashDuration;
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged.RemoveListener(OnPlayerDamaged);
        }
    }
}
