using UnityEngine;

public class SkipButton : MonoBehaviour
{
    public CustomerManager manager;
    void Awake() { if (manager == null) manager = Object.FindFirstObjectByType<CustomerManager>(); }
    public void Activate() { if (manager != null) manager.PlayerSkips(); }
}
