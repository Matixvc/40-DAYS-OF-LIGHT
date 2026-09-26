using System;
using UnityEngine;

/// <summary>Dificultad global de la partida.</summary>
public enum RunDifficulty
{
    Normal,
    Hard,
    Expert
}

/// <summary>Multiplicadores globales asociados a una dificultad.</summary>
[Serializable]
public struct DifficultyModifiers
{
    [Tooltip("Multiplicador global de la vida de los enemigos.")]
    public float healthMultiplier;

    [Tooltip("Multiplicador global del daño de los enemigos.")]
    public float damageMultiplier;

    [Tooltip("Multiplicador global de la velocidad de los enemigos.")]
    public float speedMultiplier;
}

/// <summary>
/// Director de la partida: 40 rondas (días), rondas de noche con jefe y condición de victoria.
/// Es el único sistema que decide cuándo avanza la ronda y cuándo termina la partida.
/// No toca el tiempo del juego: eso es responsabilidad de GameStateController.
/// </summary>
[DefaultExecutionOrder(-400)]
[DisallowMultipleComponent]
public class RunDirector : MonoBehaviour
{
    public static RunDirector Instance { get; private set; }

    [Header("Rondas (Días)")]
    [Tooltip("Total de rondas de la partida.")]
    [SerializeField] private int totalRounds = 40;
    [Tooltip("Duración de una ronda normal, en segundos.")]
    [SerializeField] private float normalRoundDuration = 20f;
    [Tooltip("Duración de una ronda de noche. El jefe debe morir para poder avanzar.")]
    [SerializeField] private float nightRoundDuration = 30f;
    [Tooltip("Cada cuántas rondas aparece una noche con jefe (10 = días 10, 20, 30 y 40).")]
    [SerializeField] private int roundsPerNight = 10;
    [Tooltip("Si está activo, la partida arranca sola al entrar en Playing (útil mientras no haya menú).")]
    [SerializeField] private bool autoStartOnPlay = true;

    [Header("Dificultad global")]
    [Tooltip("Dificultad por defecto. La elección del menú principal la sobrescribe si 'Use Saved Difficulty' está activo.")]
    [SerializeField] private RunDifficulty difficulty = RunDifficulty.Normal;
    [Tooltip("Usa la dificultad guardada por el menú principal (PlayerPrefs). Desactívalo para probar siempre la de este Inspector.")]
    [SerializeField] private bool useSavedDifficulty = true;
    [SerializeField] private DifficultyModifiers normalModifiers = new DifficultyModifiers { healthMultiplier = 1f, damageMultiplier = 1f, speedMultiplier = 1f };
    [SerializeField] private DifficultyModifiers hardModifiers = new DifficultyModifiers { healthMultiplier = 1.35f, damageMultiplier = 1.25f, speedMultiplier = 1.1f };
    [SerializeField] private DifficultyModifiers expertModifiers = new DifficultyModifiers { healthMultiplier = 1.75f, damageMultiplier = 1.5f, speedMultiplier = 1.2f };

    [Header("Escalado por ronda")]
    [Tooltip("Vida extra por ronda (0.12 = +12%).")]
    [SerializeField] private float healthIncreasePerRound = 0.12f;
    [Tooltip("Daño extra por ronda (0.07 = +7%).")]
    [SerializeField] private float damageIncreasePerRound = 0.07f;
    [Tooltip("Velocidad extra por ronda (0.05 = +5%): en la ronda 25+ los enemigos son claramente más rápidos.")]
    [SerializeField] private float speedIncreasePerRound = 0.05f;
    [SerializeField] private float maxHealthMultiplier = 6.5f;
    [SerializeField] private float maxDamageMultiplier = 4f;
    [SerializeField] private float maxSpeedMultiplier = 2.2f;

    [Header("Ritmo durante la Noche")]
    [Tooltip("Multiplicador del intervalo de spawn de enemigos comunes durante la noche (1.8 = 80% más lento, pero sin detenerlo).")]
    [SerializeField] private float nightSpawnIntervalMultiplier = 1.8f;
    [Tooltip("Multiplicador del tope de enemigos comunes durante la noche (0.6 = 40% menos, para que la pelea con el jefe se lea bien).")]
    [SerializeField] private float nightMaxEnemiesMultiplier = 0.6f;

