using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Универсальный пул объектов для переиспользования вместо Instantiate/Destroy.
/// Убирает GC спайки и повышает производительность.
/// </summary>
public class ObjectPool : MonoBehaviour
{
    [Header("Pool Settings")]
    public GameObject prefab;
    public int initialPoolSize = 20;
    public bool expandIfNeeded = true;
    public int maxPoolSize = 100; // Максимальный размер пула (0 = без ограничений)
    public Transform poolParent; // Родитель для организации в иерархии

    private Queue<GameObject> availableObjects = new Queue<GameObject>();
    private List<GameObject> allObjects = new List<GameObject>();
    private bool isInitialized = false;

    void Start()
    {
        if (!isInitialized)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Инициализация пула (можно вызвать вручную до Start)
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;
        
        // Создаём родителя для организации если не назначен
        if (poolParent == null)
        {
            GameObject parent = new GameObject($"{prefab.name}_Pool");
            poolParent = parent.transform;
        }

        // Предварительно создаём объекты
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewObject();
        }

        isInitialized = true;
    }

    GameObject CreateNewObject()
    {
        GameObject obj = Instantiate(prefab, poolParent);
        obj.SetActive(false);
        availableObjects.Enqueue(obj);
        allObjects.Add(obj);
        return obj;
    }

    /// <summary>
    /// Получить объект из пула
    /// </summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject obj;

        if (availableObjects.Count == 0)
        {
            if (expandIfNeeded && (maxPoolSize == 0 || allObjects.Count < maxPoolSize))
            {
                Debug.LogWarning($"ObjectPool {prefab.name} expanding - consider increasing initial size");
                obj = CreateNewObject();
            }
            else
            {
                Debug.LogError($"ObjectPool {prefab.name} exhausted! Max size: {maxPoolSize}");
                return null;
            }
        }
        else
        {
            obj = availableObjects.Dequeue();
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        return obj;
    }

    /// <summary>
    /// Вернуть объект в пул
    /// </summary>
    public void Return(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        obj.transform.SetParent(poolParent);
        
        if (!availableObjects.Contains(obj))
        {
            availableObjects.Enqueue(obj);
        }
    }

    /// <summary>
    /// Вернуть объект в пул с задержкой
    /// </summary>
    public void ReturnAfterDelay(GameObject obj, float delay)
    {
        if (obj != null)
        {
            StartCoroutine(ReturnAfterDelayCoroutine(obj, delay));
        }
    }

    System.Collections.IEnumerator ReturnAfterDelayCoroutine(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        Return(obj);
    }

    /// <summary>
    /// Получить количество доступных объектов
    /// </summary>
    public int GetAvailableCount()
    {
        return availableObjects.Count;
    }

    /// <summary>
    /// Получить общее количество объектов в пуле
    /// </summary>
    public int GetTotalCount()
    {
        return allObjects.Count;
    }

    /// <summary>
    /// Очистить весь пул
    /// </summary>
    public void Clear()
    {
        foreach (GameObject obj in allObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        
        availableObjects.Clear();
        allObjects.Clear();
    }

    void OnDestroy()
    {
        Clear();
    }
}
