using UnityEngine;
using UnityEngine.Events;

public class DoomHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    [Header("Audio")]
    public AudioSource damageSound; // Звук получения урона
    public AudioClip[] damageSounds; // Массив звуков урона для вариации
    public float damageVolume = 0.3f; // Громкость звука урона
    public AudioSource deathSound; // Звук смерти

    [Header("Events")]
    public UnityEvent onDeath;
    public UnityEvent<int> OnHealthChanged; // С большой буквы для совместимости

    [Header("Player specific")]
    public bool isPlayer = false;
    public string gameOverScene = "";

    [Header("Object Pooling")]
    public bool usePooling = false; // Для врагов с пулом объектов
    public float deathDelay = 1f; // Задержка перед возвратом в пул

    private bool isDead = false;
    private ObjectPool parentPool; // Ссылка на пул

    // Базовое HP для безопасного масштабирования при переиспользовании из пула
    private int _baseMaxHealth;
    private bool _baseCaptured;

    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        OnHealthChanged?.Invoke(currentHealth);

        // Воспроизводим звук урона
        PlayDamageSound();

        if (currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
    }

    /// <summary>
    /// Сбросить здоровье для переиспользования из пула
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        OnHealthChanged?.Invoke(currentHealth);
    }

    /// <summary>
    /// Установить пул для возврата объекта
    /// </summary>
    public void SetPool(ObjectPool pool)
    {
        parentPool = pool;
        usePooling = true;
    }

    /// <summary>
    /// Применить множитель HP от базового значения. База запоминается один раз,
    /// поэтому переиспользование из пула не влияет на максимум.
    /// Вызывай до ResetHealth().
    /// </summary>
    public void ApplyHealthMultiplier(float mult)
    {
        if (!_baseCaptured) { _baseMaxHealth = maxHealth; _baseCaptured = true; }
        maxHealth = Mathf.RoundToInt(_baseMaxHealth * mult);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // Воспроизводим звук смерти
        if (deathSound != null)
            deathSound.Play();

        onDeath?.Invoke();

        if (isPlayer)
        {
            // Игрок умер → проигрыш, уходим на сцену итогов.
            GameStats.End(GameStats.Outcome.Lose);
        }
        else
        {
            // Враг умер — засчитываем килл.
            GameStats.AddKill();

            if (usePooling && parentPool != null)
            {
                // Возвращаем в пул через задержку
                parentPool.ReturnAfterDelay(gameObject, deathDelay);
            }
            else
            {
                // Уничтожаем через секунду (старый способ)
                Destroy(gameObject, deathDelay);
            }
        }
    }

    void PlayDamageSound()
    {
        if (damageSounds != null && damageSounds.Length > 0)
        {
            // Случайный звук из массива
            AudioClip clip = damageSounds[Random.Range(0, damageSounds.Length)];
            if (damageSound != null)
            {
                damageSound.PlayOneShot(clip, damageVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, damageVolume);
            }
        }
        else if (damageSound != null)
        {
            float originalVolume = damageSound.volume;
            damageSound.volume = damageVolume;
            damageSound.Play();
            damageSound.volume = originalVolume;
        }
    }

    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public bool IsDead() => isDead;
}
