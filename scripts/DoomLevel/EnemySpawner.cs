using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public int maxEnemies = 10;
    public float minSpawnDelay = 5f;
    public float maxSpawnDelay = 15f;

    [Header("Object Pooling")]
    public bool useObjectPooling = true;
    public int poolSize = 20;

    [Header("Spawn Points")]
    public Transform[] spawnPoints;
    public bool useRandomPositions = false;
    public float spawnRadius = 20f;
    public float minDistanceFromPlayer = 10f;

    [Header("Spawn Area (если useRandomPositions)")]
    public Vector3 spawnAreaCenter = Vector3.zero;
    public Vector3 spawnAreaSize = new Vector3(30, 0, 30);

    [Header("References")]
    public Transform player;

    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private float nextSpawnTime;
    private float cleanupTimer = 0f;
    private const float CLEANUP_INTERVAL = 2f;
    private ObjectPool enemyPool;

    void Start()
    {
        if (player == null && PlayerManager.Instance != null)
        {
            player = PlayerManager.Instance.PlayerTransform;
        }

        if (useObjectPooling && enemyPrefab != null)
        {
            GameObject poolObj = new GameObject("EnemyPool");
            poolObj.transform.SetParent(transform);
            enemyPool = poolObj.AddComponent<ObjectPool>();
            enemyPool.prefab = enemyPrefab;
            enemyPool.initialPoolSize = poolSize;
            enemyPool.expandIfNeeded = true;
            enemyPool.maxPoolSize = poolSize * 3;

            enemyPool.Initialize();

        }

        ScheduleNextSpawn();
    }

    void Update()
    {
        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= CLEANUP_INTERVAL)
        {
            cleanupTimer = 0f;
            CleanupDeadEnemies();
        }

        if (Time.time >= nextSpawnTime && spawnedEnemies.Count < maxEnemies)
        {
            SpawnEnemy();
            ScheduleNextSpawn();
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("Enemy prefab not assigned!");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition();

        if (player != null)
        {
            float sqrDistance = (spawnPosition - player.position).sqrMagnitude;
            float minDistanceSqr = minDistanceFromPlayer * minDistanceFromPlayer;

            if (sqrDistance < minDistanceSqr)
            {
                ScheduleNextSpawn(1f);
                return;
            }
        }

        GameObject enemy;
        if (useObjectPooling && enemyPool != null)
        {
            enemy = enemyPool.Get(spawnPosition, Quaternion.identity);

            DoomHealth health = enemy.GetComponent<DoomHealth>();
            if (health != null)
            {
                health.ResetHealth();
                health.SetPool(enemyPool);
            }
        }
        else
        {
            enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        }

        spawnedEnemies.Add(enemy);
        ApplyDifficultyToEnemy(enemy);

    }

    void ApplyDifficultyToEnemy(GameObject enemy)
    {
        var cfg = DifficultyManager.Current;
        if (cfg == null) return;

        var h = enemy.GetComponent<DoomHealth>();
        if (h != null)
        {
            h.ApplyHealthMultiplier(cfg.shooter.enemy_health_mult);
            h.ResetHealth();
        }

        var de = enemy.GetComponent<DoomEnemy>();
        if (de != null)
            de.ApplyDifficulty(cfg.shooter.enemy_speed_mult, cfg.shooter.enemy_damage_mult);
    }

    Vector3 GetSpawnPosition()
    {
        if (useRandomPositions)
        {
            Vector3 randomPos = spawnAreaCenter + new Vector3(
                Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
                0,
                Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
            );

            if (Physics.Raycast(randomPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            {
                return hit.point + Vector3.up * 0.5f;
            }

            return randomPos;
        }
        else
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                return spawnPoint.position;
            }
            else
            {
                Debug.LogWarning("No spawn points assigned!");
                return transform.position;
            }
        }
    }

    void ScheduleNextSpawn(float customDelay = -1f)
    {
        float delay = customDelay > 0 ? customDelay : Random.Range(minSpawnDelay, maxSpawnDelay);
        nextSpawnTime = Time.time + delay;
    }

    void CleanupDeadEnemies()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null || !spawnedEnemies[i].activeInHierarchy)
                spawnedEnemies.RemoveAt(i);
        }
    }

    void OnDrawGizmos()
    {
        if (useRandomPositions)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
        }

        if (spawnPoints != null)
        {
            Gizmos.color = Color.red;
            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                    Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
                }
            }
        }

        if (player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);
        }
    }

    public void StopSpawning()
    {
        nextSpawnTime = float.MaxValue;
    }

    public void ResumeSpawning()
    {
        ScheduleNextSpawn();
    }

    public void ClearAllEnemies()
    {
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null)
                Destroy(enemy);
        }
        spawnedEnemies.Clear();
    }

    public int GetEnemyCount()
    {
        return spawnedEnemies.Count;
    }
}
