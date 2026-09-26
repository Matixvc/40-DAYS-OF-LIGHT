using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour, IPooledObject
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

    [Header("Ataque")]
    [Tooltip("Margen extra (metros) sobre el rango de ataque del EnemyDataSO: no hace falta estar encima del jugador.")]
    [SerializeField] private float attackRangeBonus = 0.35f;
    [Tooltip("Margen extra (metros) sobre el radio de impacto del EnemyDataSO.")]
    [SerializeField] private float attackHitRadiusBonus = 0.4f;
    [Tooltip("Velocidad de la animación de ataque. 1 = original; 1.35 = 35% más rápido.")]
    [SerializeField] private float attackSpeedMultiplier = 1.35f;
    [Tooltip("Cuánto influye la velocidad de movimiento en la velocidad del ataque (0 = nada, 1 = igual).")]
    [Range(0f, 1f)]
    [SerializeField] private float attackSpeedSpeedInfluence = 0.5f;

    [Header("Escalado de Dificultad (runtime)")]
    [Tooltip("Tiempo máximo que puede durar la animación de ataque antes de liberar al enemigo.")]
    [SerializeField] private float attackSafetyTimeout = 2f;
    [Tooltip("Actívalo solo para depurar: el spawner ya registra el escalado de cada enemigo.")]
    [SerializeField] private bool logSpawnScaling = false;

    private float speedMultiplier = 1.0f;
    private float damageMultiplier = 1.0f;
    private bool isBoss;
    private Vector3 baseScale = Vector3.one;

    [Header("Jefe")]
    [Tooltip("Nombre del objeto hijo que se activa si esta instancia se marca como jefe (opcional).")]
    [SerializeField] private string bossAuraChildName = "BossAura";

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private void Awake()
    {
        // 1. Obtener componentes ANTES de cualquier llamada externa
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

        // Escala original del prefab: se restaura al reciclar (el jefe la multiplica).
        baseScale = transform.localScale;
    }

    private void Start()
    {
        if (enemyData == null)
        {
            Debug.LogError($"<color=red>[EnemyAI] ¡Falta asignar EnemyDataSO en {gameObject.name}!</color>");
            enabled = false;
            return;
        }

        // Si el spawner ya asignó el objetivo, no hace falta buscarlo por tag.
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                SetTarget(player.transform);
            }
        }

        ApplyStats();
    }

    /// <summary>Datos base del enemigo (solo lectura).</summary>
    public EnemyDataSO Data => enemyData;

    /// <summary>Daño real de ESTA instancia (base × escalado). No modifica el ScriptableObject.</summary>
    public float CurrentDamage => enemyData != null ? enemyData.damage * damageMultiplier : 0f;

    /// <summary>Velocidad real de ESTA instancia (base × escalado).</summary>
    public float CurrentMoveSpeed => enemyData != null ? enemyData.moveSpeed * speedMultiplier : 0f;

    /// <summary>Rango real de ataque (base del SO + margen extra).</summary>
    public float EffectiveAttackRange => (enemyData != null ? enemyData.attackRange : 0f) + attackRangeBonus;

    /// <summary>Radio real del impacto en el momento exacto del golpe.</summary>
    public float EffectiveHitRadius
    {
        get
        {
            float baseRadius = enemyData != null ? enemyData.attackHitRadius : 0f;
            return Mathf.Max(baseRadius + attackHitRadiusBonus, EffectiveAttackRange + 0.3f);
        }
    }

    /// <summary>Velocidad de animación aplicada mientras se ejecuta el ataque.</summary>
    public float CurrentAttackAnimationSpeed =>
        attackSpeedMultiplier * Mathf.Lerp(1f, speedMultiplier, attackSpeedSpeedInfluence);

    public float DamageMultiplier => damageMultiplier;
    public float SpeedMultiplier => speedMultiplier;

    /// <summary>True mientras el enemigo está ejecutando su ataque (animación + golpe).</summary>
    public bool IsAttacking => isAttacking;

    /// <summary>True si esta instancia es un Jefe de Noche.</summary>
    public bool IsBoss => isBoss;

    /// <summary>
    /// Marca esta instancia como jefe: activa el aura opcional (hijo por nombre) y permite
    /// que otros sistemas la traten de forma especial (por ejemplo, no empujable por el jugador).
    /// </summary>
    public void MarkAsBoss()
    {
        isBoss = true;

        if (!string.IsNullOrEmpty(bossAuraChildName))
        {
            Transform aura = transform.Find(bossAuraChildName);

            if (aura != null)
            {
                aura.gameObject.SetActive(true);
            }
        }

        Debug.Log($"<color=magenta>[EnemyAI] '{gameObject.name}' marcado como JEFE.</color>", this);
    }

    // ======================================================================
    // RECICLAJE (OBJECT POOLING)
    // ======================================================================

    /// <summary>Deja el enemigo en estado limpio cada vez que se reutiliza desde el pool.</summary>
    public void OnPoolSpawned()
    {
        isAttacking = false;
        isBoss = false;
        attackSafetyTimer = 0f;
        lastAttackTime = 0f;

        damageMultiplier = 1f;
        speedMultiplier = 1f;

        if (animator != null)
        {
            animator.speed = 1f;
        }

        // Restaurar la escala del prefab (el jefe la había multiplicado).
        transform.localScale = baseScale;

        if (!string.IsNullOrEmpty(bossAuraChildName))
        {
            Transform aura = transform.Find(bossAuraChildName);

            if (aura != null)
            {
                aura.gameObject.SetActive(false);
            }
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }
    }

    public void OnPoolDespawned()
    {
        isAttacking = false;
        attackSafetyTimer = 0f;

        if (animator != null)
        {
            animator.speed = 1f;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    /// <summary>
    /// Asigna el objetivo al spawnear. Evita un FindGameObjectWithTag por enemigo.
    /// </summary>
    public void SetTarget(Transform target)
    {
        playerTransform = target;
        playerHealth = target != null ? target.GetComponent<HealthComponent>() : null;
    }

    /// <summary>
    /// Aplica el escalado de dificultad del spawner sobre el estado de ESTA instancia.
    /// Compatible con pooling: puede volver a llamarse al reciclar el enemigo.
    /// </summary>
    public void ApplySpawnScaling(float newDamageMultiplier, float newSpeedMultiplier)
    {
        damageMultiplier = Mathf.Max(0.01f, newDamageMultiplier);
        speedMultiplier = Mathf.Max(0.01f, newSpeedMultiplier);

        ApplyStats();

        if (logSpawnScaling)
        {
            Debug.Log(
                $"<color=orange>[EnemyAI] {gameObject.name} escalado | Daño: {CurrentDamage:0.0} (x{damageMultiplier:0.00}) | Velocidad: {CurrentMoveSpeed:0.00} (x{speedMultiplier:0.00})</color>",
                this);
        }
    }

    /// <summary>Compatibilidad: ajusta solo la velocidad manteniendo el escalado de daño actual.</summary>
    public void SetDifficultyMultiplier(float multiplier)
    {
        ApplySpawnScaling(damageMultiplier, multiplier);
    }

    private void ApplyStats()
    {
        if (enemyData == null) return;

        if (agent != null)
        {
            agent.speed = CurrentMoveSpeed;
            agent.stoppingDistance = EffectiveAttackRange * 0.8f;
        }

        // La velocidad de animación se gestiona en Update: solo se acelera durante el ataque
        // (acelerarla siempre alteraba el timing del golpe y del evento de animación).
    }

    private void Update()
    {
        if (playerTransform == null || enemyData == null) return;

        // La animación va acelerada SOLO durante el ataque y a velocidad normal el resto del tiempo.
        if (animator != null)
        {
            float targetAnimationSpeed = isAttacking ? CurrentAttackAnimationSpeed : 1f;

            if (!Mathf.Approximately(animator.speed, targetAnimationSpeed))
            {
                animator.speed = targetAnimationSpeed;
            }
        }

        // Liberación de seguridad
        if (isAttacking)
        {
            attackSafetyTimer += Time.deltaTime;

            // El margen se acorta si el ataque se reproduce más rápido.
            float safetyLimit = attackSafetyTimeout / Mathf.Max(0.1f, CurrentAttackAnimationSpeed);

            if (attackSafetyTimer >= safetyLimit)
            {
                OnAttackFinished();
            }
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float adjustedCooldown = enemyData.attackCooldown / Mathf.Max(0.1f, speedMultiplier);

        if (distanceToPlayer <= EffectiveAttackRange && Time.time >= lastAttackTime + adjustedCooldown)
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
        float effectiveHitRadius = EffectiveHitRadius;

        // SOLO HACE DAÑO SI: Está en rango Y el jugador está en un cono frontal de ~120° (dotProduct > 0.3f)
        if (distanceToPlayer <= effectiveHitRadius && dotProduct > 0.3f)
        {
            if (playerHealth != null)
            {
                float damage = CurrentDamage;
                playerHealth.TakeDamage(damage);
                Debug.Log($"<color=red>[EnemyAI] ¡Zarpazo frontal certero! Daño: {damage:0.0}</color>");
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