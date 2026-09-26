using System;
using System.Collections;
using UnityEngine;

public class PlayerLevelSystem : MonoBehaviour
{
    [Header("Fuente de Datos (configuración inmutable)")]
    [SerializeField] private CharacterDataSO playerData;

    [Header("Panel de Subida de Nivel")]
    [Tooltip("Panel que contiene el componente LevelUpUI. Si se deja vacío, se resuelve automáticamente.")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private LevelUpUI levelUpUI;

    [Header("Progreso de la Partida (runtime)")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float currentXP = 0f;
    [SerializeField] private float xpToNextLevel = 50f;

    [Header("Subidas Encadenadas")]
    [Tooltip("Espera en tiempo real antes de mostrar el siguiente panel cuando se ganan 2 o más niveles de golpe.")]
    [SerializeField] private float delayBetweenLevelUpPanels = 0.3f;

    [Header("Depuración")]
    [SerializeField] private bool logProgress = true;

    private int pendingLevelUps = 0;
    private Coroutine showPanelCoroutine;
    private const int MaxLevelsPerXpGain = 50;

    // --- PROPIEDAD 'Data' QUE BUSCAN GameManager Y XPBarUI ---
    public CharacterDataSO Data => playerData;

    // --- EVENTOS QUE BUSCA XPBarUI ---
    public event Action<float, float> OnXPChanged;      // (currentXP, maxXP)
    public event Action<int> OnLevelUp;                // (newLevel)
    public event Action<int> OnPendingLevelUpsChanged; // (subidas pendientes)

    public int CurrentLevel => currentLevel;
    public float CurrentXP => currentXP;
    public float XPToNextLevel => xpToNextLevel;
    public int PendingLevelUps => pendingLevelUps;
    public bool HasPendingLevelUps => pendingLevelUps > 0;

    private void Awake()
    {
        ResolveLevelUpUI();
    }

    private void Start()
    {
        ResolveLevelUpUI();
        ResetRunProgress();

        if (levelUpUI != null)
        {
            levelUpUI.OnPanelClosed += HandleLevelUpPanelClosed;
        }
        else
        {
            Debug.LogWarning(
                "[PlayerLevelSystem] No se encontró LevelUpUI: las subidas de nivel quedarán pendientes hasta que se asigne.",
                this);
        }

        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.OnStateChanged += HandleGameStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (levelUpUI != null)
        {
            levelUpUI.OnPanelClosed -= HandleLevelUpPanelClosed;
        }

        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.OnStateChanged -= HandleGameStateChanged;
        }
    }

    /// <summary>
    /// Reinicia el progreso de la partida (arranque y reinicios).
    /// Es el único punto que fija el progreso: el ScriptableObject ya no almacena estado.
    /// </summary>
    public void ResetRunProgress()
    {
        currentLevel = 1;
        currentXP = 0f;
        pendingLevelUps = 0;
        xpToNextLevel = CalculateXPToNextLevel(currentLevel);

        OnPendingLevelUpsChanged?.Invoke(pendingLevelUps);
        OnXPChanged?.Invoke(currentXP, xpToNextLevel);

        if (logProgress)
        {
            Debug.Log(
                $"<color=cyan>[PlayerLevelSystem] Progreso reiniciado | Nivel 1 | XP 0/{xpToNextLevel:0}</color>",
                this);
        }
    }
            
    /// <summary>
    /// Añade experiencia y resuelve TODAS las subidas de nivel que correspondan
    /// (una recompensa grande puede dar varios niveles de golpe).
    /// </summary>
    public void AddXP(float amount)
    {
        if (amount <= 0f) return;

        GameStateController stateController = GameStateController.Instance;
        if (stateController != null && (stateController.IsGameOver || stateController.IsVictory))
        {
            return; // No se acumula XP con la partida terminada
        }

        currentXP += amount;

        int levelsGained = 0;

        while (currentXP >= xpToNextLevel && levelsGained < MaxLevelsPerXpGain)
        {
            currentXP -= xpToNextLevel;
            currentLevel++;
            levelsGained++;
            pendingLevelUps++;

            xpToNextLevel = CalculateXPToNextLevel(currentLevel);
            OnLevelUp?.Invoke(currentLevel);
        }

        if (levelsGained >= MaxLevelsPerXpGain)
        {
            Debug.LogWarning(
                $"[PlayerLevelSystem] Se alcanzó el límite de {MaxLevelsPerXpGain} subidas en una sola ganancia de XP. Revisa la curva de XP del CharacterDataSO.",
                this);
        }

        OnXPChanged?.Invoke(currentXP, xpToNextLevel);

        if (levelsGained <= 0) return;

        OnPendingLevelUpsChanged?.Invoke(pendingLevelUps);

        if (logProgress)
        {
            Debug.Log(
                $"<color=cyan>[PlayerLevelSystem] ¡Nivel {currentLevel}! (+{levelsGained}) | XP {currentXP:0}/{xpToNextLevel:0} | Mejoras pendientes: {pendingLevelUps}</color>",
                this);
        }

        TryShowLevelUpPanel();
    }

    /// <summary>Muestra el panel de mejoras si hay subidas pendientes y el estado lo permite.</summary>
    private void TryShowLevelUpPanel()
    {
        if (pendingLevelUps <= 0) return;

        if (levelUpUI == null)
        {
            Debug.LogError(
                "[PlayerLevelSystem] Hay subidas pendientes pero falta LevelUpUI en la escena: la mejora quedará en espera.",
                this);
            return;
        }

        if (levelUpUI.IsVisible) return;

        if (showPanelCoroutine != null)
        {
            StopCoroutine(showPanelCoroutine);
        }

        showPanelCoroutine = StartCoroutine(ShowLevelUpPanelRoutine());
    }

    private IEnumerator ShowLevelUpPanelRoutine()
    {
        // Espera en tiempo real: permite encadenar paneles sin solapar animaciones.
        if (delayBetweenLevelUpPanels > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBetweenLevelUpPanels);
        }

        // Los cambios de estado se aplican al final del frame: esperamos a que el estado quede estable
        // antes de consultarlo. Así no se intenta abrir el panel en el mismo frame de una pausa y no
        // se genera el aviso "RequestLevelUp ignorado (estado actual: Paused)".
        yield return new WaitForEndOfFrame();

        showPanelCoroutine = null;

        if (pendingLevelUps <= 0 || levelUpUI == null || levelUpUI.IsVisible)
        {
            yield break;
        }

        GameStateController stateController = GameStateController.Instance;

        if (stateController != null && !stateController.IsPlaying)
        {
            // El panel no puede abrirse ahora (pausa o fin de partida). No es un error:
            // queda en espera y se reintenta al volver a Playing (HandleGameStateChanged).
            LogDeferredPanel(stateController.CurrentState);
            yield break;
        }

        levelUpUI.ShowPanel();

        // Si aun estando en Playing no se abrió, entonces sí es un caso anómalo.
        if (stateController != null && !stateController.IsLevelUpActive)
        {
            Debug.LogWarning(
                "[PlayerLevelSystem] La selección de mejora no pudo abrirse estando en Playing. " +
                "Revisa que el panel esté activo y que LevelUpUI tenga su UpgradeManager asignado.",
                this);
        }
    }

