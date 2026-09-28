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
///
/// ORDEN DE EJECUCIÓN (crítico): <see cref="RunDirector"/> tiene
/// <c>DefaultExecutionOrder(-400)</c> y dispara <c>OnRunStarted</c> dentro de su propio
/// <c>Start()</c>. Este componente se declara en <c>-450</c>, ANTES, a propósito: si se
/// suscribiera después, el evento ya se habría emitido y el panel no se abriría nunca.
/// Es un fallo silencioso: la suscripción es válida, solo llega tarde. Por eso, además del
/// orden, <see cref="CatchUpIfRunAlreadyStarted"/> cubre el caso restante.
/// </summary>
[DefaultExecutionOrder(-450)]
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

    [Header("Panel de elección de arma (WeaponPanelUI)")]
    [Tooltip("Panel que ofrece 2 armas. Si se deja vacío se busca en la escena por nombre.")]
    [SerializeField] private WeaponPanelUI weaponPanel;

    [Tooltip("Si está activo, usa WeaponPanelUI (2 opciones de arma). Desactívalo para usar el " +
             "LevelUpUI de 3 cartas aleatorias en vez del panel de armas.")]
    [SerializeField] private bool preferWeaponPanel = true;

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

        // Red de seguridad: si por lo que sea la partida ya estaba corriendo cuando nos
        // suscribimos (orden de ejecución alterado, escena reentrante, dominio recargado),
        // el evento OnRunStarted ya se emitió y nunca lo veríamos. Esto cubre ese hueco.
        CatchUpIfRunAlreadyStarted();
    }

    /// <summary>
    /// Abre el panel del arma inicial si la partida ya había empezado cuando este componente
    /// se suscribió. Es idempotente: <see cref="HandleRunStarted"/> y el propio panel ya se
    /// protegen con <c>awaitingInitialChoice</c> / <c>IsVisible</c>, así que llamarlo dos veces
    /// no duplica nada.
    /// </summary>
    private void CatchUpIfRunAlreadyStarted()
    {
        if (runDirector == null || !runDirector.IsRunActive) return;
        if (awaitingInitialChoice) return;

        if (logFlow)
        {
            Debug.Log(
                "[RunWeaponFlow] La partida ya estaba en marcha al suscribirse: se recupera la " +
                "elección de arma inicial.",
                this);
        }

        HandleRunStarted(runDirector.Difficulty, runDirector.TotalRounds);
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

        if (weaponPanel == null)
        {
            // Búsqueda por nombre: el panel es un objeto concreto de la escena de producción,
            // no un componente que pueda haber varios. Un Find por tipo podría coger el
            // panel equivocado si alguien duplicase la escena.
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] found = roots[i].GetComponentsInChildren<Transform>(true);
                weaponPanel = FindNamedPanel(found);
            }
        }
    }

    /// <summary>Localiza el GameObject 'WeaponPanelUI' y devuelve su componente, si lo tiene.</summary>
    private static WeaponPanelUI FindNamedPanel(Transform[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null && candidates[i].name == "WeaponPanelUI")
            {
                return candidates[i].GetComponent<WeaponPanelUI>();
            }
        }

        return null;
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

        // Basta con que exista UNO de los dos paneles: el de armas es el preferido, pero
        // el de nivel sirve de respaldo. Antes esta guarda exigía LevelUpUI y cortaba el
        // flujo aunque el WeaponPanelUI estuviera cableado correctamente.
        bool hasWeaponPanel = preferWeaponPanel && weaponPanel != null;

        if (!hasWeaponPanel && levelUpUI == null) return;

        // Sin arma inicial ya equipada no hay nada que elegir.
        if (weaponController != null && weaponController.WeaponCount > 0) return;

        // El WeaponPanelUI trae su propio catálogo, así que no exige la lista de aquí.
        if (!hasWeaponPanel && (startingWeaponUpgrades == null || startingWeaponUpgrades.Count == 0))
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

        if (preferWeaponPanel && weaponPanel != null)
        {
            // El WeaponPanelUI se pausa y reanuda solo, así que basta con abrirlo.
            weaponPanel.ShowWeaponSelection(2);

            if (logFlow)
            {
                Debug.Log("<color=cyan>[RunWeaponFlow] Elige tu arma inicial (2 opciones).</color>", this);
            }

            return;
        }

        // Respaldo: el panel de nivel de 3 cartas, con las armas iniciales como catálogo.
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

        // El panel de armas filtra solo lo que el jugador ya tiene, así que es el correcto
        // para la recompensa: ofrecer un arma repetida hace que el premio parezca un fallo.
        if (preferWeaponPanel && weaponPanel != null)
        {
            weaponPanel.ShowWeaponSelection(2);
            return;
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
