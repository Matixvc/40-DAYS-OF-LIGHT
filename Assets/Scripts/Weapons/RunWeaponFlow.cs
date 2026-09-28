using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flujo de armas de la partida: elección del arma inicial al empezar y recompensa por
/// derrotar a un jefe.
///
/// POR QUÉ UN COMPONENTE APARTE Y NO DOS: los dos casos son el mismo patrón (escuchar un
/// evento del <see cref="RunDirector"/> y abrir el <see cref="LevelUpUI"/> con un conjunto
/// concreto de cartas). Juntarlos aquí evita dos clases casi idénticas y un segundo
/// punto donde olvidarse de desuscribirse.
///
/// ORDEN DE ARRANQUE: <see cref="RunDirector.StartRun"/> dispara <c>OnRunStarted</c> ANTES de
/// <c>StartRound(1)</c>, así que este script abre el panel del arma inicial antes de que
/// exista la primera oleada. Además pausa el spawner para que no aparezcan enemigos detrás
/// del panel: elegir arma con 12 bichos encima se lee como un castigo, no como una elección.
/// El spawner se reanuda solo cuando el panel se cierra.
///
/// NO TOCA RunDirector NI LevelUpUI: se limita a suscribirse a sus eventos, de modo que
/// ninguno de los dos necesita saber que este flujo existe.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Run Weapon Flow")]
public class RunWeaponFlow : MonoBehaviour
{
    [Header("Referencias (se resuelven solas si están vacías)")]
    [Tooltip("Panel de selección de nivel. Si se deja vacío se busca en la escena.")]
    [SerializeField] private LevelUpUI levelUpUI;

    [Tooltip("Director de la partida. Si se deja vacío se usa RunDirector.Instance.")]
    [SerializeField] private RunDirector runDirector;

    [Tooltip("Spawner, para pausarlo mientras el jugador elige su arma inicial.")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Tooltip("Controlador de armas del Player, para saber si ya lleva alguna equipada.")]
    [SerializeField] private WeaponController weaponController;

    [Header("Armas iniciales (Día 1)")]
    [Tooltip("Activa la pantalla de elección de arma al empezar la partida.")]
    [SerializeField] private bool enableStartingWeaponChoice = true;

    [Tooltip("Armas ofrecidas al empezar. La herramienta de 1 clic rellena esta lista.")]
    [SerializeField] private List<UpgradeDataSO> startingWeaponUpgrades = new List<UpgradeDataSO>();

    [Tooltip("Si está activo, el spawner se detiene mientras el panel está abierto.")]
    [SerializeField] private bool pauseSpawnerDuringChoice = true;

    [Header("Recompensa de jefe")]
    [Tooltip("Abre un panel con cartas de arma/evolución cada vez que se derrota a un jefe.")]
    [SerializeField] private bool enableBossReward = true;

    [Tooltip("Cartas ofrecidas al derrotar a un jefe. Vacío = se usa el sorteo normal.")]
    [SerializeField] private List<UpgradeDataSO> bossRewardUpgrades = new List<UpgradeDataSO>();

    [Header("Depuración")]
    [SerializeField] private bool logFlow = true;

    /// <summary>True entre OnRunStarted y la elección: evita abrir el panel dos veces.</summary>
    private bool awaitingInitialChoice;

