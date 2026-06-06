using UnityEngine;

public class TVInteractable : MonoBehaviour
{
    public SecurityCamerasController camerasController;

    void Awake()
    {
        if (camerasController == null)
            camerasController = Object.FindFirstObjectByType<SecurityCamerasController>();

        if (camerasController == null)
            Debug.LogError("[TVInteractable] В сцене не найден SecurityCamerasController");
    }

    public void Activate()
    {
        if (camerasController != null) camerasController.TogglePanel();
    }
}
