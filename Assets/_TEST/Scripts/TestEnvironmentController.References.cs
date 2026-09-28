using UnityEngine;

public partial class TestEnvironmentController
{
    // ================= Accesores de solo lectura =================
    // El panel de depuración necesita leer el estado de partida. Se exponen como
    // propiedades PascalCase para no chocar con los campos [SerializeField] en camelCase,
    // y para que quede claro que el sandbox no escribe en el gameplay.

    /// <summary>Estadísticas de partida en runtime (o null si el Player no tiene RunStats).</summary>
    public RunStats Stats => runStats;

    /// <summary>Vida del jugador (o null).</summary>
    public HealthComponent PlayerHealth => playerHealth;

    /// <summary>Progresión de nivel del jugador (o null).</summary>
    public PlayerLevelSystem LevelSystem => levelSystem;

    /// <summary>Spawner continuo de la partida (o null).</summary>
    public EnemySpawner Spawner => enemySpawner;

    /// <summary>True si el spawner continuo de la partida está generando enemigos.</summary>
    public bool ContinuousSpawnEnabled => enemySpawner != null && enemySpawner.SpawningEnabled;

    /// <summary>
    /// Auto-resolución defensiva. Cada aviso explica exactamente qué revisar: en un
    /// sandbox la mitad del valor está en que el fallo se entienda a los cinco segundos.
    /// </summary>
    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        if (player != null)
        {
            if (playerAttack == null) playerAttack = player.GetComponent<PlayerAttack>();
            if (playerHealth == null) playerHealth = player.GetComponent<HealthComponent>();
            if (runStats == null) runStats = player.GetComponent<RunStats>();
            if (levelSystem == null) levelSystem = player.GetComponent<PlayerLevelSystem>();
            if (weaponController == null) weaponController = player.GetComponent<WeaponController>();
            if (spawnCenter == null) spawnCenter = player.transform;
        }

        if (weaponController == null) weaponController = FindAnyObjectByType<WeaponController>();

        if (runStats == null) runStats = RunStats.Active;
        if (upgradeManager == null) upgradeManager = FindAnyObjectByType<UpgradeManager>();
        if (enemySpawner == null) enemySpawner = FindAnyObjectByType<EnemySpawner>();
        if (runDirector == null) runDirector = RunDirector.Instance;
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (navMeshGate == null) navMeshGate = FindAnyObjectByType<TestNavMeshGate>();
        if (debugPanel == null) debugPanel = FindAnyObjectByType<TestDebugPanel>();
        if (poolManager == null) poolManager = ObjectPoolManager.Instance;

        ResolveEnemyPrefab();

        if (player == null)
        {
            Debug.LogError("[TestEnvironmentController] No hay Player en la escena: el sandbox no puede probarlo.", this);
        }

        if (enemyPrefab == null)
        {
            Debug.LogError(
                "[TestEnvironmentController] No hay prefab enemigo asignado: el spawn masivo y la congelación de IA no funcionarán.",
                this);
        }

        if (logActions)
        {
            Debug.Log(
                $"<color=cyan>[TestEnvironmentController] Listo | NavMesh: {(NavMeshReady ? "OK" : "PENDIENTE")} | " +
                $"Enemigo: {(enemyPrefab != null ? enemyPrefab.name : "SIN ASIGNAR")} | " +
                $"Centro de spawn: {(spawnCenter != null ? spawnCenter.name : "SIN ASIGNAR")}</color>",
                this);
        }
    }

    /// <summary>
    /// Busca el prefab enemigo si el Inspector lo dejó vacío. Misma cascada defensiva que
    /// el Player: asignado a mano, luego nombre exacto, luego cualquier prefab de enemigo.
    /// </summary>
    private void ResolveEnemyPrefab()
    {
        if (enemyPrefab != null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!TryFindPrefab("Enemy01 t:Prefab", out enemyPrefab))
        {
            TryFindPrefab("Enemy t:Prefab", out enemyPrefab);
        }
#else
        // Fuera del editor no hay AssetDatabase: el sandbox exige que el prefab esté
        // asignado en el Inspector. La escena TEST no forma parte de ninguna build.
        Debug.LogWarning(
            "[TestEnvironmentController] Fuera del editor el prefab enemigo debe asignarse a mano en el Inspector.",
            this);
#endif
    }

#if UNITY_EDITOR
    private static bool TryFindPrefab(string filter, out GameObject result)
    {
        result = null;

        string[] guids = UnityEditor.AssetDatabase.FindAssets(filter);

        for (int i = 0; i < guids.Length; i++)
        {
            GameObject candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]));

            if (candidate != null)
            {
                result = candidate;
                return true;
            }
        }

        return false;
    }
#endif

    /// <summary>
    /// True si se puede generar una horda ahora mismo. El NavMesh es requisito duro: sin él
    /// <c>NavMesh.SamplePosition</c> devuelve false y el spawn fallaría en silencio, que es el
    /// peor síntoma posible en una herramienta de pruebas.
    /// </summary>
    public bool CanSpawnHorde()
    {
        if (enemyPrefab == null || spawnCenter == null)
        {
            return false;
        }

        if (navMeshGate != null && !navMeshGate.IsReady)
        {
            return false;
        }

        return UnityEngine.AI.NavMesh.CalculateTriangulation().indices.Length > 0;
    }

    /// <summary>Elimina del registro los enemigos ya reciclados o destruidos.</summary>
    private void PruneTestSpawns()
    {
        testSpawns.RemoveAll(ai => ai == null);
    }
}
