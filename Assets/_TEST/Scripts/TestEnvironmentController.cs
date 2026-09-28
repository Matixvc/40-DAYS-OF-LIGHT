using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lógica del entorno de pruebas: hordas masivas, congelación de IA, activación de armas
/// y atajos de depuración. NO dibuja nada: <see cref="TestDebugPanel"/> sólo lee las
/// propiedades públicas de este componente.
///
/// Decisión de arquitectura: este script <b>no modifica ningún script de producción</b>.
/// El spawn replica lo que hace <c>EnemySpawner.ConfigureSpawnedEnemy</c> (que es privado)
/// usando únicamente su API pública: <c>ResetEnemyState</c>, <c>SetTarget</c>,
/// <c>ApplySpawnScaling</c> y <c>ApplyHealthScaling</c>.
///
/// Consecuencia conocida y aceptada: los enemigos lanzados desde aquí NO quedan registrados
/// en el contador interno del <c>EnemySpawner</c> (<c>aliveEnemiesCount</c> / <c>trackedEnemies</c>).
/// En la escena TEST es inocuo porque el spawner continuo se deja desactivado y la gestión de
/// instancias es del <c>ObjectPoolManager</c>. Documentado en Assets/_TEST/README.md.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("TEST/Test Environment Controller")]
public partial class TestEnvironmentController : MonoBehaviour
{
    [Header("Referencias de escena (se auto-resuelven si quedan vacías)")]
    [SerializeField] private TestNavMeshGate navMeshGate;
    [SerializeField] private TestDebugPanel debugPanel;
    [SerializeField] private TestHotkeyMap hotkeyMap;

    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private RunStats runStats;
    [SerializeField] private PlayerLevelSystem levelSystem;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private RunDirector runDirector;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ObjectPoolManager poolManager;

    [Header("Spawn de prueba")]
    [Tooltip("Prefab enemigo a generar. Si se deja vacío se busca 'Enemy01' en el proyecto.")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("Centro desde el que se sortean las posiciones (normalmente el jugador).")]
    [SerializeField] private Transform spawnCenter;
    [SerializeField, Min(0.5f)] private float spawnMinDistance = 8f;
    [SerializeField, Min(0.5f)] private float spawnMaxDistance = 16f;
    [SerializeField, Min(1)] private int navMeshSampleAttempts = 8;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 3f;

    [Header("Escalado de las hordas")]
    [SerializeField] private bool applyScaling = true;
    [SerializeField, Min(0.01f)] private float hordeHealthMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float hordeDamageMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float hordeSpeedMultiplier = 1f;

    [Header("Depuración")]
    [SerializeField] private bool logActions = true;

    // Registro de lo que ha generado el sandbox, para limpiarlo sin tocar enemigos que
    // el EnemySpawner haya creado por su cuenta. Cero GC: se purgan los nulos.
    private readonly List<EnemyAI> testSpawns = new List<EnemyAI>();

    // ================= Estado observable por el panel =================

    /// <summary>True cuando el NavMesh ya admite búsqueda de posiciones.</summary>
    public bool NavMeshReady => navMeshGate == null || navMeshGate.IsReady;

    /// <summary>Enemigos que ha generado el sandbox y siguen vivos.</summary>
    public int TestSpawnCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < testSpawns.Count; i++)
            {
                if (testSpawns[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Enemigos activos totales según el pool (incluye los del spawner continuo).</summary>
    public int TotalEnemiesAlive => enemyPrefab != null && poolManager != null
        ? poolManager.GetActiveCount(enemyPrefab)
        : TestSpawnCount;

    public bool PlayerAttackEnabled => playerAttack != null && playerAttack.enabled;
    public bool EnemyAiFrozen { get; private set; }
    public bool GodMode { get; private set; }
    public int SelectedUpgradeIndex { get; private set; }
    public int TestUpgradeCount => testUpgrades != null ? testUpgrades.Length : 0;
    public UpgradeDataSO SelectedUpgrade => GetSelectedUpgradeOrNull();
    public bool HasUpgrades => TestUpgradeCount > 0;

    // ================= Armas (Fase 3) =================

    /// <summary>Gestor de armas del jugador (o null si la escena no lo tiene).</summary>
    public WeaponController Weapons => weaponController;

    /// <summary>True si hay WeaponController y está gestionando el combate.</summary>
    public bool UsingWeaponSystem => weaponController != null && weaponController.UseNewWeaponSystem;

    /// <summary>Armas equipadas ahora mismo (0 si no hay WeaponController).</summary>
    public int EquippedWeaponCount => weaponController != null ? weaponController.WeaponCount : 0;

    /// <summary>Resumen legible del loadout, para el panel.</summary>
    public string LoadoutSummary => weaponController != null ? weaponController.GetLoadoutSummary() : "sin sistema de armas";

    /// <summary>Índice del arma del catálogo que equipará el próximo F11.</summary>
    public int SelectedWeaponIndex { get; private set; }

    /// <summary>Número de armas en el catálogo de prueba.</summary>
    public int TestWeaponCount => testWeapons != null ? testWeapons.Length : 0;

    /// <summary>
    /// True si el "ataque del jugador" está operativo por CUALQUIERA de las dos vías: el pulso
    /// original o las armas nuevas. El panel no debería tener que saber cuál está en uso.
    /// </summary>
    public bool AttackOperational => weaponController != null && weaponController.UseNewWeaponSystem
        ? weaponController.WeaponsActive
        : PlayerAttackEnabled;

    private void Awake()
    {
        PruneTestSpawns();
    }

    private void Start()
    {
        // Todas las Awake de la escena ya han terminado: punto seguro para resolver
        // referencias, igual que hacen PlayerController y UpgradeManager.
        ResolveReferences();
        SubscribeHotkeys();
        PruneTestSpawns();
    }

    private void OnDestroy()
    {
        UnsubscribeHotkeys();
    }
}
