using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour, IPooledObject
{
    [Header("Datos del Enemigo")]
    [SerializeField] private EnemyDataSO enemyData;

    [Header("Audio de Ataque")]
    [Tooltip("Sonido del zarpazo (AtackEnemy.mp3). Se reproduce en el FRAME DE IMPACTO, " +
             "sincronizado con el Animation Event ExecuteAttackHit del clip de ataque.")]
    [SerializeField] private AudioClip impAttackSFX;
    [Range(0f, 1f)]
    [Tooltip("Volumen del zarpazo. El SFX suena en el FRAME DE IMPACTO, así que debe destacar sobre " +
             "la música: por debajo de ~0.6 se pierde detrás de la mezcla.")]
    [SerializeField] private float attackSfxVolume = 0.75f;
    [Range(0f, 0.2f)]
    [Tooltip("Variación de tono (±) del zarpazo: dos golpes seguidos no suenan idénticos.")]
    [SerializeField] private float attackSfxPitchVariation = 0.08f;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform playerTransform;
    private HealthComponent playerHealth;

    private float lastAttackTime;
    private bool isAttacking;
    private float attackSafetyTimer;

    /// <summary>
    /// True cuando el golpe del ataque actual ya se resolvió (por el Animation Event). Evita
    /// aplicar daño o repetir el SFX dos veces si el evento llegara duplicado.
    /// </summary>
    private bool attackHitResolved;

    /// <summary>El aviso de "Animation Event perdido" solo se emite una vez por instancia.</summary>
    private bool attackHitWarningLogged;

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
    [Tooltip("Diagnóstico (APAGADO por defecto para dejar la consola de Unity 100% limpia): avisa una vez " +
             "por instancia si la animación de ataque termina sin invocar ExecuteAttackHit(). Actívalo solo " +
             "para depurar un clip: el respaldo por tiempo resuelve el golpe y su SFX con precisión (~0.28s), " +
             "así que con el evento ausente no hay ruido de consola, solo el mecanismo de respaldo.")]
    [SerializeField] private bool logMissingHitEvent = false;
    [Tooltip("Respaldo del golpe: instante (segundos de ANIMACIÓN) en el que el clip tiene el " +
             "Animation Event de impacto (AttackEnemy01: 0.38s). Si el evento no llegara, el golpe " +
             "y su SFX se resuelven aquí para que el ataque nunca quede mudo.")]
    [SerializeField] private float attackHitFallbackTime = 0.38f;
    [Tooltip("Margen extra (segundos de animación) que se espera antes de dar el Animation Event " +
             "por perdido. Evita el doble golpe si el evento real llega ligeramente tarde.")]
    [SerializeField] private float attackHitFallbackGrace = 0.08f;

    [Header("Escalado de Dificultad (runtime)")]
    [Tooltip("Tiempo máximo que puede durar la animación de ataque antes de liberar al enemigo.")]
    [SerializeField] private float attackSafetyTimeout = 2f;
    [Tooltip("Actívalo solo para depurar: el spawner ya registra el escalado de cada enemigo.")]
    [SerializeField] private bool logSpawnScaling = false;

    private float speedMultiplier = 1.0f;
    private float damageMultiplier = 1.0f;
    private bool isBoss;
    private Vector3 baseScale = Vector3.one;

    /// <summary>
    /// True cuando el enemigo está congelado (secuencia de muerte del jugador): no persigue, no
    /// ataca y su animación queda parada.
    /// </summary>
    private bool isFrozen;

    [Header("Jefe")]
    [Tooltip("Nombre del objeto hijo que se activa si esta instancia se marca como jefe (opcional).")]
    [SerializeField] private string bossAuraChildName = "BossAura";

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    /// <summary>
    /// Aviso del respaldo ya emitido: se registra una sola vez por instancia para no inundar la
    /// consola (la instancia se recicla y el diagnóstico se repetiría en cada aparición).
    /// </summary>
    private bool attackHitFallbackLogged;

    private void Awake()
    {
        // 1. Obtener componentes ANTES de cualquier llamada externa
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

        // Escala original del prefab: se restaura al reciclar (el jefe la multiplica).
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Reasegura flags y componentes al activarse (spawn del pool o reinicio de escena):
        // un enemigo que quedó congelado por el GameOver anterior nunca puede aparecer estático.
        // Awake ya ejecutó en este punto, así que agent/animator están cacheados.
        ResetEnemyState();
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

    /// <summary>True si el enemigo está congelado (fin de partida: permanece estático).</summary>
    public bool IsFrozen => isFrozen;

    /// <summary>
    /// Congela (o descongela) al enemigo por completo: detiene su NavMeshAgent y su animación para
    /// que quede estático. El Update sale de inmediato, así que tampoco arranca ataques nuevos ni
    /// aplica el respaldo del golpe. Lo usa la secuencia de muerte del jugador (GameManager).
    /// </summary>
    public void SetFrozen(bool value)
    {
        if (isFrozen == value) return;

        isFrozen = value;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (value)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            else
            {
                agent.isStopped = false;
            }
        }

        if (animator != null)
        {
            // Congelar también la animación: deja al enemigo completamente quieto (ni caminar ni atacar).
            animator.SetBool(IsMovingHash, false);
            animator.speed = value ? 0f : 1f;
        }
    }

    /// <summary>
    /// Devuelve al enemigo a un estado operativo limpio: descongela (isFrozen = false), habilita
    /// nuevamente el NavMeshAgent, reactiva la animación y limpia los flags de ataque
    /// (isAttacking, attackHitResolved, timers). Lo llaman OnEnable() y el reinicio de partida
    /// para que ningún enemigo se quede estático o "congelado" tras un GameOver.
    /// </summary>
    public void ResetEnemyState()
    {
        isFrozen = false;
        isAttacking = false;
        attackHitResolved = false;
        attackSafetyTimer = 0f;
        lastAttackTime = 0f;

        if (animator != null && animator.speed <= 0f)
        {
            animator.speed = 1f;
        }

        if (agent != null)
        {
            // Se re-habilita por completo: durante la secuencia de muerte pudo quedar detenido.
            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.velocity = Vector3.zero;
            }
        }
    }

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

        // Instancia reciclada: el ataque empieza de cero y el diagnóstico puede avisar otra vez.
        attackHitResolved = false;
        attackHitWarningLogged = false;

        // Ningún enemigo del pool puede volver a la escena congelado por una muerte anterior.
        isFrozen = false;

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
        // Fin de partida: el enemigo queda estático (ni persigue, ni ataca, ni anima).
        if (isFrozen) return;

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

            float animationSpeed = Mathf.Max(0.1f, CurrentAttackAnimationSpeed);

            // --- RESPALDO DEL GOLPE (si el Animation Event no llega) ---
            // El clip AttackEnemy01 trae el evento 'ExecuteAttackHit' en el frame de impacto: ahí
            // suenan el zarpazo y el daño. Si ese evento no se disparara (clip re-exportado sin él,
            // método renombrado o Animator movido a otro GameObject), el golpe se resuelve por
            // TIEMPO: el ataque nunca queda mudo ni deja de hacer daño.
            // Con el evento funcionando este bloque no hace nada, porque attackHitResolved ya es true.
            if (!attackHitResolved)
            {
                float fallbackLimit = (attackHitFallbackTime + Mathf.Max(0f, attackHitFallbackGrace)) / animationSpeed;

                if (attackSafetyTimer >= fallbackLimit)
                {
                    LogMissingHitEventFallback();
                    ExecuteAttackHit();
                }
            }

            // El margen se acorta si el ataque se reproduce más rápido.
            float safetyLimit = attackSafetyTimeout / animationSpeed;

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

        // El sonido del zarpazo YA NO se lanza aquí. La animación arranca con el "viento" del
        // brazo, así que sonaba antes del golpe: el Animation Event del clip sitúa el impacto en
        // 0.38s de animación y, a velocidad 1.35, eso son ~0.28s reales de desfase.
        // Ahora el SFX lo dispara ExecuteAttackHit(), el evento del FRAME DE IMPACTO:
        // audio y aplicación del daño van siempre juntos (hit frame sync).
        attackHitResolved = false;

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
    // Lo invoca el Animation Event 'ExecuteAttackHit' del clip AttackEnemy01 en el frame de
    // impacto (0.38s de animación). Es el ÚNICO punto donde se aplica daño Y donde suena el
    // zarpazo: así el audio siempre queda sincronizado con el impacto real (hit frame sync).
    // =========================================================================
    public void ExecuteAttackHit()
    {
        // Un solo golpe por ataque: si el Animation Event llegara duplicado no se aplicaría el
        // daño ni se repetiría el sonido dos veces.
        if (attackHitResolved) return;

        attackHitResolved = true;

        // --- SFX EN EL FRAME DE IMPACTO (acierte o falle: es el sonido del zarpazo) ---
        PlayAttackSFX();

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

    /// <summary>
    /// Reproduce el sonido del zarpazo en el instante del impacto (acierte o falle: es el sonido
    /// del golpe). La variación de tono evita que una horda suene como un solo clip en bucle.
    /// </summary>
    private void PlayAttackSFX()
    {
        if (impAttackSFX == null) return;

        AudioManager audio = AudioManager.Instance;

        if (audio == null) return;

        audio.PlaySFXAtPosition(impAttackSFX, transform.position, attackSfxVolume, attackSfxPitchVariation);
    }

    /// <summary>
    /// Aviso (una sola vez por instancia) de que el golpe se resolvió con el respaldo por tiempo en
    /// lugar del Animation Event: el ataque suena y hace daño, pero conviene revisar el clip para
    /// un sincronizado exacto con la garra.
    /// </summary>
    private void LogMissingHitEventFallback()
    {
        if (!logMissingHitEvent || attackHitFallbackLogged) return;

        attackHitFallbackLogged = true;

        Debug.LogWarning(
            $"[EnemyAI] '{gameObject.name}': el Animation Event 'ExecuteAttackHit' no llegó; el golpe " +
            "y su SFX se han resuelto por tiempo (fallback). Revisa el Animation Event del clip de ataque.",
            this);
    }

    public void OnAttackFinished()
    {
        // Diagnóstico: si la animación terminó sin pasar por el frame de impacto, el Animation
        // Event se ha perdido (clip sustituido o editado) y este enemigo NO estaría haciendo daño.
        // Se avisa una sola vez por instancia para no inundar la consola.
        if (logMissingHitEvent && !attackHitResolved && !attackHitWarningLogged)
        {
            attackHitWarningLogged = true;
            Debug.LogWarning(
                $"[EnemyAI] '{gameObject.name}': la animación de ataque terminó sin invocar " +
                "ExecuteAttackHit(). Revisa el Animation Event del clip de ataque: sin él el enemigo " +
                "no aplica daño.", this);
        }

        attackHitResolved = true; // Un golpe tardío no debe aplicar daño fuera del ataque.
        isAttacking = false;
        attackSafetyTimer = 0f;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }
}