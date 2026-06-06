using UnityEngine;
using UnityEngine.Events;

public class DoomHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    [Header("Audio")]
    public AudioSource damageSound;
    public AudioClip[] damageSounds;
    public float damageVolume = 0.3f;
    public AudioSource deathSound;

    [Header("Events")]
    public UnityEvent onDeath;
    public UnityEvent<int> OnHealthChanged;

    [Header("Player specific")]
    public bool isPlayer = false;
    public string gameOverScene = "";

    [Header("Object Pooling")]
    public bool usePooling = false;
    public float deathDelay = 1f;

    private bool isDead = false;
    private ObjectPool parentPool;

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

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void SetPool(ObjectPool pool)
    {
        parentPool = pool;
        usePooling = true;
    }

    public void ApplyHealthMultiplier(float mult)
    {
        if (!_baseCaptured) { _baseMaxHealth = maxHealth; _baseCaptured = true; }
        maxHealth = Mathf.RoundToInt(_baseMaxHealth * mult);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathSound != null)
            deathSound.Play();

        onDeath?.Invoke();

        if (isPlayer)
        {
            GameStats.End(GameStats.Outcome.Lose);
        }
        else
        {
            GameStats.AddKill();

            if (usePooling && parentPool != null)
            {
                parentPool.ReturnAfterDelay(gameObject, deathDelay);
            }
            else
            {
                Destroy(gameObject, deathDelay);
            }
        }
    }

    void PlayDamageSound()
    {
        if (damageSounds != null && damageSounds.Length > 0)
        {
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
