using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Data & Configuration")]
    [SerializeField] private CharacterDataSO stats;

    [Header("Ground & Gravity")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Audio")]
    [SerializeField] private AudioClip playerAttackSFX;
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
    }

    private void Update()
    {
        HandleSprintTimers();
        HandleMovement();
    }

    private void HandleSprintTimers()
    {
        if (stats == null) return;

        // Si se está corriendo, agotar el tiempo de sprint
        if (isSprinting)
        {
            sprintTimer -= Time.deltaTime;
            if (sprintTimer <= 0f)
            {
                // Se acabó el sprint: iniciar cooldown
                isSprinting = false;
                isSprintOnCooldown = true;
                cooldownTimer = stats.sprintCooldown;
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

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // Detección de Sprint con Shift Izquierdo, Espacio O Clic Izquierdo del ratón
        bool shiftPressed = Input.GetKey(KeyCode.LeftShift) || 
                           Input.GetKey(KeyCode.Space) || 
                           Input.GetMouseButton(0);
        
        if (shiftPressed && direction.magnitude >= 0.1f && !isSprintOnCooldown && !isSprinting)
        {
            isSprinting = true;
            sprintTimer = stats.sprintDuration;
            Debug.Log("<color=yellow>[Sprint] ¡Corriendo!</color>");
        }
        else if (!shiftPressed && isSprinting)
        {
            // Calcular cuánto tiempo se usó realmente del sprint (en porcentaje de 0 a 1)
            float timeUsed = stats.sprintDuration - sprintTimer;
            float usedPercent = Mathf.Clamp01(timeUsed / stats.sprintDuration);

            // Aplicar un cooldown proporcional al tiempo gastado
            isSprinting = false;
            isSprintOnCooldown = true;
            cooldownTimer = stats.sprintCooldown * usedPercent;
        }

        // 1. Calcular velocidad actual
        float baseSpeed = stats != null ? stats.moveSpeed : 5f;
        float sprintMult = stats != null ? stats.sprintMultiplier : 1.6f;
        float currentSpeed = isSprinting ? baseSpeed * sprintMult : baseSpeed;
        float currentRotSpeed = stats != null ? stats.rotationSpeed : 10f;

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
    float speedPercent = direction.magnitude * (isSprinting ? stats.sprintMultiplier : 1f);
    animator.SetFloat(SpeedHash, speedPercent, 0.1f, Time.deltaTime);
    }
    }
    // Propiedades para que la UI lea el estado del Sprint
    public float SprintPercent
    {
    get
    {
        if (stats == null) return 1f;
        if (isSprinting) return sprintTimer / stats.sprintDuration;
        if (isSprintOnCooldown) return 1f - (cooldownTimer / stats.sprintCooldown);
        return 1f; // Energía llena por defecto
    }
    }
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
    // Verificar si el Pastor está colisionando contra un enemigo
    if (hit.gameObject.CompareTag("Enemy"))
    {
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