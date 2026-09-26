using UnityEngine;
using System;

public class HealthComponent : MonoBehaviour
{
    [Header("Fuente de Datos (Opcional)")]
    [SerializeField] private CharacterDataSO playerData;
    [SerializeField] private EnemyDataSO enemyData;

    [Header("Estado en Tiempo de Ejecución")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false;

    /// <summary>Vida máxima BASE leída del ScriptableObject, sin escalado de dificultad.</summary>
    private float baseMaxHealth = 100f;

    [Header("Animación de Impacto")]
    [Tooltip("Tiempo mínimo entre animaciones de Hit. Evita el bloqueo por impactos continuos (stunlock).")]
    [SerializeField] private float hitAnimationCooldown = 0.4f;
    [Tooltip("Si el enemigo está atacando, la animación de Hit no interrumpe su ataque.")]
    [SerializeField] private bool avoidInterruptingAttacks = true;

    [Header("Muerte")]
    [Tooltip("Desactívalo cuando el enemigo forme parte de un Object Pool: lo reciclará el sistema de pooling.")]
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 0.05f;

    [Header("Recompensas (Drop de XP)")]
    [SerializeField] private GameObject xpGemPrefab;
    private float xpReward = 15f;
    private float baseXpReward = 15f;

    private Animator animator;
    private EnemyAI enemyAI;
    private float lastHitAnimationTime = -999f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private static readonly int HitHash = Animator.StringToHash("Hit");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        // Solo los enemigos tienen EnemyAI; en el jugador queda null y la protección se ignora.
        enemyAI = GetComponent<EnemyAI>();

        if (playerData != null)
        {
            baseMaxHealth = playerData.maxHealth;
        }
        else if (enemyData != null)
        {
            baseMaxHealth = enemyData.maxHealth;
            baseXpReward = enemyData.xpReward;
            xpReward = baseXpReward;
        }

        Initialize(baseMaxHealth);
    }

    /// <summary>
    /// Inicializa la vida de esta instancia. Es también el punto de entrada al reciclar desde un pool.
    /// </summary>
    public void Initialize(float maxHealthValue, bool refillHealth = true)
    {
        // Cada vez que el enemigo se (re)activa, la recompensa de XP vuelve a su valor base
        // (evita que el multiplicador del jefe se acumule al reutilizar la instancia del pool).
        xpReward = baseXpReward;

        maxHealth = Mathf.Max(1f, maxHealthValue);

        if (refillHealth)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        isDead = false;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Aplica el multiplicador de dificultad sobre la vida BASE.
    /// Es idempotente: dos llamadas con el mismo multiplicador no acumulan escalado.
    /// </summary>
    public void ApplyHealthScaling(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.01f, multiplier);
        Initialize(baseMaxHealth * safeMultiplier);
    }

    public float BaseMaxHealth => baseMaxHealth;

    /// <summary>Multiplica la recompensa de XP (los jefes reparten más experiencia).</summary>
    public void MultiplyXpReward(float multiplier)
    {
        xpReward *= Mathf.Max(0.01f, multiplier);
    }
    
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // --- ANIMACIÓN DE IMPACTO (con protección anti-bloqueo) ---
        TryPlayHitAnimation();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Reproduce la animación de impacto respetando un cooldown y sin interrumpir un ataque en curso.
    /// Así el enemigo puede seguir persiguiendo y atacando aunque reciba daño continuo.
    /// </summary>
    private void TryPlayHitAnimation()
    {
        if (animator == null || currentHealth <= 0f) return;

        // No romper el ataque actual: era la causa de que los enemigos nunca llegaran a golpear.
        if (avoidInterruptingAttacks && enemyAI != null && enemyAI.IsAttacking) return;

        if (Time.time - lastHitAnimationTime < hitAnimationCooldown) return;

        lastHitAnimationTime = Time.time;
        animator.SetTrigger(HitHash);
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        DropXpGem();

        OnDeath?.Invoke();

        if (gameObject.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerGameOver();
            }

            return;
        }

        ReturnToPoolOrDestroy();
    }

    /// <summary>Suelta la gema de XP reciclándola desde el pool (sin Instantiate/Destroy).</summary>
    private void DropXpGem()
    {
        if (xpGemPrefab == null) return;

        Vector3 spawnPosition = new Vector3(transform.position.x, 0.5f, transform.position.z);
        ObjectPoolManager pool = ObjectPoolManager.Instance;

        GameObject gemObject = pool != null
            ? pool.Spawn(xpGemPrefab, spawnPosition, Quaternion.identity)
            : Instantiate(xpGemPrefab, spawnPosition, Quaternion.identity);

        if (gemObject == null) return;

        XPGem gem = gemObject.GetComponentInChildren<XPGem>();

        if (gem != null)
        {
            gem.SetXPValue(xpReward);
        }
    }

    /// <summary>
    /// Recicla el enemigo en el pool en lugar de destruirlo (cero GC durante el gameplay).
    /// Si no hay pool, se mantiene el comportamiento antiguo (Destroy con retardo).
    /// </summary>
    private void ReturnToPoolOrDestroy()
    {
        ObjectPoolManager pool = ObjectPoolManager.Instance;

        if (pool != null && pool.IsPooled(gameObject))
        {
            pool.Despawn(gameObject);
            return;
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
}