using UnityEngine;
using UnityEngine.UI;

// Красный экран при получении урона (как в Doom)
public class DamageScreenEffect : MonoBehaviour
{
    [Header("Damage Flash")]
    public Image damageImage; // Красный overlay
    public Color damageColor = new Color(1f, 0f, 0f, 0.5f); // Красный полупрозрачный
    public float flashDuration = 0.3f; // Длительность вспышки
    
    private float flashTimer = 0f;
    private DoomHealth playerHealth;
    private int lastHealth = -1; // Отслеживаем предыдущее здоровье

    void Start()
    {
        // Создаем красный overlay если его нет
        if (damageImage == null)
        {
            CreateDamageOverlay();
        }
        // На всякий случай: вспышка урона никогда не должна ловить клики
        // (фуллскрин-оверлей иначе блокирует кнопки UI под ним).
        if (damageImage != null) damageImage.raycastTarget = false;

        // Находим здоровье игрока
        playerHealth = GetComponent<DoomHealth>();
        if (playerHealth != null)
        {
            // Запоминаем начальное здоровье
            lastHealth = playerHealth.currentHealth;
            
            // Подписываемся на событие получения урона
            playerHealth.OnHealthChanged.AddListener(OnPlayerDamaged);
        }
    }

    void CreateDamageOverlay()
    {
        // Находим Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DamageCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Создаем Image для красного экрана
        GameObject imageObj = new GameObject("DamageOverlay");
        imageObj.transform.SetParent(canvas.transform, false);
        
        damageImage = imageObj.AddComponent<Image>();
        damageImage.color = new Color(damageColor.r, damageColor.g, damageColor.b, 0f); // Начинаем прозрачным
        // ВАЖНО: вспышка урона не должна перехватывать клики. Иначе этот фуллскрин-Image
        // (даже прозрачный) блокирует кнопки UI под ним (диалог в шопе и т.п.).
        damageImage.raycastTarget = false;

        // Растягиваем на весь экран
        RectTransform rect = damageImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        if (damageImage == null) return;

        // Плавное исчезновение красного экрана
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
        // Показываем красный экран ТОЛЬКО если здоровье уменьшилось
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
