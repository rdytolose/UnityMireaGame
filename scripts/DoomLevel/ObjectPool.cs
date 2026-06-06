using UnityEngine;
using System.Collections.Generic;

public class ObjectPool : MonoBehaviour
{
    [Header("Pool Settings")]
    public GameObject prefab;
    public int initialPoolSize = 20;
    public bool expandIfNeeded = true;
    public int maxPoolSize = 100;
    public Transform poolParent;

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

    public void Initialize()
    {
        if (isInitialized) return;

        if (poolParent == null)
        {
            GameObject parent = new GameObject($"{prefab.name}_Pool");
            poolParent = parent.transform;
        }

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

    public int GetAvailableCount()
    {
        return availableObjects.Count;
    }

    public int GetTotalCount()
    {
        return allObjects.Count;
    }

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
