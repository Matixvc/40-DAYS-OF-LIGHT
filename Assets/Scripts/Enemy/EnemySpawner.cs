using System;
using UnityEngine;
using UnityEngine.AI;

// System.Random también existe: el alias resuelve la ambigüedad CS0104 y deja
// las llamadas como Random.Range(), que es la forma que usa el resto del proyecto.
using Random = UnityEngine.Random;

/// <summary>
/// Fase de la ola dentro de una ronda. Sustituye al ritmo plano: una ronda ya no es
/// "enemigos cada X segundos" de principio a fin, sino una curva de tensión con descansos.
///
/// El ritmo por sí solo no genera tensión: lo que la genera es el CONTRASTE entre un pico
/// de presión y un respiro. Una horda constante e infinita solo produce cansancio; el
/// "Surge" seguido del "Breather" es lo que produce alivio... y, por tanto, ganas de volver.
/// </summary>
public enum SpawnPhase
{
    Calm,      // Entrada suave: el jugador-orienta, todavía sin presión.
    Tension,   // La horda se acerca: aviso de que viene algo peor.
    Surge,     // Pico de presión: ráfagas grandes, cadencia rápida.
    Breather   // Descanso: la horda se adelgaza sola si el jugador mantiene la distancia.
}

/// <summary>
/// Un tramo de la ronda con su propio ritmo. Todos los valores son MULTIPLICADORES sobre
/// el intervalo y el tope de enemigos que ya calculó <see cref="EnemySpawner.ApplyRoundSettings"/>,
/// así que la curva de 40 días sigue mandando: las fases solo dan forma dentro de cada ronda.
/// </summary>
[Serializable]
public struct SpawnWave
{
    [Tooltip("Fase que representa este tramo.")]
    public SpawnPhase phase;

    [Tooltip("Multiplicador del intervalo de spawn. >1 = más lento, <1 = más rápido.")]
    [Range(0.1f, 3f)] public float intervalMultiplier;

    [Tooltip("Enemigos generados por ciclo en este tramo.")]
    [Min(1)] public int burstSize;

    [Tooltip("Multiplicador del tope de enemigos simultáneos. El 'Breather' lo baja para que la horda se limpie sola.")]
    [Range(0.3f, 1f)] public float maxEnemiesFactor;

    [Tooltip("Inicio del tramo, en fracción de la ronda (0 = 0%, 1 = 100%).")]
    [Range(0f, 1f)] public float startAt;