    /// <summary>Registra que la mejora queda en espera, sin alarmar con un Warning.</summary>
    private void LogDeferredPanel(GameState currentState)
    {
        if (!logProgress) return;

        Debug.Log(
            $"<color=cyan>[PlayerLevelSystem] Mejora en espera: no se puede abrir el panel en el estado {currentState}. " +
            "Se mostrará al volver a jugar.</color>",
            this);
    }

    /// <summary>El panel se cierra una vez por cada mejora elegida.</summary>
    private void HandleLevelUpPanelClosed()
    {
        pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);
        OnPendingLevelUpsChanged?.Invoke(pendingLevelUps);

        if (logProgress)
        {
            Debug.Log($"<color=cyan>[PlayerLevelSystem] Mejora elegida. Pendientes: {pendingLevelUps}</color>", this);
        }
    }

    private void HandleGameStateChanged(GameState previous, GameState current)
    {
        if (current == GameState.Playing)
        {
            // Reintento: al cerrar un panel o al salir de la pausa.
            TryShowLevelUpPanel();
            return;
        }

        if (current == GameState.GameOver || current == GameState.Victory)
        {
            // Partida terminada: descartar pendientes evita paneles fantasma.
            pendingLevelUps = 0;
            OnPendingLevelUpsChanged?.Invoke(pendingLevelUps);
        }
    }

    private float CalculateXPToNextLevel(int level)
    {
        if (playerData != null)
        {
            return playerData.GetXPToNextLevel(level);
        }

        // Respaldo si falta el ScriptableObject
        return Mathf.Max(1f, Mathf.Round(50f * Mathf.Pow(1.25f, Mathf.Max(0, level - 1))));
    }

    private void ResolveLevelUpUI()
    {
        if (levelUpUI == null && levelUpPanel != null)
        {
            levelUpUI = levelUpPanel.GetComponent<LevelUpUI>();
        }
    }

    [ContextMenu("Registrar progreso actual en consola")]
    private void DebugProgress()
    {
        Debug.Log(
            $"[PlayerLevelSystem:'{name}'] Nivel {currentLevel} | XP {currentXP:0}/{xpToNextLevel:0} | Pendientes: {pendingLevelUps}",
            this);
    }

}