using UnityEngine;

public class DifficultyManager : MonoBehaviour
{
    public enum Mode { Shooter, Shop }

    [Header("Что это за сцена")]
    public Mode mode = Mode.Shooter;
    public bool autoApply = true;

    public static LevelConfig Current;

    void Start()
    {
        GameApi.Ensure();
        FetchAndApply();
    }

    public void FetchAndApply()
    {
        GameApi.Instance.Get("/api/me", (ok, body) =>
        {
            int lvl = 1;
            if (ok) lvl = Mathf.Max(1, JsonUtility.FromJson<MeResp>(body).current_level);
            GameApi.Instance.Get($"/api/levels/{lvl}", (ok2, body2) =>
            {
                if (!ok2) { Debug.LogWarning("[Difficulty] не удалось получить конфиг уровня"); return; }
                Current = JsonUtility.FromJson<LevelConfig>(body2);
                GameStats.SetLevel(Current.level);
                if (autoApply) Apply();
            });
        });
    }

    public void Apply()
    {
        if (Current == null) return;
        if (mode == Mode.Shooter) ApplyShooter();
        else ApplyShop();
    }

    void ApplyShooter()
    {
        var t = Object.FindFirstObjectByType<DoomTimer>();
        if (t != null) t.SetTotalTime(Current.shooter.survival_time);

        var sp = Object.FindFirstObjectByType<EnemySpawner>();
        if (sp != null)
        {
            sp.maxEnemies = Current.shooter.max_enemies;
            sp.minSpawnDelay = Current.shooter.spawn_min_delay;
            sp.maxSpawnDelay = Current.shooter.spawn_max_delay;
        }
    }

    void ApplyShop()
    {
        var cm = Object.FindFirstObjectByType<CustomerManager>();
        if (cm != null)
        {
            cm.badChance = Current.shop.bad_chance;
            cm.minOrder = Current.shop.min_order;
            cm.maxOrder = Current.shop.max_order;
            cm.customerMoveDuration = Current.shop.customer_move_duration;
        }
    }
}
