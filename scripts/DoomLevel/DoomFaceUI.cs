using UnityEngine;
using UnityEngine.UI;

public class DoomFaceUI : MonoBehaviour
{
    [Header("Face Sprites (10 состояний)")]
    public Sprite face100;
    public Sprite face90;
    public Sprite face80;
    public Sprite face70;
    public Sprite face60;
    public Sprite face50;
    public Sprite face40;
    public Sprite face30;
    public Sprite face20;
    public Sprite face10;

    [Header("UI Settings")]
    public Image faceImage;
    public Vector2 facePosition = new Vector2(0, -400);
    public Vector2 faceSize = new Vector2(100, 100);

    [Header("References")]
    public DoomHealth playerHealth;

    void Start()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<DoomHealth>();
        }

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
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DoomUICanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        GameObject faceObj = new GameObject("DoomFace");
        faceObj.transform.SetParent(canvas.transform, false);

        faceImage = faceObj.AddComponent<Image>();
        faceImage.sprite = face100;
        faceImage.preserveAspect = true;

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

        float healthPercent = (float)currentHealth / playerHealth.maxHealth * 100f;

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
