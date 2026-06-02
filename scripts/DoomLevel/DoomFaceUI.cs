using UnityEngine;
using UnityEngine.UI;

// Лицо персонажа в стиле Doom (меняется в зависимости от здоровья)
public class DoomFaceUI : MonoBehaviour
{
    [Header("Face Sprites (10 состояний)")]
    public Sprite face100; // 100% здоровья
    public Sprite face90;  // 90%
    public Sprite face80;  // 80%
    public Sprite face70;  // 70%
    public Sprite face60;  // 60%
    public Sprite face50;  // 50%
    public Sprite face40;  // 40%
    public Sprite face30;  // 30%
    public Sprite face20;  // 20%
    public Sprite face10;  // 10% (почти мертв)

    [Header("UI Settings")]
    public Image faceImage; // UI Image для лица
    public Vector2 facePosition = new Vector2(0, -400); // Позиция внизу экрана
    public Vector2 faceSize = new Vector2(100, 100);

    [Header("References")]
    public DoomHealth playerHealth;

    void Start()
    {
        // Находим здоровье игрока
        if (playerHealth == null)
        {
            playerHealth = GetComponent<DoomHealth>();
        }

        // Создаем UI если его нет
        if (faceImage == null)
        {
            CreateFaceUI();
        }

        if (playerHealth != null && faceImage != null)
        {
            playerHealth.OnHealthChanged.AddListener(UpdateFace);
            UpdateFace(playerHealth.currentHealth);
        }
        else
        {
            Debug.LogWarning("DoomFaceUI: Missing playerHealth or faceImage!");
        }
    }

    void CreateFaceUI()
    {
        // Находим или создаем Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DoomUICanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Создаем Image для лица
        GameObject faceObj = new GameObject("DoomFace");
        faceObj.transform.SetParent(canvas.transform, false);
        
        faceImage = faceObj.AddComponent<Image>();
        faceImage.sprite = face100;
        faceImage.preserveAspect = true;

        // Настраиваем позицию (внизу по центру)
        RectTransform rect = faceImage.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = facePosition;
        rect.sizeDelta = faceSize;
    }

    void UpdateFace(int currentHealth)
    {
        if (faceImage == null || playerHealth == null) return;

        // Вычисляем процент здоровья
        float healthPercent = (float)currentHealth / playerHealth.maxHealth * 100f;

        // Выбираем спрайт в зависимости от здоровья
        Sprite newFace = null;

        if (healthPercent >= 95f) newFace = face100;
        else if (healthPercent >= 85f) newFace = face90;
        else if (healthPercent >= 75f) newFace = face80;
        else if (healthPercent >= 65f) newFace = face70;
        else if (healthPercent >= 55f) newFace = face60;
        else if (healthPercent >= 45f) newFace = face50;
        else if (healthPercent >= 35f) newFace = face40;
        else if (healthPercent >= 25f) newFace = face30;
        else if (healthPercent >= 15f) newFace = face20;
        else newFace = face10;

        if (newFace != null)
            faceImage.sprite = newFace;
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged.RemoveListener(UpdateFace);
        }
    }
}