    [Header("Jefe de Noche")]
    [SerializeField] private string bossName = "Sombra de la Noche";
    [Tooltip("Multiplicadores del jefe respecto a un enemigo normal de esa ronda.")]
    [SerializeField] private float bossHealthMultiplier = 12f;
    [SerializeField] private float bossDamageMultiplier = 1.6f;
    [SerializeField] private float bossSpeedMultiplier = 1.05f;
    [Tooltip("Escala visual del modelo del jefe.")]
    [SerializeField] private float bossScaleMultiplier = 1.6f;
    [Tooltip("Multiplicador base de XP del jefe. Se multiplica además por el número de noche (25 → x25, x50, x75, x100).")]
    [SerializeField] private float bossXpMultiplier = 25f;

    [Header("Referencias")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Depuración")]
    [SerializeField] private bool logRunEvents = true;

    // --- Estado de la partida (solo lectura desde fuera) ---
    public int CurrentRound { get; private set; }
    public int TotalRounds => totalRounds;
    public float RoundTimeRemaining { get; private set; }
    public float CurrentRoundDuration { get; private set; }
    public bool IsNightRound { get; private set; }
    public bool IsRunActive { get; private set; }
    public bool IsBossAlive => isBossAlive;
    public RunDifficulty Difficulty => difficulty;
    public string BossName => bossName;
    /// <summary>Tiempo total jugado de la partida, en segundos (no cuenta pausas ni selección de mejoras).</summary>
    public float RunElapsedTime => runElapsedTime;

    /// <summary>Noches totales de la partida (una noche cada N rondas).</summary>
    public int RoundsPerNight => roundsPerNight;

    /// <summary>Índice de la noche actual (1..N) o 0 si la ronda actual no es de noche.</summary>
    public int CurrentNightIndex => IsNightRound && roundsPerNight > 0 ? CurrentRound / roundsPerNight : 0;

    // --- Eventos para la UI ---
    /// <summary>(dificultad, total de rondas)</summary>
    public event Action<RunDifficulty, int> OnRunStarted;
    /// <summary>(número de ronda, esNoche)</summary>
    public event Action<int, bool> OnRoundStarted;
    /// <summary>(número de ronda completada)</summary>
    public event Action<int> OnRoundCompleted;
    /// <summary>(número de la ronda de noche que comienza)</summary>
    public event Action<int> OnNightWarning;
    /// <summary>(número de ronda, vida máxima del jefe)</summary>
    public event Action<int, float> OnBossSpawned;
    /// <summary>(número de ronda en la que murió el jefe)</summary>
    public event Action<int> OnBossDefeated;
    /// <summary>(tiempo restante, duración de la ronda)</summary>
    public event Action<float, float> OnRoundTimeChanged;
    /// <summary>(nueva dificultad)</summary>
    public event Action<RunDifficulty> OnDifficultyChanged;
    /// <summary>(true = victoria, false = derrota)</summary>
    public event Action<bool> OnRunEnded;

    private bool isBossAlive;
    private HealthComponent activeBossHealth;
    private float runElapsedTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RunDirector] Ya existe una instancia activa. Se desactiva el duplicado.", this);
            enabled = false;
            return;
        }

        Instance = this;
        ResolveSpawner();
    }

    private void Start()
    {
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.OnStateChanged += HandleGameStateChanged;
        }

        if (autoStartOnPlay && (stateController == null || stateController.IsPlaying))
        {
            StartRun();
        }
    }

    private void OnDestroy()
    {
        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.OnStateChanged -= HandleGameStateChanged;
        }

        if (activeBossHealth != null)
        {
            activeBossHealth.OnDeath -= HandleBossDeath;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!IsRunActive) return;

        // El reloj de ronda solo corre durante Playing: pausa, selección de mejora y fin de partida lo congelan.
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null && !stateController.IsPlaying)
        {
            return;
        }

        // Red de seguridad: con la partida activa el spawner SIEMPRE debe estar generando.
        // Evita que una noche mal cerrada deje las rondas siguientes sin enemigos.
        if (enemySpawner != null && !enemySpawner.SpawningEnabled)
        {
            Debug.LogWarning("[RunDirector] El spawn estaba detenido con la partida activa: se ha reactivado.", this);
            enemySpawner.SetSpawningEnabled(true);
        }

        RoundTimeRemaining = Mathf.Max(0f, RoundTimeRemaining - Time.deltaTime);
        runElapsedTime += Time.deltaTime;
        OnRoundTimeChanged?.Invoke(RoundTimeRemaining, CurrentRoundDuration);

        if (RoundTimeRemaining > 0f) return;

        // La noche no se cierra mientras el jefe siga vivo.
        if (IsNightRound && isBossAlive) return;

        CompleteRound();
    }

    // ======================================================================
    // CICLO DE PARTIDA
    // ======================================================================

    /// <summary>Arranca la partida desde el Día 1. Se llama solo (o desde un futuro menú principal).</summary>
    public void StartRun()
    {
        if (IsRunActive) return;

        ResolveSpawner();

        if (enemySpawner == null)
        {
            Debug.LogError("[RunDirector] Falta EnemySpawner en la escena: no se puede iniciar la partida.", this);
            return;
        }

        IsRunActive = true;
        runElapsedTime = 0f;

        // La dificultad elegida en el menú principal tiene prioridad sobre la del Inspector.
        if (useSavedDifficulty)
        {
            difficulty = GameSessionConfig.SelectedDifficulty;
        }

        OnDifficultyChanged?.Invoke(difficulty);
        OnRunStarted?.Invoke(difficulty, totalRounds);

        if (logRunEvents)
        {
            Debug.Log($"<color=green>[RunDirector] Partida iniciada | Dificultad {difficulty} | {totalRounds} días</color>", this);
        }

        StartRound(1);
    }

    /// <summary>Cambia la dificultad global. Solo permitido antes de empezar la partida.</summary>
    public void SetDifficulty(RunDifficulty newDifficulty)
    {
        if (IsRunActive)
        {
            Debug.LogWarning("[RunDirector] No se puede cambiar la dificultad con la partida en curso.", this);
            return;
        }

        difficulty = newDifficulty;
        OnDifficultyChanged?.Invoke(difficulty);
    }

    private void StartRound(int roundNumber)
    {
        CurrentRound = Mathf.Clamp(roundNumber, 1, totalRounds);
        IsNightRound = IsNight(CurrentRound);
        CurrentRoundDuration = IsNightRound ? nightRoundDuration : normalRoundDuration;
        RoundTimeRemaining = CurrentRoundDuration;
        isBossAlive = false;

        EnemyScalingProfile profile = BuildScalingProfile(CurrentRound);

        // El spawner ajusta su ritmo a la ronda y recibe el escalado de estadísticas.
        // De noche el spawn CONTINÚA: más espaciado y con menos enemigos simultáneos,
        // para que el jugador siempre tenga objetivos sin saturar la pelea con el jefe.
        float intervalMultiplier = IsNightRound ? nightSpawnIntervalMultiplier : 1f;
        float maxEnemiesMultiplier = IsNightRound ? nightMaxEnemiesMultiplier : 1f;

        enemySpawner.ApplyRoundSettings(CurrentRound, profile, intervalMultiplier, maxEnemiesMultiplier);

        // Se fuerza el spawn activo al empezar CUALQUIER ronda,
        // incluida la primera ronda posterior a una noche (arregla el "Día 11 sin enemigos").
        enemySpawner.SetSpawningEnabled(true);

        if (IsNightRound)
        {
            OnNightWarning?.Invoke(CurrentRound);
            SpawnNightBoss(profile);
        }

        OnRoundStarted?.Invoke(CurrentRound, IsNightRound);
        OnRoundTimeChanged?.Invoke(RoundTimeRemaining, CurrentRoundDuration);

        if (logRunEvents)
        {
            string nightTag = IsNightRound ? " (NOCHE)" : string.Empty;
            Debug.Log(
                $"<color=cyan>[RunDirector] Día {CurrentRound}/{totalRounds}{nightTag} | {CurrentRoundDuration:0}s | " +
                $"Escalado Vida x{profile.healthMultiplier:0.00} Daño x{profile.damageMultiplier:0.00} Vel x{profile.speedMultiplier:0.00}</color>",
                this);
        }
    }

    private void CompleteRound()
    {
        OnRoundCompleted?.Invoke(CurrentRound);

        if (CurrentRound >= totalRounds)
        {
            FinishRun(true);
            return;
        }

        StartRound(CurrentRound + 1);
    }

    /// <summary>Cierra la partida (victoria o derrota) y detiene el spawn.</summary>
    public void FinishRun(bool victory)
    {
        if (!IsRunActive) return;

        IsRunActive = false;
        isBossAlive = false;

        UnsubscribeFromBoss();

        if (enemySpawner != null)
        {
            enemySpawner.SetSpawningEnabled(false);
        }

        OnRunEnded?.Invoke(victory);

        if (logRunEvents)
        {
            Debug.Log(
                $"<color={(victory ? "green" : "red")}>[RunDirector] Partida terminada | Día {CurrentRound}/{totalRounds} | {(victory ? "VICTORIA" : "DERROTA")}</color>",
                this);
        }

        if (!victory) return;

        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.RequestVictory();
        }
        else
        {
            Debug.LogError("[RunDirector] No hay GameStateController: la victoria no se podrá mostrar.", this);
        }
    }

    // ======================================================================
    // JEFE DE NOCHE
    // ======================================================================

    private void SpawnNightBoss(EnemyScalingProfile baseProfile)
    {
        EnemyScalingProfile bossProfile = new EnemyScalingProfile
        {
            healthMultiplier = baseProfile.healthMultiplier * bossHealthMultiplier,
            damageMultiplier = baseProfile.damageMultiplier * bossDamageMultiplier,
            speedMultiplier = baseProfile.speedMultiplier * bossSpeedMultiplier
        };

        // La recompensa crece en cada noche: x25, x50, x75 y x100.
        int nightIndex = Mathf.Max(1, CurrentNightIndex);
        float xpMultiplier = bossXpMultiplier * nightIndex;

        string displayName = $"{bossName} (Día {CurrentRound})";
        GameObject boss = enemySpawner.SpawnBoss(bossProfile, bossScaleMultiplier, xpMultiplier, displayName);

        if (boss == null)
        {
            // Sin jefe no se puede cerrar la noche: se degrada a ronda normal para no bloquear la partida.
            Debug.LogError("[RunDirector] No se pudo generar el Jefe de Noche. La ronda continuará como ronda normal.", this);
            IsNightRound = false;
            enemySpawner.ApplyRoundSettings(CurrentRound, baseProfile);
            enemySpawner.SetSpawningEnabled(true);
            return;
        }

        activeBossHealth = boss.GetComponentInChildren<HealthComponent>();

        if (activeBossHealth == null)
        {
            Debug.LogError("[RunDirector] El Jefe no tiene HealthComponent: no se podrá detectar su muerte.", boss);
            return;
        }

        isBossAlive = true;
        activeBossHealth.OnDeath += HandleBossDeath;

        OnBossSpawned?.Invoke(CurrentRound, activeBossHealth.MaxHealth);

        if (logRunEvents)
        {
            Debug.Log(
                $"<color=magenta>[RunDirector] ¡NOCHE {CurrentRound}! Aparece '{boss.name}' | Vida {activeBossHealth.MaxHealth:0} | XP x{xpMultiplier:0}</color>",
                boss);
        }
    }

    private void HandleBossDeath()
    {
        UnsubscribeFromBoss();

        isBossAlive = false;
        OnBossDefeated?.Invoke(CurrentRound);

        if (logRunEvents)
        {
            Debug.Log($"<color=green>[RunDirector] Jefe del Día {CurrentRound} derrotado.</color>", this);
        }

        // El jefe del último día cierra la partida con victoria inmediata.
        if (CurrentRound >= totalRounds)
        {
            FinishRun(true);
            return;
        }

        // En el resto de noches la ronda se cierra cuando se agota el tiempo restante.
        if (RoundTimeRemaining <= 0f)
        {
            CompleteRound();
        }
    }

    private void UnsubscribeFromBoss()
    {
        if (activeBossHealth != null)
        {
            activeBossHealth.OnDeath -= HandleBossDeath;
            activeBossHealth = null;
        }
    }

    private void HandleGameStateChanged(GameState previous, GameState current)
    {
        if (!IsRunActive) return;

        if (current == GameState.GameOver)
        {
            FinishRun(false);
            return;
        }

        if (current == GameState.Victory)
        {
            // La victoria ya la hemos disparado nosotros: solo detenemos el spawn.
            IsRunActive = false;
            isBossAlive = false;
            UnsubscribeFromBoss();

            if (enemySpawner != null)
            {
                enemySpawner.SetSpawningEnabled(false);
            }
        }
    }

    // ======================================================================
    // CÁLCULO DE ESCALADO
    // ======================================================================

    private EnemyScalingProfile BuildScalingProfile(int roundNumber)
    {
        DifficultyModifiers modifiers = GetDifficultyModifiers(difficulty);
        int steps = Mathf.Max(0, roundNumber - 1);

        return new EnemyScalingProfile
        {
            healthMultiplier = Mathf.Min(maxHealthMultiplier, 1f + (steps * healthIncreasePerRound)) * modifiers.healthMultiplier,
            damageMultiplier = Mathf.Min(maxDamageMultiplier, 1f + (steps * damageIncreasePerRound)) * modifiers.damageMultiplier,
            speedMultiplier = Mathf.Min(maxSpeedMultiplier, 1f + (steps * speedIncreasePerRound)) * modifiers.speedMultiplier
        };
    }

    private DifficultyModifiers GetDifficultyModifiers(RunDifficulty value)
    {
        switch (value)
        {
            case RunDifficulty.Hard:
                return hardModifiers;

            case RunDifficulty.Expert:
                return expertModifiers;

            default:
                return normalModifiers;
        }
    }

    private bool IsNight(int roundNumber)
    {
        return roundsPerNight > 0 && roundNumber % roundsPerNight == 0;
    }

    private void ResolveSpawner()
    {
        if (enemySpawner == null)
        {
            enemySpawner = FindAnyObjectByType<EnemySpawner>();
        }

        if (enemySpawner == null)
        {
            Debug.LogError("[RunDirector] No se encontró EnemySpawner en la escena.", this);
        }
    }

    /// <summary>
    /// Aplica los valores de balance recomendados para la curva de 40 días.
    /// Útil porque los campos ya serializados en la escena conservan los valores antiguos.
    /// </summary>
    [ContextMenu("Aplicar balance recomendado")]
    private void ApplyRecommendedBalance()
    {
        healthIncreasePerRound = 0.12f;
        damageIncreasePerRound = 0.07f;
        speedIncreasePerRound = 0.05f;
        maxHealthMultiplier = 6.5f;
        maxDamageMultiplier = 4f;
        maxSpeedMultiplier = 2.2f;
        bossXpMultiplier = 25f;
        nightSpawnIntervalMultiplier = 1.8f;
        nightMaxEnemiesMultiplier = 0.6f;
        bossHealthMultiplier = 12f;
        bossDamageMultiplier = 1.6f;
        bossSpeedMultiplier = 1.05f;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        Debug.Log(
            "[RunDirector] Balance recomendado aplicado: vida +12%/ronda, daño +7%/ronda, velocidad +5%/ronda, " +
            "jefes x12 de vida, XP de jefe x25 por noche y spawn nocturno más espaciado. " +
            "Guarda la escena (Ctrl+S) para conservarlo.",
            this);
    }

    [ContextMenu("Registrar estado de la partida")]
    private void DebugRunState()
    {
        Debug.Log(
            $"[RunDirector:'{name}'] Día {CurrentRound}/{totalRounds} | Noche: {IsNightRound} | " +
            $"Tiempo: {RoundTimeRemaining:0.0}/{CurrentRoundDuration:0}s | Jefe vivo: {isBossAlive} | " +
            $"Activa: {IsRunActive} | Dificultad: {difficulty} | Spawner: {(enemySpawner != null ? enemySpawner.name : "NO ASIGNADO")}",
            this);
    }

    [ContextMenu("Forzar fin de ronda (debug)")]
    private void DebugCompleteRound()
    {
        if (!IsRunActive)
        {
            Debug.LogWarning("[RunDirector] La partida no está activa.", this);
            return;
        }

        RoundTimeRemaining = 0f;

        if (IsNightRound && isBossAlive)
        {
            Debug.LogWarning("[RunDirector] La noche no puede cerrarse con el jefe vivo. Matando al jefe (debug)...", this);

            if (activeBossHealth != null)
            {
                activeBossHealth.TakeDamage(activeBossHealth.MaxHealth * 10f);
            }

            return;
        }

        CompleteRound();
    }
}
