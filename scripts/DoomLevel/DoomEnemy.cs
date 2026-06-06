using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class DoomEnemy : MonoBehaviour
{
    [Header("AI Settings")]
    public float detectionRange = 15f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("Combat")]
    public int attackDamage = 10;
    public LayerMask playerLayer;

    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float chaseSpeed = 5f;

    [Header("References")]
    public Transform player;
    public AudioSource attackSound;
    public Animator animator;

    private NavMeshAgent agent;
    private DoomHealth health;
    private DoomHealth _playerHealth;
    private float nextAttackTime = 0f;
    private bool isChasing = false;

    private float _baseMove, _baseChase;
    private int _baseDmg;
    private bool _diffCaptured;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<DoomHealth>();
        agent.speed = moveSpeed;

        if (player == null && PlayerManager.Instance != null)
            player = PlayerManager.Instance.PlayerTransform;
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (PlayerManager.Instance != null) _playerHealth = PlayerManager.Instance.PlayerHealth;
        if (_playerHealth == null && player != null)
            _playerHealth = player.GetComponentInParent<DoomHealth>() ?? player.GetComponentInChildren<DoomHealth>();
    }

    void Update()
    {
        if (health != null && health.IsDead())
        {
            agent.isStopped = true;
            return;
        }

        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            if (!isChasing)
            {
                isChasing = true;
                agent.speed = chaseSpeed;
            }

            agent.SetDestination(player.position);

            if (distanceToPlayer <= attackRange && Time.time >= nextAttackTime)
            {
                Attack();
                nextAttackTime = Time.time + attackCooldown;
            }
        }
        else
        {
            if (isChasing)
            {
                isChasing = false;
                agent.speed = moveSpeed;
                agent.ResetPath();
            }
        }

        if (animator != null)
        {
            animator.SetBool("IsMoving", agent.velocity.magnitude > 0.1f);
            animator.SetBool("IsAttacking", distanceToPlayer <= attackRange);
        }
    }

    void Attack()
    {
        if (attackSound != null)
            attackSound.Play();

        if (_playerHealth != null && !_playerHealth.IsDead())
            _playerHealth.TakeDamage(attackDamage);
    }

    public void ApplyDifficulty(float speedMult, float damageMult)
    {
        if (!_diffCaptured)
        {
            _baseMove  = moveSpeed;
            _baseChase = chaseSpeed;
            _baseDmg   = attackDamage;
            _diffCaptured = true;
        }
        moveSpeed    = _baseMove  * speedMult;
        chaseSpeed   = _baseChase * speedMult;
        attackDamage = Mathf.RoundToInt(_baseDmg * damageMult);
        if (agent != null) agent.speed = isChasing ? chaseSpeed : moveSpeed;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
