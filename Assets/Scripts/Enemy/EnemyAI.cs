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

    [Header("Optimización de CPU (NavMesh)")]
    [Tooltip("Ventana MÍNIMA entre recálculos de ruta (segundos). Evita que cada enemigo " +
             "recalcule su NavMeshPath en cada Update(): con una horda son miles de consultas " +
             "al NavMesh por segundo y eso se come el frame en CPU.")]
    [SerializeField] private float pathUpdateIntervalMin = 0.15f;
    [Tooltip("Ventana MÁXIMA entre recálculos de ruta (segundos). El intervalo real se sortea una " +
             "vez POR ENEMIGO entre el mínimo y el máximo, así toda la horda nunca recalcula su " +
             "ruta en el mismo frame (evita picos de CPU).")]
    [SerializeField] private float pathUpdateIntervalMax = 0.25f;
    [Tooltip("Distancia mínima (unidades) que debe moverse el jugador para que valga la pena " +
             "recalcular la ruta, aunque la ventana de tiempo ya haya expirado.")]
    [SerializeField] private float playerMoveThreshold = 0.5f;

    // Estado del throttling de ruta
    private float pathUpdateInterval;   // Sorteado por enemigo en [min, max]
    private float nextPathUpdateTime;   // Momento (Time.time) del próximo recálculo permitido
    private Vector3 lastPlayerTargetPosition;
    private bool animatorMovingState;   // Último valor enviado a IsMoving (evita SetBool redundante)

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
             "por instancia si la animación de ataque termina sin invocar ExecuteAttackHit(), porque eso " +
             "significa que al clip le falta el Animation Event y el enemigo NO hará daño. Actívalo solo " +
             "para depurar el clip.")]
    [SerializeField] private bool logMissingHitEvent = false;

    [Header("Escalado de Dificultad (runtime)")]
    [Tooltip("Tiempo máximo que puede durar la animación de ataque antes de liberar al enemigo.")]
    [SerializeField] private float attackSafetyTimeout = 2f;

    [Header("Juice (Micro-pausa de impacto)")]
    [Tooltip("Segundos reales de micro-pausa cuando la garra alcanza al jugador. 0 la desactiva. " +
             "Idempotente: nunca se encadenan dos micro-pausas por el mismo golpe.")]
    [SerializeField] private float hitStopOnPlayerHit = 0.04f;
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

    private void Awake()
    {
        // 1. Obtener componentes ANTES de cualquier llamada externa
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

        // Optimización GPU/CPU: Evita calcular huesos y animaciones si el enemigo está fuera del frustum de la cámara
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        // Escala original del prefab: se restaura al reciclar (el jefe la multiplica).
        baseScale = transform.localScale;

        isFrozen = false;
        isAttacking = false;
        attackHitResolved = false;
        if (animator != null) animator.speed = 1f;

        // Cada enemigo recibe su propia ventana de recálculo de ruta (offset aleatorio).
        RollPathUpdateInterval();
        nextPathUpdateTime = 0f;

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
            if (enemyData != null) agent.speed = enemyData.moveSpeed;
            if (agent.isOnNavMesh) agent.ResetPath();
        }
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
                // Al reanudar la persecución la ruta debe recomputarse YA: ResetPath() la dejó
                // vacía, así que sin forzar la ventana el enemigo se quedaría quieto hasta
                // que expirase su throttling (0.15-0.25s de parón visible).
                nextPathUpdateTime = 0f;
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

            agent.isStopped = false;
            if (enemyData != null)
            {
                agent.speed = CurrentMoveSpeed > 0f ? CurrentMoveSpeed : enemyData.moveSpeed;
            }

            if (agent.isOnNavMesh)
            {
                agent.velocity = Vector3.zero;
                agent.ResetPath();
                // Tras el reseteo la ruta está vacía: se fuerza el recálculo inmediato para que
                // el enemigo no espere a que expire su ventana de throttling.
                nextPathUpdateTime = 0f;
                if (playerTransform != null)
                {
                    lastPlayerTargetPosition = playerTransform.position;
                    agent.SetDestination(playerTransform.position);
                }
            }

            RollPathUpdateInterval();
            animatorMovingState = false;
        }
    }

    /// <summary>
    /// Descongela explícitamente al enemigo reanudando su IA y navegación.
    /// </summary>
    public void Unfreeze()
    {
        ResetEnemyState();
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

        // Instancia recién reciclada: la ventana de ruta se re-sortea y se fuerza el primer
        // recálculo, así el throttling nunca provoca un parón al entrar en escena.
        RollPathUpdateInterval();
        nextPathUpdateTime = 0f;
        lastPlayerTargetPosition = playerTransform != null ? playerTransform.position : transform.position;
        animatorMovingState = false;
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

        // Objetivo nuevo o reasignado: se invalida la ventana de throttling para que la
        // ruta se recalcule en el primer Update (si no, tardaría hasta 0.25s en responder).
        nextPathUpdateTime = 0f;
        lastPlayerTargetPosition = target != null ? target.position : transform.position;
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

        // Liberación de seguridad del ATAQUE (no aplica daño).
        if (isAttacking)
        {
            attackSafetyTimer += Time.deltaTime;

            // --- IMPORTANTE: aquí NO se aplica daño en ningún momento ---
            // El ÚNICO punto que hace daño es ExecuteAttackHit(), invocado por el Animation
            // Event del clip en su frame exacto de impacto. Antes había aquí un "fallback por
            // tiempo" (attackHitFallbackTime) que llamaba a ExecuteAttackHit() al llegar a un
            // umbral: como MonoBehaviour.Update() se ejecuta ANTES que la evaluación del
            // Animator, ese disparo adelantaba el daño al frame anterior al de la garra
            // (el jugador veía el zarpazo y ya había recibido daño). Eliminado por completo.
            // El Animated Event es ahora la fuente única e inevitable del golpe.

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

            // ==================================================================
            // OPTIMIZACIÓN DE CPU: Throttling de SetDestination
            // Antiguamente la ruta se recalculaba en CADA Update() de cada enemigo.
            // Con una horda de ~100 enemigos a 120 FPS eso son ~12.000 consultas
            // de pathfinding por segundo (la causa principal del saturado de CPU
            // que terminaba en GPU Timeout / TDR de D3D11).
            // Ahora solo se recalcula si:
            //   1) Se agotó la ventana de este enemigo (0.15s - 0.25s, sorteada), y
            //   2) El jugador se ha movido más de 'playerMoveThreshold' (0.5 u).
            // El NavMesh conserva la ruta existente entre recálculos, así que el
            // movimiento es idéntico a ojos del jugador, solo con menos cálculo.
            // ==================================================================
            if (ShouldUpdatePath())
            {
                agent.SetDestination(playerTransform.position);
            }

            if (animator != null)
            {
                // SetBool solo cuando el valor cambia: evita una llamada nativa por
                // enemigo y frame (ahorro real con hordas grandes).
                bool isMovingNow = agent.velocity.magnitude > 0.1f;

                if (isMovingNow != animatorMovingState)
                {
                    animatorMovingState = isMovingNow;
                    animator.SetBool(IsMovingHash, isMovingNow);
                }
            }
        }
    }

    /// <summary>
    /// Sortea la ventana de recálculo de ruta de ESTE enemigo dentro de [min, max].
    /// Al ser aleatorio por instancia, los enemigos de una horda no sincronizan sus
    /// recálculos en el mismo frame: el coste de CPU se reparde en lugar de darse en picos.
    /// </summary>
    private void RollPathUpdateInterval()
    {
        float min = Mathf.Max(0.02f, pathUpdateIntervalMin);
        float max = Mathf.Max(min, pathUpdateIntervalMax);

        pathUpdateInterval = Random.Range(min, max);
    }

    /// <summary>
    /// Devuelve true únicamente cuando este enemigo tiene permiso de recalcular su ruta.
    /// Reglas:
    ///   - Fuera de su ventana (0.15s - 0.25s, sorteada por instancia): NUNCA recalcula.
    ///   - Dentro de la ventana: solo recalcula si el jugador se movió &gt;= 0.5 unidades.
    ///   - Sin ruta activa: se fuerza el recálculo (spawn desde el pool, reinicio de escena,
    ///     destino invalidado o fin de ataque), para que el enemigo nunca se quede quieto.
    /// </summary>
    private bool ShouldUpdatePath()
    {
        if (agent == null || playerTransform == null) return false;

        // 1) El agente no tiene ninguna ruta activa: hay que darle una sí o sí.
        //    Cubre el primer frame tras aparecer del pool, el reinicio de escena y el
        //    caso de que el NavMesh haya invalidado la ruta (destino inalcanzable).
        //    Se ignora si una ruta está a punto de estar lista (pathPending): ahí el
        //    SetDestination ya se emitió y recalcular otra vez sería tirar CPU a la basura.
        if (!agent.hasPath && !agent.pathPending)
        {
            RefreshPathTracking();
            return true;
        }

        // 2) Fuera de la ventana de este enemigo: no se toca nada.
        if (Time.time < nextPathUpdateTime)
        {
            return false;
        }

        // 3) Ventana agotada: solo recalcula si el jugador se ha movido lo suficiente.
        if ((playerTransform.position - lastPlayerTargetPosition).sqrMagnitude >= playerMoveThreshold * playerMoveThreshold)
        {
            RefreshPathTracking();
            return true;
        }

        // 4) El jugador apenas se movió: se aplaza la ventana sin recalcular nada.
        nextPathUpdateTime = Time.time + pathUpdateInterval;
        return false;
    }

    /// <summary>
    /// Registra la ruta que se acaba de calcular y programa la próxima ventana de este enemigo.
    /// </summary>
    private void RefreshPathTracking()
    {
        if (playerTransform != null)
        {
            lastPlayerTargetPosition = playerTransform.position;
        }

        nextPathUpdateTime = Time.time + pathUpdateInterval;
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
            // CORRECCIÓN CRÍTICA: el culling de animación rompe los Animation Events.
            // Con CullUpdateTransforms, si el enemigo queda fuera del frustum el Animator
            // NO evalúa el clip y ExecuteAttackHit() nunca se dispara (=> daño cero).
            // Como el rango de ataque (radio ~2m) puede alcanzarse desde fuera de cámara,
            // durante el ataque se fuerza AlwaysAnimate para garantizar el evento exacto.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);
        }
    }

    // =========================================================================
    // EVENTO DE ANIMACIÓN: Golpe de precisión frontal
    // -------------------------------------------------------------------------
    // Lo invoca el Animation Event 'ExecuteAttackHit' del clip de ataque en SU frame
    // exacto de impacto. Es la ÚNICA fuente de daño y del SFX del zarpazo de todo el
    // proyecto: así el audio, el daño y la animación van siempre sincronizados.
    //
    // CONEXIÓN CON EL ANIMATOR (requisito):
    //   - El método es PÚBLICO, que es lo que exige Unity para resolverlo en un
    //     Animation Event (Unity lo invoca por SendMessage sobre el GameObject que
    //     tiene el Animator; por eso debe ser public y sin parámetros).
    //   - El Animator se busca con GetComponentInChildren<Animator>(), por lo que el
    //     evento funciona tanto si el Animator está en el propio objeto del enemigo
    //     como si está en un hijo (modelo visual). SendMessage llega igual a este
    //     componente porque ambos están bajo el mismo GameObject raíz.
    //   - AttackHash dispara el estado/trigger "Attack"; el clip de ese estado lleva
    //     el evento a ~0.38s de animación (frame en que la garra contacta al jugador).
    // =========================================================================
    public void ExecuteAttackHit()
    {
        // 1) Un solo golpe por ataque: si el evento llegara duplicado (p. ej. dos capas
        //    del Animator evaluando el mismo clip) no se aplicaría daño ni se repetiría
        //    el sonido dos veces.
        if (attackHitResolved) return;

        // 2) Si el ataque ya terminó (evento huérfano de un estado anterior, o el enemigo
        //    fue liberado por el timeout de seguridad), el golpe ya no es válido: nunca
        //    se aplica daño fuera de la ventana de impacto de una animación en curso.
        if (!isAttacking) return;

        attackHitResolved = true;

        // --- SFX EN EL FRAME DE IMPACTO (acierte o falle: es el sonido de la garra) ---
        PlayAttackSFX();

        if (playerTransform == null || enemyData == null) return;

        if (playerHealth == null)
        {
            playerHealth = playerTransform.GetComponent<HealthComponent>();
        }

        if (playerHealth == null) return;

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0f; // Ignorar diferencia de altura

        float sqrDistance = directionToPlayer.sqrMagnitude;
        float sqrHitRadius = EffectiveHitRadius * EffectiveHitRadius;

        // Fuera de radio: ni siquiera se normaliza ni se calcula el dot product.
        if (sqrDistance > sqrHitRadius) return;

        // Evitar normalizar un vector nulo cuando el enemigo está exactamente encima.
        if (sqrDistance > 0.0001f)
        {
            directionToPlayer /= Mathf.Sqrt(sqrDistance);
        }

        // Cono frontal de ~120° (dotProduct > 0.3f). Se evalúa en el frame exacto del
        // evento, así que si el jugador esquivó hacia un lado el zarpazo falla de verdad.
        if (Vector3.Dot(transform.forward, directionToPlayer) <= 0.3f) return;

        // Daño aplicado únicamente aquí: sin daño anticipado.
        // La micro-pausa va en ESTE frame, el de la garra, no antes: es el impacto lo que la justifica.
        playerHealth.TakeDamage(CurrentDamage);

        // HealthComponent ya pide su propia micro-pausa al recibir el golpe, así que esta
        // llamada normalmente se descarta por el anti-spam de HitStopManager (0.06s). Es
        // intencionada: deja el impacto explícito en el enemigo aunque el HealthComponent
        // deje de pedirlo, y Request es idempotente, así que nunca se encadenan dos pausas.
        GameStateController.Instance?.RequestHitStop(hitStopOnPlayerHit);
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

        // Restaura el culling de animación: fuera del ataque ya no hace falta evaluación
        // continua, así que el enemigo vuelve a ahorrar CPU cuando no se ve en cámara.
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            // El ataque deja al agente sin ruta: se fuerza el recálculo en el primer Update
            // posterior para que el enemigo vuelva a perseguir sin parón.
            nextPathUpdateTime = 0f;
            lastPlayerTargetPosition = playerTransform != null
                ? playerTransform.position
                : transform.position;
        }
    }
}