using UnityEngine;
using System.Collections.Generic;

// Спавнит врагов в случайных точках через случайные интервалы
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab; // Префаб врага
    public int maxEnemies = 10; // Максимум врагов одновременно
    public float minSpawnDelay = 5f; // Минимальная задержка между спавнами
    public float maxSpawnDelay = 15f; // Максимальная задержка

    [Header("Object Pooling")]
    public bool useObjectPooling = true; // Использовать пул объектов
    public int poolSize = 20; // Размер пула

    [Header("Spawn Points")]
    public Transform[] spawnPoints; // Точки спавна
    public bool useRandomPositions = false; // Или случайные позиции в радиусе
    public float spawnRadius = 20f; // Радиус случайного спавна
    public float minDistanceFromPlayer = 10f; // Минимальное расстояние от игрока

    [Header("Spawn Area (если useRandomPositions)")]
    public Vector3 spawnAreaCenter = Vector3.zero;
    public Vector3 spawnAreaSize = new Vector3(30, 0, 30);

    [Header("References")]
    public Transform player;

    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private float nextSpawnTime;
    private float cleanupTimer = 0f;
    private const float CLEANUP_INTERVAL = 2f; // Очистка раз в 2 секунды
    private ObjectPool enemyPool; // Пул врагов

    void Start()
    {
        // Используем PlayerManager вместо медленного FindGameObjectWithTag
        if (player == null && PlayerManager.Instance != null)
        {
            player = PlayerManager.Instance.PlayerTransform;
        }

        // Создаём пул объектов если включен
        if (useObjectPooling && enemyPrefab != null)
        {
            GameObject poolObj = new GameObject("EnemyPool");
            poolObj.transform.SetParent(transform);
            enemyPool = poolObj.AddComponent<ObjectPool>();
            enemyPool.prefab = enemyPrefab;
            enemyPool.initialPoolSize = poolSize;
            enemyPool.expandIfNeeded = true;
            enemyPool.maxPoolSize = poolSize * 3; // Максимум в 3 раза больше начального

            // ВАЖНО: Инициализируем пул вручную сразу
            enemyPool.Initialize();

        }

        // Планируем первый спавн
        ScheduleNextSpawn();
    }

    void Update()
    {
        // Удаляем мертвых врагов из списка (оптимизировано - не каждый кадр)
        cleanupTimer += Time.deltaTime;
        if (cleanupTimer >= CLEANUP_INTERVAL)
        {
            cleanupTimer = 0f;
            CleanupDeadEnemies();
        }

        // Проверяем, пора ли спавнить нового врага
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

        // Проверяем расстояние от игрока (оптимизация: sqrMagnitude вместо Distance)
        if (player != null)
        {
            float sqrDistance = (spawnPosition - player.position).sqrMagnitude;
            float minDistanceSqr = minDistanceFromPlayer * minDistanceFromPlayer;

            if (sqrDistance < minDistanceSqr)
            {
                // Слишком близко к игроку, пробуем еще раз позже
                ScheduleNextSpawn(1f); // Попробуем через 1 секунду
                return;
            }
        }

        // Спавним врага через пул или обычным способом
        GameObject enemy;
        if (useObjectPooling && enemyPool != null)
        {
            enemy = enemyPool.Get(spawnPosition, Quaternion.identity);

            // Сбрасываем состояние врага
            DoomHealth health = enemy.GetComponent<DoomHealth>();
            if (health != null)
            {
                health.ResetHealth();
                health.SetPool(enemyPool); // Связываем с пулом для возврата
            }
        }
        else
        {
            enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        }

        spawnedEnemies.Add(enemy);
        ApplyDifficultyToEnemy(enemy);   // масштабируем под текущий уровень

    }

    /// <summary>
    /// Применяет настройки сложности из DifficultyManager.Current к заспавненному врагу.
    /// Безопасно для пула: HP/скорость/урон считаются от базы.
    /// </summary>
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
            // Случайная позиция в заданной области
            Vector3 randomPos = spawnAreaCenter + new Vector3(
                Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
                0,
                Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
            );

            // Raycast вниз чтобы найти землю
            if (Physics.Raycast(randomPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            {
                return hit.point + Vector3.up * 0.5f;
            }

            return randomPos;
        }
        else
        {
            // Используем заданные точки спавна
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
        // Оптимизированная очистка без лямбда-выражений
        // Удаляем null объекты и неактивных врагов (вернувшихся в пул)
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null || !spawnedEnemies[i].activeInHierarchy)
                spawnedEnemies.RemoveAt(i);
        }
    }

    // Визуализация в редакторе
    void OnDrawGizmos()
    {
        // Рисуем область спавна
        if (useRandomPositions)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
        }

        // Рисуем точки спавна
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

        // Рисуем минимальное расстояние от игрока
        if (player != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);
        }
    }

    // Публичные методы для управления
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
