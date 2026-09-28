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

    [Header("Mitigación y vida extra (Fase 2)")]
    [Tooltip("Puntos planos que se restan a CADA golpe recibido. Lo escribe UpgradeManager desde " +
             "RunStats.Armor. En 0 (valor por defecto) el daño recibido no cambia.")]
    [SerializeField] private float flatDamageReduction = 0f;
    [Tooltip("Al subir la vida máxima por una mejora, ¿se rellena también la vida? " +
             "Activado = la mejora de vida máxima cura de inmediato (habitual en roguelites).")]
    [SerializeField] private bool refillOnMaxHealthIncrease = true;

    [Header("Recompensas (Drop de XP)")]
    [SerializeField] private GameObject xpGemPrefab;
    private float xpReward = 15f;
    private float baseXpReward = 15f;

    [Header("Juice (Números de daño)")]
    [Tooltip("Prefab con DamageNumber (TextMeshPro en World Space). Si está vacío no se muestran números.")]
    [SerializeField] private GameObject damageNumberPrefab;
    [Tooltip("Altura sobre el objeto donde aparece el número flotante.")]
    [SerializeField] private float damageNumberHeight = 1.5f;

    [Header("Juice (Micro-pausa de impacto)")]
    [Tooltip("Segundos reales de micro-pausa al CERMAR UN ENEMIGO. Una muerte merece más peso que un golpe. 0 la desactiva.")]
    [SerializeField] private float hitStopOnEnemyDeath = 0.04f;
    [Tooltip("Segundos reales de micro-pausa cuando el golpe LO RECIBE el jugador. Un poco más larga: " +
             "es el feedback que el jugador más nota. 0 la desactiva.")]
    [SerializeField] private float hitStopOnPlayerHit = 0.06f;

    [Header("Audio de Impacto (daño recibido)")]
    [Tooltip("Sonido al recibir daño (ImapactEnemy.mp3). En el JUGADOR se reproduce en 2D y con " +
             "prioridad: si se deja vacío se usa el impacto genérico del AudioManager.")]
    [SerializeField] private AudioClip damageSFX;
    [Range(0f, 1f)]
    [Tooltip("Volumen del SFX de daño.")]
    [SerializeField] private float damageSfxVolume = 0.6f;
    [Range(0f, 0.2f)]
    [Tooltip("Variación de tono (±) por impacto: evita la fatiga auditiva al golpear muchas veces.")]
    [SerializeField] private float damageSfxPitchVariation = 0.08f;
    [Tooltip("Tiempo mínimo (s) entre dos SFX de daño de ESTA instancia (0.08s). Evita que varios " +
             "impactos a la vez (p. ej. un golpe en área sobre la horda) solapen el audio.")]
    [SerializeField] private float damageSfxCooldown = 0.08f;
    [Range(0f, 1f)]
    [Tooltip("Volumen del golpe recibido por el JUGADOR (2D, sin atenuación: se oye siempre).")]
    [SerializeField] private float playerDamageSfxVolume = 0.85f;

    [Header("Audio de Daño del Jugador (variaciones)")]
    [Tooltip("Variaciones del golpe recibido por el JUGADOR (SFX_DamagePlayer_01/02). En cada impacto " +
             "se elige una al azar: si el array está vacío se usa 'Damage Sfx' y, si tampoco hay, " +
             "la variación aleatoria del catálogo del AudioManager.")]
    [SerializeField] private AudioClip[] playerDamageSFX;

    private Animator animator;
    private EnemyAI enemyAI;
    private float lastHitAnimationTime = -999f;

    /// <summary>Última vez que ESTA instancia reprodujo su SFX de daño (cooldown anti-saturación).</summary>
    private float lastDamageSfxTime = -999f;

    /// <summary>
    /// Frame en el que ESTA instancia mostró su último número de daño. Protección anti-duplicado:
    /// si un mismo golpe invoca TakeDamage() dos veces dentro del mismo frame (Animation Event
    /// repetido, evento + respaldo por tiempo o doble fuente de ataque), solo se instancia UN
    /// texto flotante. Es un campo por instancia: varios enemigos golpeados en el mismo frame
    /// (golpe en área) cada uno muestra su propio número.
    /// </summary>
    private int lastDamageNumberFrame = -1;

    /// <summary>
    /// True si quien recibe el daño es el jugador: su golpe se reproduce en 2D, sin atenuación y sin
    /// cooldown, para que nunca se pierda en la mezcla. Se resuelve una sola vez en Awake.
    /// </summary>
    private bool isPlayerTarget;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    /// <summary>True si este HealthComponent pertenece al JUGADOR (se resuelve una sola vez en Awake).</summary>
    public bool IsPlayerTarget => isPlayerTarget;

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

        // ¿Quién recibe el daño? Determina cómo se reproduce el SFX:
        //  - Jugador  -> 2D prioritario (siempre audible, sin atenuación ni cooldown).
        //  - Enemigos -> 3D posicional con cooldown por instancia (anti-saturación de horda).
        // Se comprueba por etiqueta, por componente y por datos de personaje: así funciona aunque
        // falte cualquiera de los tres (la etiqueta puede no estar guardada en la escena).
        isPlayerTarget = gameObject.CompareTag("Player")
            || GetComponent<PlayerController>() != null
            || playerData != null;

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

        // Instancia (re)activada: el SFX de daño puede volver a sonar de inmediato y el primer
        // golpe tras el reciclaje siempre puede mostrar su número (aunque coincida con el frame
        // en el que la instancia anterior fue devuelta al pool).
        lastDamageSfxTime = -999f;
        lastDamageNumberFrame = -1;

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

        currentHealth -= Mathf.Max(0f, amount - flatDamageReduction);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // --- ANIMACIÓN DE IMPACTO (con protección anti-bloqueo) ---
        TryPlayHitAnimation();

        // --- AUDIO DE IMPACTO (cooldown por instancia + variación de tono) ---
        TryPlayDamageSfx();

        // --- JUICE: número de daño flotante sobre la posición del impacto ---
        SpawnDamageNumber(amount);

        // --- JUICE: micro-pausa de impacto ---
        // Solo cuando el golpe realmente cuenta (una muerte) o cuando lo recibe el jugador.
        // En un enemigo que sobrevive no se pide: el pulso de área ya pide la suya en
        // PlayerAttack, y sumarlas aquí convertiría el impacto en un parpadeo.
        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        if (isPlayerTarget)
        {
            GameStateController.Instance?.RequestHitStop(hitStopOnPlayerHit);
        }
    }

    /// <summary>
    /// Muestra el número de daño flotante reciclándolo desde el pool (sin Instantiate/Destroy).
    /// No hace nada si no hay prefab asignado: el combate funciona igual sin juice.
    /// </summary>
    private void SpawnDamageNumber(float amount)
    {
        if (damageNumberPrefab == null) return;

        // Protección por frame: un mismo golpe no puede instanciar dos números en el mismo frame.
        // Si TakeDamage() se invocara dos veces de forma simultánea (Animation Event duplicado,
        // evento + respaldo del enemigo, multi-collider sin deduplicar, etc.), la segunda llamada
        // sale aquí sin crear texto. Al ser por instancia, el daño en área a varios enemigos
        // sigue mostrando un número por cada uno.
        if (Time.frameCount == lastDamageNumberFrame) return;

        lastDamageNumberFrame = Time.frameCount;

        Vector3 spawnPosition = transform.position + Vector3.up * damageNumberHeight;
        ObjectPoolManager pool = ObjectPoolManager.Instance;

        GameObject numberObject = pool != null
            ? pool.Spawn(damageNumberPrefab, spawnPosition, Quaternion.identity)
            : Instantiate(damageNumberPrefab, spawnPosition, Quaternion.identity);

        if (numberObject == null) return;

        DamageNumber damageNumber = numberObject.GetComponentInChildren<DamageNumber>();

        if (damageNumber != null)
        {
            damageNumber.Show(amount);
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

    /// <summary>
    /// Reproduce el SFX de daño con una variación ligera de tono.
    /// En el JUGADOR va en 2D y sin cooldown (feedback crítico); en los enemigos el cooldown
    /// (0.08s) evita que varios impactos del mismo enemigo en el mismo frame, o un
    /// golpe en área sobre la horda, solapen el mismo clip y saturen la mezcla.
    /// Se usa Time.unscaledTime para que el audio no dependa del Time.timeScale (pausa/nivel).
    /// </summary>
    private void TryPlayDamageSfx()
    {
        AudioManager audio = AudioManager.Instance;

        if (audio == null) return;

        float now = Time.unscaledTime;

        // Cooldown anti-saturación: se aplica SOLO a los enemigos (horda). El jugador
        // recibe pocos golpes y su feedback nunca debe perderse en la mezcla.
        if (!isPlayerTarget && now - lastDamageSfxTime < damageSfxCooldown) return;

        lastDamageSfxTime = now;

        if (isPlayerTarget)
        {
            // 2D y prioritario (sin filtros anti-saturación): el golpe recibido se oye siempre,
            // esté donde esté la cámara y aunque los enemigos usen el mismo clip a la vez.
            audio.PlayHitOnPlayer(ResolvePlayerDamageClip(audio), playerDamageSfxVolume, damageSfxPitchVariation);
            return;
        }

        if (damageSFX == null) return;

        audio.PlaySFXAtPosition(damageSFX, transform.position, damageSfxVolume, damageSfxPitchVariation);
    }

    /// <summary>
    /// Clip del golpe recibido por el jugador, por orden de prioridad:
    /// 1) una variación aleatoria de 'playerDamageSFX' (SFX_DamagePlayer_01/02),
    /// 2) el clip único 'damageSFX' del Inspector,
    /// 3) la variación aleatoria del catálogo del AudioManager,
    /// 4) el impacto sintetizado del propio manager (audible sin depender de ningún asset).
    /// </summary>
    private AudioClip ResolvePlayerDamageClip(AudioManager audio)
    {
        AudioClip variation = GetRandomPlayerDamageClip();

        if (variation != null) return variation;
        if (damageSFX != null) return damageSFX;

        AudioClip catalogClip = audio.GetPlayerDamageClip();

        return catalogClip != null ? catalogClip : audio.GetFallbackImpactClip();
    }

    /// <summary>
    /// Variación aleatoria del array de golpes del jugador. Empieza en un índice al azar y recorre el
    /// array desde ahí, así se ignoran los huecos vacíos sin salir siempre con el mismo clip.
    /// </summary>
    private AudioClip GetRandomPlayerDamageClip()
    {
        if (playerDamageSFX == null || playerDamageSFX.Length == 0) return null;

        // UnityEngine.Random explícito: este archivo importa System, donde también existe Random.
        int startIndex = UnityEngine.Random.Range(0, playerDamageSFX.Length);

        for (int i = 0; i < playerDamageSFX.Length; i++)
        {
            AudioClip candidate = playerDamageSFX[(startIndex + i) % playerDamageSFX.Length];

            if (candidate != null) return candidate;
        }

        return null;
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // ==================================================================
    // Fase 2: mitigación y vida máxima adicional
    // ==================================================================

    /// <summary>
    /// Mitigación plana de daño. La escribe <see cref="UpgradeManager"/> después de cada
    /// mejora; con 0 (valor por defecto) el daño recibido es exactamente el de antes.
    ///
    /// Nota: NO se usa <see cref="ApplyHealthScaling"/>, que reinicializa la vida y borraría
    /// el progreso del jugador en plena partida.
    /// </summary>
    public void SetFlatDamageReduction(float value)
    {
        flatDamageReduction = Mathf.Max(0f, value);
    }

    /// <summary>Mitigación plana actual, por si la UI quiere mostrarla.</summary>
    public float FlatDamageReduction => flatDamageReduction;

    /// <summary>
    /// Ajusta la vida máxima a <c>baseMaxHealth + bonus</c>. Es idempotente: se puede llamar en
    /// cada mejora porque siempre parte de la vida base, nunca del valor ya modificado.
    ///
    /// Se conserva la proporción de vida actual salvo que <c>refillOnMaxHealthIncrease</c> esté
    /// activo, en cuyo caso la mejora también cura.
    /// </summary>
    public void ApplyMaxHealthBonus(float bonus)
    {
        float newMaxHealth = Mathf.Max(1f, baseMaxHealth + Mathf.Max(0f, bonus));

        if (Mathf.Approximately(newMaxHealth, maxHealth))
        {
            return;
        }

        if (refillOnMaxHealthIncrease && newMaxHealth > maxHealth)
        {
            maxHealth = newMaxHealth;
            currentHealth = newMaxHealth;
        }
        else
        {
            float ratio = maxHealth > 0f ? currentHealth / maxHealth : 1f;
            maxHealth = newMaxHealth;
            currentHealth = Mathf.Clamp(ratio * newMaxHealth, 0f, newMaxHealth);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Micro-pausa de cierre: el último golpe de un enemigo es el que más peso necesita.
        // Va DESPUÉS de marcar isDead para que un golpe letal nunca se procese dos veces.
        if (!isPlayerTarget)
        {
            GameStateController.Instance?.RequestHitStop(hitStopOnEnemyDeath);
        }

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