    private bool spawnerPausedByUs;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        // En Start han terminado todos los Awake: momento seguro para resolver referencias.
        ResolveReferences();
        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        // Si nos destruyen con el panel abierto, no dejamos el spawner en pausa.
        ResumeSpawner();
    }

    private void ResolveReferences()
    {
        if (levelUpUI == null)
        {
            levelUpUI = FindAnyObjectByType<LevelUpUI>();
        }

        if (runDirector == null)
        {
            runDirector = RunDirector.Instance != null ? RunDirector.Instance : FindAnyObjectByType<RunDirector>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindAnyObjectByType<EnemySpawner>();
        }

        if (weaponController == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            weaponController = player != null ? player.GetComponent<WeaponController>() : null;
        }
    }

    private void Subscribe()
    {
        if (runDirector == null)
        {
            Debug.LogWarning(
                "[RunWeaponFlow] No se encontró RunDirector: la elección de arma inicial y la recompensa " +
                "de jefe no se dispararán. Ejecuta '40 Days of Light > Game Feel > 0. Configurar TODO'.",
                this);
            return;
        }

        runDirector.OnRunStarted += HandleRunStarted;
        runDirector.OnBossDefeated += HandleBossDefeated;
    }

    private void Unsubscribe()
    {
        if (runDirector != null)
        {
            runDirector.OnRunStarted -= HandleRunStarted;
            runDirector.OnBossDefeated -= HandleBossDefeated;
        }

        if (levelUpUI != null)
        {
            levelUpUI.OnPanelClosed -= HandlePanelClosed;
        }
    }

    // ======================================================================
    // 1. SELECCIÓN DE ARMA INICIAL
    // ======================================================================

    /// <summary>
    /// Se dispara al empezar la partida. Si el jugador no lleva ninguna arma, abre el panel
    /// con las armas iniciales. Con arma ya equipada no hace nada: es el caso de una partida
    /// reanudada o de una escena de prueba con loadout pre-cargado.
    /// </summary>
    private void HandleRunStarted(RunDifficulty difficulty, int totalRounds)
    {
        if (!enableStartingWeaponChoice) return;
        if (levelUpUI == null) return;

        // Sin arma inicial ya equipada no hay nada que elegir.
        if (weaponController != null && weaponController.WeaponCount > 0) return;

        if (startingWeaponUpgrades == null || startingWeaponUpgrades.Count == 0)
        {
            // No es un error grave: el juego sigue con el arma por defecto.
            Debug.LogWarning(
                "[RunWeaponFlow] No hay armas iniciales configuradas: la partida arranca con el arma por defecto. " +
                "Ejecuta la herramienta de 1 clic para rellenarlas.",
                this);
            return;
        }

        awaitingInitialChoice = true;

        // Nada de enemigos detrás del panel: la elección debe leerse limpia.
        PauseSpawner();

        levelUpUI.OnPanelClosed -= HandlePanelClosed;
        levelUpUI.OnPanelClosed += HandlePanelClosed;

        levelUpUI.ShowPanelWith(startingWeaponUpgrades);

        if (logFlow)
        {
            Debug.Log(
                $"<color=cyan>[RunWeaponFlow] Elige tu arma inicial ({startingWeaponUpgrades.Count} opciones).</color>",
                this);
        }
    }

    // ======================================================================
    // 2. RECOMPENSA DE JEFE
    // ======================================================================

    /// <summary>
    /// Recompensa por derrotar a un jefe: un panel con cartas de arma o evolución.
    /// Llega en la NOCHE, que es justo cuando la build de armas importa más.
    /// </summary>
    private void HandleBossDefeated(int round)
    {
        if (!enableBossReward) return;
        if (levelUpUI == null) return;

        // Si el panel normal de subida de nivel ya está abierto (el jefe muere de un golpe
        // crítico y sube de nivel a la vez), respetamos ese panel en vez de pisarlo.
        if (levelUpUI.IsVisible) return;

        if (logFlow)
        {
            Debug.Log(
                $"<color=cyan>[RunWeaponFlow] Jefe de la ronda {round} derrotado: eliges recompensa.</color>",
                this);
        }

        if (bossRewardUpgrades != null && bossRewardUpgrades.Count > 0)
        {
            levelUpUI.ShowPanelWith(bossRewardUpgrades);
        }
        else
        {
            // Sin cartas de jefe configuradas, el sorteo normal también sirve: es un premio.
            levelUpUI.ShowPanel();
        }
    }

    // ======================================================================
    // 3. PAUSA DEL SPAWNER
    // ======================================================================

    /// <summary>Reanuda el spawn cuando el jugador cierra el panel del arma inicial.</summary>
    private void HandlePanelClosed()
    {
        if (!awaitingInitialChoice) return;

        awaitingInitialChoice = false;
        ResumeSpawner();

        if (levelUpUI != null)
        {
            levelUpUI.OnPanelClosed -= HandlePanelClosed;
        }

        if (logFlow)
        {
            Debug.Log("<color=cyan>[RunWeaponFlow] Arma inicial elegida: la horda vuelve a moverse.</color>", this);
        }
    }

    private void PauseSpawner()
    {
        if (!pauseSpawnerDuringChoice || enemySpawner == null) return;
        if (!enemySpawner.SpawningEnabled) return;

        enemySpawner.SetSpawningEnabled(false);
        spawnerPausedByUs = true;
    }

    private void ResumeSpawner()
    {
        if (!spawnerPausedByUs || enemySpawner == null) return;

        enemySpawner.SetSpawningEnabled(true);
        spawnerPausedByUs = false;
    }

    [ContextMenu("Registrar estado del flujo")]
    private void DebugState()
    {
        Debug.Log(
            $"[RunWeaponFlow:'{name}'] Panel: {(levelUpUI != null ? levelUpUI.name : "NO ASIGNADO")} | " +
            $"RunDirector: {(runDirector != null ? "OK" : "NO ASIGNADO")} | " +
            $"Armas iniciales: {(startingWeaponUpgrades != null ? startingWeaponUpgrades.Count : 0)} | " +
            $"Recompensas de jefe: {(bossRewardUpgrades != null ? bossRewardUpgrades.Count : 0)} | " +
            $"Armas equipadas: {(weaponController != null ? weaponController.WeaponCount : 0)} | " +
            $"Esperando elección: {awaitingInitialChoice}",
            this);
    }
}