    [Tooltip("Fin del tramo, en fracción de la ronda.")]
    [Range(0f, 1f)] public float endAt;
}

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

    [Header("Curva de la Ola (fases de tensión y descanso)")]
    [Tooltip("Activa la máquina de fases. Desactivado, la ronda usa el ritmo plano heredado.")]
    [SerializeField] private bool useWavePhases = true;

    [Tooltip("Tramos de la ronda, en orden. El primero empieza en 0 y el último termina en 1.\n" +
             "Calm: entrada suave | Tension: aviso | Surge: pico de presión | Breather: descanso.")]
    [SerializeField] private SpawnWave[] wavePhases = DefaultWaves();

    [Header("Depuración")]
    [SerializeField] private bool logSpawnScaling = true;

    /// <summary>Reloj propio de la ronda: se reinicia en cada <see cref="ApplyRoundSettings"/>.</summary>
    private float roundElapsed;

    /// <summary>Duración de la ronda en curso, la pasa el RunDirector. 0 = ritmo plano heredado.</summary>
    private float roundDuration;

    private SpawnPhase currentPhase = SpawnPhase.Calm;
    private bool hasEvaluatedPhase;

    /// <summary>Se lanza al cambiar de fase dentro de una ronda. El HUD o el audio pueden reaccionar.</summary>
    public event Action<SpawnPhase> OnPhaseChanged;

    public SpawnPhase CurrentPhase => currentPhase;

    /// <summary>Progreso de la ronda en curso (0..1). 0 si la duración aún no es conocida.</summary>
    public float RoundProgress => roundDuration > 0.01f ? Mathf.Clamp01(roundElapsed / roundDuration) : 0f;

    /// <summary>Tramos por defecto: la curva de tensión de cuatro tiempos del diseño original.</summary>
    private static SpawnWave[] DefaultWaves()
    {
        return new[]
        {
            new SpawnWave { phase = SpawnPhase.Calm,     intervalMultiplier = 1.00f, burstSize = 1, maxEnemiesFactor = 0.70f, startAt = 0.00f, endAt = 0.20f },
            new SpawnWave { phase = SpawnPhase.Tension,  intervalMultiplier = 0.65f, burstSize = 2, maxEnemiesFactor = 0.90f, startAt = 0.20f, endAt = 0.55f },
            new SpawnWave { phase = SpawnPhase.Surge,    intervalMultiplier = 0.35f, burstSize = 3, maxEnemiesFactor = 1.00f, startAt = 0.55f, endAt = 0.80f },
            new SpawnWave { phase = SpawnPhase.Breather, intervalMultiplier = 1.80f, burstSize = 1, maxEnemiesFactor = 0.55f, startAt = 0.80f, endAt = 1.00f },
        };
    }

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

        // Reinicio limpio: si este spawner sobrevive a una recarga o reactivación, nunca debe
        // heredar el flag 'spawningEnabled = false' del GameOver anterior.
        spawningEnabled = true;
        currentRound = 1;

        // El reloj de la ronda arranca limpio. Con roundDuration = 0 el spawner usa el
        // ritmo plano heredado hasta que el RunDirector pase la duración real.
        roundElapsed = 0f;
        roundDuration = 0f;
        currentPhase = SpawnPhase.Calm;
        hasEvaluatedPhase = false;

        // Limpieza de listas residuales (entradas muertas de una partida anterior).
        trackedEnemies.RemoveWhere(h => h == null);
        aliveEnemiesCount = trackedEnemies.Count;

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

    private void OnEnable()
    {
        // Reasegura el arranque del spawn al activarse el objeto: el flag nunca puede quedar
        // en false por una partida anterior (el RunDirector lo volverá a ajustar si hace falta).
        spawningEnabled = true;
    }

    private void Update()
    {
        if (!spawningEnabled) return;
        if (playerTransform == null || enemyPrefab == null) return;

        // El spawn solo avanza durante la partida; el tiempo ya está congelado en pausa, nivel y final.
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null && !stateController.IsPlaying) return;

        // El reloj de la ronda avanza con tiempo ESCALADO: la micro-pausa de impacto (0.04s)
        // no debe retrasar la curva de la ola, solo el movimiento de los enemigos.
        if (roundDuration > 0.01f)
        {
            roundElapsed += Time.deltaTime;
        }

        SpawnWave wave = EvaluateCurrentWave();

        if (Time.time >= nextSpawnTime)
        {
            // Cero GC: usa el pool o el contador interno en lugar de FindGameObjectsWithTag.
            int aliveEnemies = GetAliveEnemiesCount();

            // El tope de la ronda se modula por fase: en el 'Breather' baja para que la horda
            // se limpie sola sin que el jugador tenga que hacer nada.
            int maxEnemiesThisCycle = Mathf.Max(
                1,
                Mathf.RoundToInt(currentMaxEnemies * (useWavePhases ? wave.maxEnemiesFactor : 1f)));

            int freeSlots = maxEnemiesThisCycle - aliveEnemies;

            if (freeSlots > 0)
            {
                // La ráfaga sale de la fase si las fases están activas; si no, del ritmo
                // heredado por ronda (días avanzados).
                int baseBurstSize = currentRound >= burstRoundsStart ? lateBurstSize : earlyBurstSize;
                int burstSize = useWavePhases ? Mathf.Max(1, wave.burstSize) : baseBurstSize;
                int spawnAttempts = Mathf.Clamp(Mathf.Min(freeSlots, burstSize), 0, Mathf.Max(0, burstSize));

                for (int i = 0; i < spawnAttempts; i++)
                {
                    if (!TrySpawnEnemyOnNavMesh())
                    {
                        break; // Sin posición válida sobre el NavMesh en este ciclo
                    }
                }
            }

            // El intervalo efectivo también lo modula la fase: el 'Surge' acelera la cadencia
            // y el 'Breather' la relaja, sin tocar el valor base de la ronda.
            float intervalMultiplier = useWavePhases ? wave.intervalMultiplier : 1f;
            nextSpawnTime = Time.time + (currentSpawnInterval * Mathf.Max(0.05f, intervalMultiplier));
        }
    }

    /// <summary>
    /// Tramo de la ola que corresponde al progreso actual de la ronda.
    /// Si las fases están desactivadas o no hay duración conocida, devuelve un tramo neutro
    /// (multiplicador 1) que deja el ritmo heredado intacto.
    /// </summary>
    private SpawnWave EvaluateCurrentWave()
    {
        SpawnWave neutral = new SpawnWave
        {
            phase = SpawnPhase.Calm,
            intervalMultiplier = 1f,
            burstSize = 1,
            maxEnemiesFactor = 1f,
            startAt = 0f,
            endAt = 1f
        };

        if (!useWavePhases || wavePhases == null || wavePhases.Length == 0 || roundDuration <= 0.01f)
        {
            return neutral;
        }

        float progress = RoundProgress;

        // El último tramo que contenga el progreso gana. Así, si el usuario deja huecos entre
        // tramos en el Inspector, el progreso cae en el tramo anterior en vez de neutralizarse.
        SpawnWave result = neutral;

        for (int i = 0; i < wavePhases.Length; i++)
        {
            SpawnWave wave = wavePhases[i];

            if (progress >= wave.startAt && progress < wave.endAt)
            {
                result = wave;
            }
        }

        if (result.phase != currentPhase || !hasEvaluatedPhase)
        {
            currentPhase = result.phase;
            hasEvaluatedPhase = true;
            OnPhaseChanged?.Invoke(currentPhase);

            if (logSpawnScaling)
            {
                Debug.Log(
                    $"<color=orange>[EnemySpawner] Fase '{currentPhase}' (ronda {currentRound}, {progress * 100f:0}% transcurrido) " +
                    $"| intervalo x{result.intervalMultiplier:0.00} | ráfaga {result.burstSize} | tope x{result.maxEnemiesFactor:0.00}</color>",
                    this);
            }
        }

        return result;
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
    ///
    /// <paramref name="roundDuration"/> es opcional (por defecto 0 = ritmo plano heredado): con
    /// ella, la curva de fases sabe en qué punto de la ronda está y el spawner deja de ser plano.
    /// Los cuatro parámetros originales no cambian, así que ningún llamante existente se rompe.
    /// </summary>
    public void ApplyRoundSettings(
        int roundNumber,
        EnemyScalingProfile profile,
        float intervalMultiplier = 1f,
        float maxEnemiesMultiplier = 1f,
        float roundDuration = 0f)
    {
        currentRound = Mathf.Max(1, roundNumber);
        scalingProfile = profile;

        // Reloj de la ronda a cero: las fases empiezan siempre desde el principio.
        roundElapsed = 0f;
        roundDuration = Mathf.Max(0f, roundDuration);
        currentPhase = SpawnPhase.Calm;
        hasEvaluatedPhase = false;

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
                $"Daño x{profile.damageMultiplier:0.00} | Velocidad x{profile.speedMultiplier:0.00} | " +
                $"Fases: {(useWavePhases && roundDuration > 0.01f ? $"sí ({roundDuration:0}s)" : "no (ritmo plano)")}</color>",
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

        if (enemyAI != null)
        {
            // Reseteo preventivo completo para asegurar que no conserve flags de congelación de una partida previa
            enemyAI.ResetEnemyState();
        }

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

        // CORRECCIÓN DE SATURACIÓN: con 0.4s el intervalo tocaba suelo hacia el día 32 y con
        // 100 enemigos el tope tocaba suelo hacia el día 27, así que los días 27-40 eran
        // IDÉNTICOS en densidad. Bajar el suelo a 0.2s y subir el techo a 160 devuelve a la
        // curva su pendiente durante toda la partida de 40 días.
        minSpawnInterval = 0.2f;
        initialMaxEnemies = 22;
        absoluteMaxEnemies = 160;
        intervalDecreaseRate = 0.045f;
        maxEnemiesIncreasePerLevel = 3;
        earlyBurstSize = 1;
        burstRoundsStart = 10;
        lateBurstSize = 3;

        useWavePhases = true;
        wavePhases = DefaultWaves();

        if (logSpawnScaling)
        {
            Debug.Log(
                "[EnemySpawner] Ritmo recomendado aplicado: 1.8s → 0.2s, 22 → 160 enemigos, ráfagas de hasta 3 y " +
                "curva de fases (Calm/Tension/Surge/Breather) activa. " +
                "Guarda la escena (Ctrl+S) para conservarlo.",
                this);
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Dibuja la curva de la fase actual en la vista de Escena: esferas concéntricas con
    /// color y altura por tramo, para ver de un vistazo dónde están el Calm y el Breather.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;

        // Dibuja los rangos de aparición alrededor del jugador en la vista de Escena
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(playerTransform.position, minSpawnDistance);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(playerTransform.position, maxSpawnDistance);

        if (wavePhases == null || wavePhases.Length == 0 || roundDuration <= 0.01f) return;

        // Colores por fase: el mismo código para el gizmo y para el log, para no inventar
        // un segundo mapa que se desincronice.
        for (int i = 0; i < wavePhases.Length; i++)
        {
            SpawnWave wave = wavePhases[i];
            Gizmos.color = GetPhaseColor(wave.phase);

            // Esfera concéntrica cuya altura codifica el multiplicador de la fase.
            float height = 0.5f + (wave.intervalMultiplier * 2f);
            Gizmos.DrawWireSphere(
                playerTransform.position + (Vector3.up * height),
                maxSpawnDistance * (0.5f + (wave.maxEnemiesFactor * 0.5f)));
        }

        // Marcador del punto actual de la ronda.
        Gizmos.color = Color.white;
        float progress = RoundProgress;
        Vector3 marker = playerTransform.position + new Vector3(0f, 0.1f, progress * maxSpawnDistance);
        Gizmos.DrawWireSphere(marker, 0.6f);
    }

    private static Color GetPhaseColor(SpawnPhase phase)
    {
        switch (phase)
        {
            case SpawnPhase.Tension:  return new Color(1f, 0.75f, 0.2f);
            case SpawnPhase.Surge:    return new Color(1f, 0.25f, 0.2f);
            case SpawnPhase.Breather: return new Color(0.4f, 0.9f, 0.5f);
            default:                   return new Color(0.5f, 0.8f, 1f); // Calm
        }
    }
}