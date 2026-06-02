using UnityEngine;

/// <summary>
/// Singleton для быстрого доступа к игроку из любого скрипта.
/// Заменяет медленные FindGameObjectWithTag и FindObjectOfType.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }
    
    public Transform PlayerTransform { get; private set; }
    public DoomHealth PlayerHealth { get; private set; }
    public DoomPlayerController PlayerController { get; private set; }

    void Awake()
    {
        // Singleton pattern - только один экземпляр
        if (Instance == null)
        {
            Instance = this;
            PlayerTransform = transform;
            PlayerHealth = GetComponent<DoomHealth>();
            PlayerController = GetComponent<DoomPlayerController>();
            
        }
        else
        {
            Debug.LogWarning("Duplicate PlayerManager found, destroying");
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
