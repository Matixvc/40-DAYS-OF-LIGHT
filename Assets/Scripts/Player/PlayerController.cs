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

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;

    // Control de Sprint
    private bool isSprinting;
    private bool isSprintOnCooldown;
    private float sprintTimer;
    private float cooldownTimer;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    public bool IsSprintOnCooldown => isSprintOnCooldown;
    
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