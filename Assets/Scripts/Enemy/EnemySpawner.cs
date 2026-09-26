using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform playerTransform;

    [Header("Parámetros Iniciales de Spawn")]
    [Tooltip("Intervalo entre spawns en el Día 1, en segundos.")]
    [SerializeField] private float initialSpawnInterval = 1.8f;
    [Tooltip("Intervalo mínimo alcanzable: límite de densidad de spawn.")]
    [SerializeField] private float minSpawnInterval = 0.5f;
    [SerializeField] private float minSpawnDistance = 10f;     // Distancia mínima al jugador
    [SerializeField] private float maxSpawnDistance = 15f;     // Distancia máxima al jugador
    [Tooltip("Enemigos simultáneos permitidos en el Día 1.")]
    [SerializeField] private int initialMaxEnemies = 22;
    [Tooltip("Tope absoluto de enemigos simultáneos, por rendimiento.")]
    [SerializeField] private int absoluteMaxEnemies = 100;

    [Header("Ritmo de Spawn por Ronda")]
    [Tooltip("Cuánto se acorta el intervalo entre spawns por cada día.")]
    [SerializeField] private float intervalDecreaseRate = 0.045f;
    [Tooltip("Cuántos enemigos simultáneos más se permiten por cada día.")]
    [SerializeField] private int maxEnemiesIncreasePerLevel = 3;

    [Header("Spawn en Ráfaga")]
    [Tooltip("Enemigos generados por ciclo al inicio de la partida.")]
    [SerializeField] private int earlyBurstSize = 1;
    [Tooltip("Ronda a partir de la cual las ráfagas crecen.")]
    [SerializeField] private int burstRoundsStart = 10;
    [Tooltip("Enemigos generados por ciclo en rondas avanzadas (respetando el límite de enemigos).")]
    [SerializeField] private int lateBurstSize = 3;

    [Header("Depuración")]
    [SerializeField] private bool logSpawnScaling = true;

    private float currentSpawnInterval;
    private int currentMaxEnemies;
    private float nextSpawnTime;
    private int currentRound = 1;
    private bool spawningEnabled = true;
    private EnemyScalingProfile scalingProfile = EnemyScalingProfile.One;

    // Conteo de enemigos vivos sin Find: se incrementa al spawnear y se decrementa al morir (cero GC).
    private int aliveEnemiesCount;
    private readonly System.Collections.Generic.HashSet<HealthComponent> trackedEnemies = new System.Collections.Generic.HashSet<HealthComponent>();

    private void Start()
    {
        // Ritmo base: el RunDirector lo reajusta en cuanto arranca la primera ronda.
        currentSpawnInterval = initialSpawnInterval;
        currentMaxEnemies = initialMaxEnemies;
        nextSpawnTime = Time.time + currentSpawnInterval;

        // Búsqueda automática del jugador si no se asignó en el Inspector
        if (playerTransform == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private void Update()
    {
        if (!spawningEnabled) return;
        if (playerTransform == null || enemyPrefab == null) return;

        // El spawn solo avanza durante la partida; el tiempo ya está congelado en pausa, nivel y final.
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null && !stateController.IsPlaying) return;

        // Spawn continuo según el intervalo actual de la ronda
        if (Time.time >= nextSpawnTime)
        {
            // Cero GC: usa el pool o el contador interno en lugar de FindGameObjectsWithTag.
            int aliveEnemies = GetAliveEnemiesCount();
            int freeSlots = currentMaxEnemies - aliveEnemies;

            // Spawn en ráfaga: en rondas avanzadas se generan varios enemigos por ciclo si hay hueco.
            int burstSize = currentRound >= burstRoundsStart ? lateBurstSize : earlyBurstSize;
            int spawnAttempts = Mathf.Clamp(Mathf.Min(freeSlots, burstSize), 0, Mathf.Max(0, burstSize));

            for (int i = 0; i < spawnAttempts; i++)
            {
                if (!TrySpawnEnemyOnNavMesh())
                {
                    break; // Sin posición válida sobre el NavMesh en este ciclo
                }
            }

            nextSpawnTime = Time.time + currentSpawnInterval;
        }
    }

    /// <summary>
    /// Busca una posición válida sobre el NavMesh alrededor del jugador y saca el enemigo del pool.
    /// Devuelve null si no encontró posición válida o el pool alcanzó su límite.
    /// </summary>
    private GameObject InstantiateEnemyNearPlayer()
    {
        for (int i = 0; i < 5; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 randomPoint = playerTransform.position + new Vector3(randomCircle.x, 0f, randomCircle.y) * distance;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                return SpawnFromPoolOrInstantiate(enemyPrefab, hit.position);
            }
        }

        return null;
    }

    /// <summary>
    /// Recicla la instancia desde el ObjectPoolManager (cero GC durante el gameplay).
    /// Si no hay pool en la escena, usa Instantiate como respaldo para no romper el flujo.
    /// </summary>
    private static GameObject SpawnFromPoolOrInstantiate(GameObject prefab, Vector3 position)
    {
        ObjectPoolManager pool = ObjectPoolManager.Instance;

        if (pool != null)
        {
            return pool.Spawn(prefab, position, Quaternion.identity);
        }

        return Instantiate(prefab, position, Quaternion.identity);
    }

    /// <summary>Intenta crear un enemigo. Devuelve false si no encontró posición válida sobre el NavMesh.</summary>
    private bool TrySpawnEnemyOnNavMesh()
    {
        GameObject newEnemy = InstantiateEnemyNearPlayer();

        if (newEnemy == null) return false;

        ConfigureSpawnedEnemy(newEnemy);
        TrackSpawnedEnemy(newEnemy);
        return true;
    }

    /// <summary>Enemigos vivos actuales: prefiere el pool, usa el contador si no hay pool.</summary>
    private int GetAliveEnemiesCount()
    {
        ObjectPoolManager pool = ObjectPoolManager.Instance;

        if (pool != null && enemyPrefab != null)
        {
            return pool.GetActiveCount(enemyPrefab);
        }

        // Sin pool: limpia referencias muertas y devuelve el conteo rastreado.
        trackedEnemies.RemoveWhere(h => h == null);
        aliveEnemiesCount = trackedEnemies.Count;
        return aliveEnemiesCount;
    }

    /// <summary>Registra un enemigo nuevo para descontarlo al morir (sin Find).</summary>
    private void TrackSpawnedEnemy(GameObject spawnedEnemy)
    {
        HealthComponent health = spawnedEnemy != null ? spawnedEnemy.GetComponentInChildren<HealthComponent>(true) : null;

        if (health == null) return;

        if (trackedEnemies.Add(health))
        {
            aliveEnemiesCount++;
        }

        health.OnDeath -= HandleTrackedEnemyDeath;
        health.OnDeath += HandleTrackedEnemyDeath;
    }

    private void HandleTrackedEnemyDeath()
    {
        aliveEnemiesCount = Mathf.Max(0, aliveEnemiesCount - 1);
    }

    private void OnDestroy()
    {
        foreach (HealthComponent health in trackedEnemies)
        {
            if (health != null)
            {
                health.OnDeath -= HandleTrackedEnemyDeath;
            }
        }

        trackedEnemies.Clear();
        aliveEnemiesCount = 0;
    }

    // ======================================================================
    // API PARA EL RUNDIRECTOR
    // ======================================================================

    public bool SpawningEnabled => spawningEnabled;
    public int CurrentRound => currentRound;
    public float CurrentSpawnInterval => currentSpawnInterval;
    public int CurrentMaxEnemies => currentMaxEnemies;

    /// <summary>
    /// Ajusta el ritmo de spawn a la ronda indicada y guarda el escalado de estadísticas
    /// que se aplicará a cada enemigo nuevo de esa ronda.
    /// </summary>
    public void ApplyRoundSettings(
        int roundNumber,
        EnemyScalingProfile profile,
        float intervalMultiplier = 1f,
        float maxEnemiesMultiplier = 1f)
    {
        currentRound = Mathf.Max(1, roundNumber);
        scalingProfile = profile;

        int steps = currentRound - 1;
        float baseInterval = initialSpawnInterval - (steps * intervalDecreaseRate);
        float baseMaxEnemies = initialMaxEnemies + (steps * maxEnemiesIncreasePerLevel);

        // Los multiplicadores permiten bajar el ritmo durante las Noches (con jefe)
        // sin llegar a detener nunca el spawn de enemigos comunes.
        currentSpawnInterval = Mathf.Max(minSpawnInterval, baseInterval * Mathf.Max(0.1f, intervalMultiplier));
        currentMaxEnemies = Mathf.Clamp(
            Mathf.RoundToInt(baseMaxEnemies * Mathf.Max(0.1f, maxEnemiesMultiplier)),
            1,
            absoluteMaxEnemies);

        if (logSpawnScaling)
        {
            Debug.Log(
                $"<color=orange>[EnemySpawner] Ronda {currentRound} configurada | Intervalo: {currentSpawnInterval:0.00}s | " +
                $"Máx enemigos: {currentMaxEnemies} | Vida x{profile.healthMultiplier:0.00} | " +
                $"Daño x{profile.damageMultiplier:0.00} | Velocidad x{profile.speedMultiplier:0.00}</color>",
                this);
        }
    }

    /// <summary>Activa o detiene el spawn regular (las noches con jefe lo detienen).</summary>
    public void SetSpawningEnabled(bool value)
    {
        spawningEnabled = value;

        if (value)
        {
            // Evita una ráfaga de spawns al reanudar.
            nextSpawnTime = Time.time + currentSpawnInterval;
        }

        if (logSpawnScaling)
        {
            Debug.Log($"<color=orange>[EnemySpawner] Spawn {(value ? "ACTIVADO" : "DETENIDO")} (ronda {currentRound}).</color>", this);
        }
    }

    /// <summary>
    /// Genera un Jefe de Noche: misma base que el enemigo normal pero escalado en
    /// estadísticas, tamaño y recompensa. No modifica ningún ScriptableObject.
    /// </summary>
    public GameObject SpawnBoss(
        EnemyScalingProfile profile,
        float scaleMultiplier,
        float xpMultiplier,
        string displayName)
    {
        if (enemyPrefab == null || playerTransform == null)
        {
            Debug.LogError("[EnemySpawner] No se puede generar el jefe: falta el prefab o el jugador.", this);
            return null;
        }

        GameObject boss = InstantiateEnemyNearPlayer();

        if (boss == null)
        {
            Debug.LogWarning("[EnemySpawner] No se encontró una posición válida sobre el NavMesh para el jefe.", this);
            return null;
        }

        boss.name = string.IsNullOrWhiteSpace(displayName) ? $"{enemyPrefab.name}_Boss" : displayName;

        // Escalado visual
        float safeScale = Mathf.Max(1f, scaleMultiplier);
        boss.transform.localScale *= safeScale;

        HealthComponent bossHealth = boss.GetComponentInChildren<HealthComponent>(true);
        EnemyAI bossAI = boss.GetComponentInChildren<EnemyAI>(true);

        if (bossHealth != null)
        {
            bossHealth.ApplyHealthScaling(profile.healthMultiplier);
            bossHealth.MultiplyXpReward(xpMultiplier);
        }
        else
        {
            Debug.LogError("[EnemySpawner] El prefab del jefe no tiene HealthComponent.", boss);
        }

        if (bossAI != null)
        {
            bossAI.SetTarget(playerTransform);
            bossAI.ApplySpawnScaling(profile.damageMultiplier, profile.speedMultiplier);
            bossAI.MarkAsBoss();
        }
        else
        {
            Debug.LogError("[EnemySpawner] El prefab del jefe no tiene EnemyAI.", boss);
        }

        if (logSpawnScaling)
        {
            float bossHealthValue = bossHealth != null ? bossHealth.MaxHealth : 0f;
            float bossDamageValue = bossAI != null ? bossAI.CurrentDamage : 0f;

            Debug.Log(
                $"<color=magenta>[EnemySpawner] JEFE '{boss.name}' | Vida {bossHealthValue:0} (x{profile.healthMultiplier:0.00}) | " +
                $"Daño {bossDamageValue:0.0} (x{profile.damageMultiplier:0.00}) | Escala x{safeScale:0.00}</color>",
                boss);
        }

        return boss;
    }

    /// <summary>
    /// Aplica el escalado de la ronda actual al enemigo recién creado.
    /// No modifica los ScriptableObjects: escribe en el runtime de la instancia (compatible con pooling).
    /// </summary>
    private void ConfigureSpawnedEnemy(GameObject spawnedEnemy)
    {
        float healthMultiplier = scalingProfile.healthMultiplier;
        float damageMultiplier = scalingProfile.damageMultiplier;
        float speedMultiplier = scalingProfile.speedMultiplier;

        HealthComponent enemyHealth = spawnedEnemy.GetComponentInChildren<HealthComponent>(true);
        EnemyAI enemyAI = spawnedEnemy.GetComponentInChildren<EnemyAI>(true);

        if (enemyHealth != null)
        {
            // Escala desde la vida BASE del enemigo y la deja llena.
            enemyHealth.ApplyHealthScaling(healthMultiplier);
        }
        else
        {
            Debug.LogError("[EnemySpawner] El prefab de enemigo no tiene HealthComponent.", spawnedEnemy);
        }

        if (enemyAI != null)
        {
            // Asignar el objetivo evita un FindGameObjectWithTag por enemigo.
            enemyAI.SetTarget(playerTransform);
            enemyAI.ApplySpawnScaling(damageMultiplier, speedMultiplier);
        }
        else
        {
            Debug.LogError("[EnemySpawner] El prefab de enemigo no tiene EnemyAI.", spawnedEnemy);
        }

        if (logSpawnScaling)
        {
            float healthValue = enemyHealth != null ? enemyHealth.MaxHealth : 0f;
            float damageValue = enemyAI != null ? enemyAI.CurrentDamage : 0f;
            float speedValue = enemyAI != null ? enemyAI.CurrentMoveSpeed : 0f;

            Debug.Log(
                $"<color=orange>[EnemySpawner] Spawn escalado (ronda {currentRound}) | " +
                $"Vida x{healthMultiplier:0.00} = {healthValue:0.0} | " +
                $"Daño x{damageMultiplier:0.00} = {damageValue:0.0} | " +
                $"Velocidad x{speedMultiplier:0.00} = {speedValue:0.00}</color>",
                spawnedEnemy);
        }
    }

    /// <summary>
    /// Aplica el ritmo de spawn recomendado para la curva de 40 días
    /// (más densidad a medida que avanza la partida, sin saturar el rendimiento).
    /// Útil porque los campos ya serializados en la escena conservan los valores antiguos.
    /// </summary>
    [ContextMenu("Aplicar ritmo de spawn recomendado")]
    private void ApplyRecommendedPacing()
    {
        initialSpawnInterval = 1.8f;
        minSpawnInterval = 0.4f;
        initialMaxEnemies = 22;
        absoluteMaxEnemies = 100;
        intervalDecreaseRate = 0.045f;
        maxEnemiesIncreasePerLevel = 3;
        earlyBurstSize = 1;
        burstRoundsStart = 10;
        lateBurstSize = 3;

        if (logSpawnScaling)
        {
            Debug.Log(
                "[EnemySpawner] Ritmo recomendado aplicado: 1.8s → 0.4s, 22 → 100 enemigos y ráfagas de hasta 3 por ciclo. " +
                "Guarda la escena (Ctrl+S) para conservarlo.",
                this);
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;

        // Dibuja los rangos de aparición alrededor del jugador en la vista de Escena
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(playerTransform.position, minSpawnDistance);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(playerTransform.position, maxSpawnDistance);
    }
}