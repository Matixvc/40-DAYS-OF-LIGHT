using System;
using UnityEngine;

/// <summary>Estados posibles de la partida.</summary>
public enum GameState
{
    Boot,             // Arranque de escena (transitorio)
    MainMenu,         // Menú principal
    Playing,          // Partida en curso
    LevelUpSelection, // Selección de mejora abierta (congela el juego)
    Paused,           // Menú de pausa abierto (congela el juego)
    GameOver,         // Derrota
    Victory           // Victoria
}

/// <summary>
/// Controlador único del estado de la partida y de Time.timeScale.
/// NINGÚN otro script debe modificar Time.timeScale: todos piden transiciones aquí.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class GameStateController : MonoBehaviour
{
    public static GameStateController Instance { get; private set; }

    [Header("Arranque")]
    [Tooltip("Mientras no exista menú principal, déjalo activado para entrar directamente a Playing.")]
    [SerializeField] private bool startPlayingDirectly = true;

    [Header("Tiempo")]
    [SerializeField] private float normalTimeScale = 1f;

    [Header("Depuración")]
    [SerializeField] private bool logStateChanges = true;

    private const float FrozenTimeScale = 0f;

    public GameState CurrentState { get; private set; } = GameState.Boot;

    /// <summary>Estado al que volver cuando se cierre una superposición (nivel o pausa).</summary>
    public GameState StateBeforeOverlay { get; private set; } = GameState.Playing;

    /// <summary>Evento con (estadoAnterior, estadoNuevo).</summary>
    public event Action<GameState, GameState> OnStateChanged;

    public bool IsPlaying => CurrentState == GameState.Playing;
    public bool IsPaused => CurrentState == GameState.Paused;
    public bool IsLevelUpActive => CurrentState == GameState.LevelUpSelection;
    public bool IsGameOver => CurrentState == GameState.GameOver;
    public bool IsVictory => CurrentState == GameState.Victory;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[GameStateController] Ya existe una instancia activa. Se desactiva el duplicado.",
                this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Boot es transitorio y no se valida.
        // Limpiamos también cualquier timeScale heredado de una recarga de escena.
        Time.timeScale = normalTimeScale;
        ApplyState(startPlayingDirectly ? GameState.Playing : GameState.MainMenu);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ======================================================================
    // PETICIONES DE ESTADO
    // ======================================================================

    /// <summary>Abre la selección de mejora. Solo válido durante la partida.</summary>
    public bool RequestLevelUp()
    {
        if (CurrentState != GameState.Playing)
        {
            LogWarning(
                $"RequestLevelUp ignorado: solo se puede subir de nivel jugando (estado actual: {CurrentState}).");
            return false;
        }

        StateBeforeOverlay = CurrentState;
        return RequestState(GameState.LevelUpSelection);
    }

    /// <summary>Cierra la selección de mejora y devuelve el control al juego.</summary>
    public bool RequestCloseLevelUp()
    {
        if (CurrentState != GameState.LevelUpSelection)
        {
            return false;
        }

        return RequestState(ResolveReturnState());
    }

    /// <summary>
    /// Solicita pausa. Devuelve false si hay una selección de mejora abierta:
    /// el jugador NO puede pausar ni despausar el panel de subida de nivel.
    /// </summary>
    public bool RequestPause()
    {
        if (CurrentState == GameState.LevelUpSelection)
        {
            LogWarning("Pausa bloqueada: hay una selección de mejora activa.");
            return false;
        }

        if (CurrentState != GameState.Playing)
        {
            return false;
        }

        StateBeforeOverlay = CurrentState;
        return RequestState(GameState.Paused);
    }

    /// <summary>Reanuda la partida. Solo desde Paused y nunca desde la selección de mejora.</summary>
    public bool RequestResume()
    {
        if (CurrentState == GameState.LevelUpSelection || CurrentState != GameState.Paused)
        {
            return false;
        }

        return RequestState(ResolveReturnState());
    }

    public bool RequestGameOver()
    {
        if (CurrentState == GameState.GameOver)
        {
            return false;
        }

        return RequestState(GameState.GameOver);
    }

    public bool RequestVictory()
    {
        if (CurrentState == GameState.Victory)
        {
            return false;
        }

        return RequestState(GameState.Victory);
    }

    /// <summary>Vuelve al estado de juego (reinicio de partida o salida del menú).</summary>
    public bool RequestRestart()
    {
        if (CurrentState == GameState.Playing)
        {
            Time.timeScale = normalTimeScale;
            return true;
        }

        return RequestState(GameState.Playing);
    }

    public bool RequestMainMenu()
    {
        return RequestState(GameState.MainMenu);
    }

    /// <summary>
    /// Restaura la escala de tiempo normal antes de cargar otra escena.
    /// No cambia el estado: lo usa la UI de fin de partida (evita dejar el juego congelado
    /// en la escena siguiente si ésta no tiene su propio GameStateController).
    /// </summary>
    public void PrepareForSceneLoad()
    {
        Time.timeScale = normalTimeScale;
    }

    // ======================================================================
    // INTERNO
    // ======================================================================

    /// <summary>
    /// Cambia de estado validando la transición. Es el único punto que modifica Time.timeScale.
    /// </summary>
    public bool RequestState(GameState newState)
    {
        if (newState == CurrentState)
        {
            return false;
        }

        if (!IsTransitionAllowed(CurrentState, newState))
        {
            LogWarning($"Transición rechazada: {CurrentState} → {newState}.");
            return false;
        }

        ApplyState(newState);
        return true;
    }

    private void ApplyState(GameState newState)
    {
        GameState previous = CurrentState;
        CurrentState = newState;

        // ÚNICO punto del proyecto que modifica Time.timeScale.
        Time.timeScale = ShouldFreezeTime(newState) ? FrozenTimeScale : normalTimeScale;

        if (logStateChanges)
        {
            Debug.Log(
                $"<color=magenta>[GameStateController] {previous} → {newState} | timeScale = {Time.timeScale:0.##}</color>",
                this);
        }

        OnStateChanged?.Invoke(previous, newState);
    }

    private static bool ShouldFreezeTime(GameState state)
    {
        switch (state)
        {
            case GameState.Playing:
                return false;

            case GameState.Boot:
            case GameState.MainMenu:
            case GameState.LevelUpSelection:
            case GameState.Paused:
            case GameState.GameOver:
            case GameState.Victory:
            default:
                return true;
        }
    }

    /// <summary>
    /// Grafo de transiciones válidas. Evita estados imposibles,
    /// como pausar mientras se está eligiendo una mejora.
    /// </summary>
    private bool IsTransitionAllowed(GameState from, GameState to)
    {
        switch (to)
        {
            case GameState.MainMenu:
                return from == GameState.Boot
                    || from == GameState.GameOver
                    || from == GameState.Victory;

            case GameState.Playing:
                return from == GameState.Boot
                    || from == GameState.MainMenu
                    || from == GameState.Paused
                    || from == GameState.LevelUpSelection
                    || from == GameState.GameOver
                    || from == GameState.Victory;

            case GameState.LevelUpSelection:
                return from == GameState.Playing;

            case GameState.Paused:
                // Nunca desde LevelUpSelection: la subida de nivel manda sobre la pausa.
                return from == GameState.Playing;

            case GameState.GameOver:
            case GameState.Victory:
                return from == GameState.Playing
                    || from == GameState.LevelUpSelection
                    || from == GameState.Paused;

            case GameState.Boot:
            default:
                return false;
        }
    }

    private GameState ResolveReturnState()
    {
        // La selección de mejora siempre devuelve el control al juego.
        if (CurrentState == GameState.LevelUpSelection)
        {
            return GameState.Playing;
        }

        switch (StateBeforeOverlay)
        {
            case GameState.Playing:
            case GameState.Paused:
            case GameState.MainMenu:
                return StateBeforeOverlay;

            default:
                return GameState.Playing;
        }
    }

    private void LogWarning(string message)
    {
        if (logStateChanges)
        {
            Debug.LogWarning($"[GameStateController] {message}", this);
        }
    }

    [ContextMenu("Registrar estado actual en consola")]
    private void DebugCurrentState()
    {
        Debug.Log(
            $"[GameStateController:'{name}'] Estado: {CurrentState} | timeScale: {Time.timeScale:0.##} | Volver a: {StateBeforeOverlay}",
            this);
    }
}
