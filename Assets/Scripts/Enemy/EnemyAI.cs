using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Datos del Enemigo")]
    [SerializeField] private EnemyDataSO enemyData;

    [Header("Audio de Ataque")]
    [SerializeField] private AudioClip impAttackSFX; // Asigna AtackEnemy.mp3 aquí

    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;
    private HealthComponent playerHealth;

    private float lastAttackTime;
    private bool isAttacking;
    private float attackSafetyTimer;

    private float speedMultiplier = 1.0f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private void Awake()
    {
        // 1. Obtener componentes ANTES de cualquier llamada externa
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (enemyData == null)
        {
            Debug.LogError($"<color=red>[EnemyAI] ¡Falta asignar EnemyDataSO en {gameObject.name}!</color>");
            enabled = false;
            return;
        }

        // Buscar al jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerHealth = player.GetComponent<HealthComponent>();
        }

        ApplyStats();
    }

    public void SetDifficultyMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(1.0f, multiplier);

        // Si ya pasó Awake(), aplicamos las estadísticas
        if (agent != null || animator != null)
        {
            ApplyStats();
        }
    }

    private void ApplyStats()
    {
        if (enemyData == null) return;

        if (agent != null)
        {
            agent.speed = enemyData.moveSpeed * speedMultiplier;
            agent.stoppingDistance = enemyData.attackRange * 0.8f;
        }

        if (animator != null)
        {
            animator.speed = speedMultiplier;
        }
    }

    private void Update()
    {
        if (playerTransform == null || enemyData == null) return;

        // Liberación de seguridad
        if (isAttacking)
        {
            attackSafetyTimer += Time.deltaTime;
            float currentAnimSpeed = (animator != null && animator.speed > 0) ? animator.speed : 1f;
            if (attackSafetyTimer >= (2.0f / currentAnimSpeed))
            {
                OnAttackFinished();
            }
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float adjustedCooldown = enemyData.attackCooldown / speedMultiplier;

        if (distanceToPlayer <= enemyData.attackRange && Time.time >= lastAttackTime + adjustedCooldown)
        {
            StartAttack();
        }
        else if (!isAttacking && agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(playerTransform.position);

            if (animator != null)
            {
                animator.SetBool(IsMovingHash, agent.velocity.magnitude > 0.1f);
            }
        }
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackSafetyTimer = 0f;

        // --- REPRODUCIR SONIDO DE ATAQUE ---
        if (AudioManager.Instance != null && impAttackSFX != null)
        {
            AudioManager.Instance.PlaySFXAtPosition(impAttackSFX, transform.position, 0.55f);
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        lastAttackTime = Time.time;

        if (playerTransform != null)
        {
            Vector3 lookDirection = (playerTransform.position - transform.position).normalized;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }

        if (animator != null)
        {
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);
        }
    }

    // =========================================================================
    // EVENTO DE ANIMACIÓN: Golpe de precisión frontal
    // =========================================================================
    public void ExecuteAttackHit()
    {
        if (playerTransform == null || enemyData == null) return;

        if (playerHealth == null)
        {
            playerHealth = playerTransform.GetComponent<HealthComponent>();
        }

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        directionToPlayer.y = 0; // Ignorar diferencia de altura

        // 1. Verificar si el Pastor está enfrente del Imp (Ángulo de ataque)
        float dotProduct = Vector3.Dot(transform.forward, directionToPlayer);

        // 2. Verificar distancia
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float effectiveHitRadius = Mathf.Max(enemyData.attackHitRadius, enemyData.attackRange + 0.3f);

        // SOLO HACE DAÑO SI: Está en rango Y el jugador está en un cono frontal de ~120° (dotProduct > 0.3f)
        if (distanceToPlayer <= effectiveHitRadius && dotProduct > 0.3f)
        {
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(enemyData.damage);
                Debug.Log($"<color=red>[EnemyAI] ¡Zarpazo frontal certero! Daño: {enemyData.damage}</color>");
            }
        }
        else
        {
            Debug.Log("<color=cyan>[EnemyAI] El zarpazo falló (Pastor fuera del ángulo o a la espalda).</color>");
        }
    }

    public void OnAttackFinished()
    {
        isAttacking = false;
        attackSafetyTimer = 0f;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }
}