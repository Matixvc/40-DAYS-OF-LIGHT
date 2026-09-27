using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Data & Configuration")]
    [SerializeField] private CharacterDataSO stats;

    [Header("Ground & Gravity")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Estado de partida (RunStats)")]
    [SerializeField] private RunStats runStats;

    [Header("Input (Input System)")]
    [Tooltip("Lector de input del jugador. Si se deja vacío se resuelve automáticamente.")]
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Depuración")]
    [SerializeField] private bool logStatsChanges = true;

    [Header("Audio de Pasos (Arena)")]
    [Tooltip("Paso sobre arena al caminar (SFX_StepSand). Si se deja vacío se usa el catálogo del AudioManager.")]
    [SerializeField] private AudioClip stepSandSFX;
    [Tooltip("Paso sobre arena al correr (SFX_SprintSand). Si se deja vacío se usa el catálogo del AudioManager.")]
    [SerializeField] private AudioClip sprintSandSFX;
    [Tooltip("Metros recorridos entre pasos al caminar: la cadencia queda atada a la velocidad real.")]
    [SerializeField, Min(0.1f)] private float walkStepDistance = 2f;
    [Tooltip("Metros recorridos entre pasos al correr (zancada más larga).")]
    [SerializeField, Min(0.1f)] private float sprintStepDistance = 2.6f;
    [Range(0f, 1f)]
    [SerializeField] private float footstepVolume = 0.5f;
    [Range(0f, 0.2f)]
    [SerializeField] private float footstepPitchVariation = 0.12f;
    [SerializeField] private bool enableFootsteps = true;

    [Header("Muerte")]
    [Tooltip("Avisa una vez si el Animator no tiene el Trigger 'Die': la secuencia sigue, pero sin animación.")]
    [SerializeField] private bool logMissingDeathTrigger = true;

    private float distanceSinceLastStep;
    private bool isDead;

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;

    // Control de Sprint
    private bool isSprinting;
    private bool isSprintOnCooldown;
    private float sprintTimer;
    private float cooldownTimer;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DieHash = Animator.StringToHash("Die");

    public bool IsSprintOnCooldown => isSprintOnCooldown;

    /// <summary>True desde que arranca la secuencia de muerte (sus controles ya están desactivados).</summary>
    public bool IsDead => isDead;
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        // Se resuelve una sola vez: nunca se busca dentro de Update.
        ResolveRunStats(false);
        ResolveInputReader(false);
    }

    private void OnEnable()
    {
        SubscribeToRunStats();
    }

    private void Start()
    {
        // Si RunStats vive en otro GameObject, su Awake ya ha terminado en este punto.
        ResolveRunStats(true);
        SubscribeToRunStats();
        ResolveInputReader(true);
    }

    private void OnDisable()
    {
        if (runStats != null)
        {
            runStats.OnStatsChanged -= HandleStatsChanged;
        }
    }

    private void ResolveRunStats(bool logIfMissing)
    {
        if (runStats == null)
        {
            runStats = GetComponent<RunStats>();
        }

        if (runStats == null)
        {
            runStats = RunStats.Active;
        }

        if (runStats == null)
        {
            if (logIfMissing)
            {
                Debug.LogError(
                    "[PlayerController] No se encontró RunStats. La velocidad de partida no podrá aplicarse: " +
                    "añade RunStats al GameObject del Player o asígnalo en el campo 'Run Stats'.",
                    this);
            }

            return;
        }

        if (logIfMissing && logStatsChanges)
        {
            Debug.Log($"[PlayerController] Usando RunStats del GameObject '{runStats.gameObject.name}'.", this);
        }
    }

    private void ResolveInputReader(bool logIfMissing)
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (inputReader == null)
        {
            inputReader = PlayerInputReader.Instance;
        }

        if (inputReader == null && logIfMissing)
        {
            Debug.LogError(
                "[PlayerController] No se encontró PlayerInputReader: el jugador no podrá moverse. " +
                "Añade el componente PlayerInputReader al Player.",
                this);
        }
    }

    private void SubscribeToRunStats()
    {
        if (runStats == null) return;

        runStats.OnStatsChanged -= HandleStatsChanged; // Evita suscripciones duplicadas
        runStats.OnStatsChanged += HandleStatsChanged;
    }

    /// <summary>Se llama cuando cambia cualquier estadística de partida.</summary>
    private void HandleStatsChanged()
    {
        if (!logStatsChanges) return;

        Debug.Log(
            $"<color=cyan>[PlayerController] Velocidad de partida actualizada: {runStats.MoveSpeed:0.00}</color>",
            this);
    }

    // --- Valores de partida (RunStats) con respaldo en el ScriptableObject base ---
    private float SprintDuration => runStats != null ? runStats.SprintDuration : (stats != null ? stats.sprintDuration : 2f);
    private float SprintCooldown => runStats != null ? runStats.SprintCooldown : (stats != null ? stats.sprintCooldown : 3f);
    private float RotationSpeed => runStats != null ? runStats.RotationSpeed : (stats != null ? stats.rotationSpeed : 10f);

    private void Update()
    {
        HandleSprintTimers();
        HandleMovement();
    }

    private void HandleSprintTimers()
    {
        if (stats == null && runStats == null) return;

        // Si se está corriendo, agotar el tiempo de sprint
        if (isSprinting)
        {
            sprintTimer -= Time.deltaTime;
            if (sprintTimer <= 0f)
            {
                // Se acabó el sprint: iniciar cooldown
                isSprinting = false;
                isSprintOnCooldown = true;
                cooldownTimer = SprintCooldown;
                Debug.Log("<color=orange>[Sprint] Cansado. Entrando en Cooldown...</color>");
            }
        }
        // Si está en cooldown, recuperar la energía
        else if (isSprintOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isSprintOnCooldown = false;
                Debug.Log("<color=green>[Sprint] ¡Listo para usar de nuevo!</color>");
            }
        }
    }

    private void HandleMovement()
    {
      if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Input System: el lector entrega la dirección combinada (teclado o mando).
        Vector2 moveInput = inputReader != null ? inputReader.Move : Vector2.zero;
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        // Sprint con Input System: Shift/Espacio en teclado, stick pulsado en mando.
        bool sprintPressed = inputReader != null && inputReader.SprintHeld;
        
        if (sprintPressed && direction.magnitude >= 0.1f && !isSprintOnCooldown && !isSprinting)
        {
            isSprinting = true;
            sprintTimer = SprintDuration;
            Debug.Log("<color=yellow>[Sprint] ¡Corriendo!</color>");
        }
        else if (!sprintPressed && isSprinting)
        {
            // Calcular cuánto tiempo se usó realmente del sprint (en porcentaje de 0 a 1)
            float timeUsed = SprintDuration - sprintTimer;
            float usedPercent = SprintDuration > 0f ? Mathf.Clamp01(timeUsed / SprintDuration) : 1f;

            // Aplicar un cooldown proporcional al tiempo gastado
            isSprinting = false;
            isSprintOnCooldown = true;
            cooldownTimer = SprintCooldown * usedPercent;
        }

        // 1. Calcular velocidad actual
        float baseSpeed = runStats != null ? runStats.MoveSpeed : (stats != null ? stats.moveSpeed : 5f);
        float sprintMult = runStats != null ? runStats.SprintMultiplier : (stats != null ? stats.sprintMultiplier : 1.6f);
        float currentSpeed = isSprinting ? baseSpeed * sprintMult : baseSpeed;
        float currentRotSpeed = RotationSpeed;

        // 2. Aplicar Movimiento y Rotación
        if (direction.magnitude >= 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * currentRotSpeed);

            controller.Move(direction * (currentSpeed * Time.deltaTime));
        }

        // 3. Gravedad
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

       // 4. Parámetro para el Animator
    if (animator != null)
    {
    // Si se está moviendo, pasamos 1f (Walk) o 1.6f (Run), y 0f (Idle) si está quieto
    float speedPercent = direction.magnitude * (isSprinting ? sprintMult : 1f);
    animator.SetFloat(SpeedHash, speedPercent, 0.1f, Time.deltaTime);
    }

    // 5. Pasos de arena sincronizados con el movimiento real
    HandleFootsteps(direction, currentSpeed, isSprinting);
    }
    /// <summary>
    /// Pasos de arena sincronizados con el movimiento: la DISTANCIA recorrida marca la cadencia, así
    /// la zancada se alarga al correr y los pasos se detienen cuando el jugador se para.
    /// Clip propio del Player o, si no hay, el del catálogo del AudioManager (SFX_StepSand/SprintSand).
    /// </summary>
    private void HandleFootsteps(Vector3 direction, float currentSpeed, bool sprinting)
    {
        if (!enableFootsteps || controller == null) return;

        // Solo con los pies en el suelo y con movimiento real (los deslizamientos no suenan).
        if (!controller.isGrounded || direction.sqrMagnitude < 0.01f || currentSpeed <= 0.01f) return;

        float strideDistance = Mathf.Max(0.1f, sprinting ? sprintStepDistance : walkStepDistance);

        distanceSinceLastStep += currentSpeed * Time.deltaTime;

        if (distanceSinceLastStep < strideDistance) return;

        distanceSinceLastStep = 0f;

        AudioManager audio = AudioManager.Instance;

        if (audio == null) return;

        audio.PlayFootstepSFX(
            sprinting,
            transform.position,
            sprinting ? sprintSandSFX : stepSandSFX,
            footstepVolume,
            footstepPitchVariation);
    }

    /// <summary>
    /// Secuencia de muerte del jugador: corta el sprint, dispara el Trigger 'Die' (si el Animator lo
    /// tiene) e inhabilita este componente, de modo que deja de leer input y de moverse.
    /// La invoca GameManager cuando la vida del jugador llega a 0.
    /// </summary>
    public void PlayDeathSequence()
    {
        if (isDead) return;

        isDead = true;

        // Estado de movimiento a cero: el Animator no puede quedarse en Locomotion/Walk.
        isSprinting = false;
        isSprintOnCooldown = false;
        sprintTimer = 0f;
        cooldownTimer = 0f;
        distanceSinceLastStep = 0f;

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, 0f);

            if (HasTrigger(animator, DieHash))
            {
                animator.SetTrigger(DieHash);
            }
            else if (logMissingDeathTrigger)
            {
                Debug.LogWarning(
                    "[PlayerController] El Animator no tiene el Trigger 'Die': el jugador se quedará " +
                    "quieto, pero sin animación de muerte. Añádelo en " +
                    "Assets/Animations/PlayerPastor_AnimatorController.controller " +
                    "(Parameters → + → Trigger → 'Die').",
                    this);
            }
        }

        // Inhabilita controles e inputs: Update deja de ejecutarse (sin movimiento, sin sprint, sin pasos).
        enabled = false;
    }

    /// <summary>
    /// True si el Animator declara un Trigger con ese hash. Evita el error de consola que provoca
    /// SetTrigger con un parámetro inexistente (el Trigger 'Die' es opcional).
    /// </summary>
    private static bool HasTrigger(Animator targetAnimator, int parameterHash)
    {
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null) return false;

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].nameHash == parameterHash)
            {
                return true;
            }
        }

        return false;
    }

    // Propiedades para que la UI lea el estado del Sprint
    public float SprintPercent
    {
    get
    {
        if (stats == null && runStats == null) return 1f;
        if (isSprinting) return SprintDuration > 0f ? sprintTimer / SprintDuration : 0f;
        if (isSprintOnCooldown) return SprintCooldown > 0f ? 1f - (cooldownTimer / SprintCooldown) : 1f;
        return 1f; // Energía llena por defecto
    }
    }
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
    // Verificar si el Pastor está colisionando contra un enemigo
    if (hit.gameObject.CompareTag("Enemy"))
    {
        // El Jefe de Noche no se deja empujar.
        EnemyAI collidedEnemy = hit.gameObject.GetComponentInParent<EnemyAI>();
        if (collidedEnemy != null && collidedEnemy.IsBoss) return;

        // 1. Obtener el NavMeshAgent del Imp
        UnityEngine.AI.NavMeshAgent enemyAgent = hit.gameObject.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (enemyAgent != null && enemyAgent.enabled)
        {
            // 2. Calcular la dirección del empuje (del Pastor hacia el Imp) solo en XZ
            Vector3 pushDirection = hit.gameObject.transform.position - transform.position;
            pushDirection.y = 0f;
            pushDirection.Normalize();

            // 3. Aplicar desplazamiento al NavMeshAgent
            float pushForce = 2.5f; // Fuerza del empuje (puedes ajustarla)
            enemyAgent.Move(pushDirection * pushForce * Time.deltaTime);
        }
    }
    }
    
}