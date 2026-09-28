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
///
/// ÚNICO PUNTO DEL PROYECTO QUE ESCRIBE Time.timeScale: el método privado <c>ApplyTimeScale</c>.
/// Ese método compone DOS factores:
///   1) el estado de la partida (congelado en pausa, nivel, menú y fin de partida), y
///   2) la micro-pausa de impacto pedida con <see cref="RequestHitStop"/>.
/// Así el hit-stop es un multiplicador más y no una excepción al invariante, y una transición
/// de estado durante un impacto respeta la composición en vez de pisarla.
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

    /// <summary>
    /// Factor de la micro-pausa de impacto. 1 = sin pausa en curso.
    /// Solo lo escriben <see cref="RequestHitStop"/> y el final de la pausa.
    /// </summary>
    private float hitStopTimeScale = 1f;

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
                "[GameStateController] Ya existe una instancia activa. Se destruye el duplicado sin tocar el estado global.",
                this);
            // Desactivar ANTES de destruir evita que Start() ejecute ApplyState() con un
            // timeScale propio (estado colgado). La destrucción es diferida al final del frame.
            enabled = false;
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Boot es transitorio y no se valida.
        // Se limpia cualquier micro-pausa heredada de la partida anterior: el factor del
        // hit-stop arranca en 1 y ApplyState (vía ApplyTimeScale) escribe la escala correcta,
        // así que el reinicio de escena deja el tiempo limpio sin escribirlo aquí.
        hitStopTimeScale = 1f;
        ApplyState(startPlayingDirectly ? GameState.Playing : GameState.MainMenu);

        // A partir de aquí todos los Awake han terminado: el HitStopManager ya existe.
        SubscribeToHitStop();
    }

    private void OnDestroy()
    {
        UnsubscribeFromHitStop();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ======================================================================
    // MICRO-PAUSA DE IMPACTO (HIT STOP)
    // ======================================================================

    /// <summary>
    /// Micro-pausa de impacto: ralentiza el juego una fracción de segundo para que un golpe
    /// conecte con peso. No cambia el estado de la partida, solo multiplica la escala de tiempo.
    ///
    /// Debe llamarla quien detecte el impacto (PlayerAttack, HealthComponent, EnemyAI...).
    /// Devuelve false si la pausa no procede, para que el llamante pueda omitir el resto del juice.
    /// </summary>
    /// <param name="duration">Segundos reales. 0 o menor usa el valor por defecto del manager (0.04s).</param>
    public bool RequestHitStop(float duration = 0f)
    {
        // El estado manda sobre el juice: en pausa, nivel o fin de partida el tiempo ya está
        // congelado, así que una micro-pausa no aportaría nada y solo ensuciaría la composición.
        if (ShouldFreezeTime(CurrentState))
        {
            return false;
        }

        HitStopManager hitStop = HitStopManager.Instance;

        if (hitStop == null)
        {
            return false;
        }

        // Si el manager ya está en pausa, su anti-spam devuelve 0 y aquí no se toca nada:
        // es el comportamiento correcto (no se encadenan pausas).
        if (hitStop.Request(duration) <= 0f)
        {
            return false;
        }

        hitStopTimeScale = hitStop.ScaleWhileStopped;
        ApplyTimeScale();
        return true;
    }

    /// <summary>Se llama al terminar la micro-pausa para devolver el tiempo a su velocidad normal.</summary>
    private void HandleHitStopEnded()
    {
        if (hitStopTimeScale == 1f)
        {
            return;
        }

        hitStopTimeScale = 1f;
        ApplyTimeScale();
    }

    private void SubscribeToHitStop()
    {
        HitStopManager hitStop = HitStopManager.Instance;

        if (hitStop == null)
        {
            return;
        }

        hitStop.OnHitStopEnded -= HandleHitStopEnded; // Evita suscripciones duplicadas
        hitStop.OnHitStopEnded += HandleHitStopEnded;
    }

    private void UnsubscribeFromHitStop()
    {
        HitStopManager hitStop = HitStopManager.Instance;

        if (hitStop != null)
        {
            hitStop.OnHitStopEnded -= HandleHitStopEnded;
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
            // Un reinicio limpia cualquier micro-pausa pendiente: la partida nueva
            // no debe arrancar con el tiempo ralentizado por un impacto de la anterior.
            hitStopTimeScale = 1f;
            ApplyTimeScale();
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
        hitStopTimeScale = 1f;
        ApplyTimeScale();
    }

    // ======================================================================
    // INTERNO
    // ======================================================================

    /// <summary>
    /// Cambia de estado validando la transición. No escribe el tiempo directamente:
    /// delega en <c>ApplyState</c>, que a su vez compone con la micro-pausa de impacto.
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

        // Único punto de escritura del tiempo: compone estado + micro-pausa de impacto.
        ApplyTimeScale();

        if (logStateChanges)
        {
            Debug.Log(
                $"<color=magenta>[GameStateController] {previous} → {newState} | timeScale = {Time.timeScale:0.##}</color>",
                this);
        }

        OnStateChanged?.Invoke(previous, newState);
    }

    /// <summary>
    /// ÚNICO punto del proyecto que escribe <see cref="Time.timeScale"/>.
    ///
    /// Compone dos factores: el estado de la partida y la micro-pausa de impacto.
    /// El estado tiene PRIORIDAD ABSOLUTA: si la partida está congelada (pausa, selección de
    /// mejora, menú o fin de partida) el resultado es 0 pase lo que pase con el hit-stop. Por eso
    /// abrir el panel de nivel en mitad de un impacto no "descongela" nada.
    /// </summary>
    private void ApplyTimeScale()
    {
        Time.timeScale = ShouldFreezeTime(CurrentState)
            ? FrozenTimeScale
            : normalTimeScale * hitStopTimeScale;
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
