using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Panel de Victoria y resumen de la partida.
/// Se suscribe a <c>RunDirector.OnRunEnded</c> (captura las estadísticas de la run) y a
/// <c>GameStateController.OnStateChanged</c> (muestra el panel al entrar en Victory).
/// </summary>
[DisallowMultipleComponent]
public class VictoryUI : MonoBehaviour
{
    [Header("Referencias de la partida")]
    [Tooltip("Si se deja vacío se resuelve con RunDirector.Instance.")]
    [SerializeField] private RunDirector runDirector;
    [Tooltip("Si se deja vacío se busca en la escena.")]
    [SerializeField] private PlayerLevelSystem playerLevelSystem;

    [Header("Panel y animación")]
    [Tooltip("Raíz del panel (normalmente un hijo del Canvas). Si se deja vacío se usa este GameObject.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("RectTransform de la tarjeta, para el efecto de escala (opcional).")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Textos (todos opcionales)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI roundsText;

    [Header("Botones (el onClick se cablea por código)")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Escenas y salida")]
    [Tooltip("Nombre de la escena del menú principal (debe estar en Build Settings).")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Escape / Cancel del mando vuelve al menú principal cuando hay escena configurada.")]
    [SerializeField] private bool cancelReturnsToMenu = true;

    [Header("Depuración")]
    [SerializeField] private bool logMissingReferences = true;

    private GameStateController cachedStateController;
    private PlayerInputReader cachedInputReader;

    private bool victoryReached;
    private bool panelVisible;
    private Coroutine fadeCoroutine;

    // --- Datos capturados al terminar la partida ---
    private RunDifficulty finalDifficulty = RunDifficulty.Normal;
    private int finalLevel;
    private int finalRound;
    private int totalRounds;
    private float finalRunTime;

    private void Start()
    {
        ResolveReferences();
        WireButtons();
        Subscribe();

        // El panel debe estar oculto por defecto.
        SetPanelInstant(false);

        if (logMissingReferences)
        {
            WarnAboutMissingReferences();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }

    // ======================================================================
    // RESOLUCIÓN Y SUSCRIPCIONES
    // ======================================================================

    private void ResolveReferences()
    {
        if (runDirector == null)
        {
            runDirector = RunDirector.Instance;
        }

        if (playerLevelSystem == null)
        {
            playerLevelSystem = FindAnyObjectByType<PlayerLevelSystem>();
        }

        if (canvasGroup == null)
        {
            GameObject root = ResolvePanelRoot();

            if (root != null)
            {
                canvasGroup = root.GetComponent<CanvasGroup>();
            }
        }
    }

    private GameObject ResolvePanelRoot()
    {
        return panelRoot != null ? panelRoot : gameObject;
    }

    private void WireButtons()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RetryRun);
            retryButton.onClick.AddListener(RetryRun);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(GoToMainMenu);
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void Subscribe()
    {
        if (runDirector != null)
        {
            runDirector.OnRunEnded += HandleRunEnded;
        }

        cachedStateController = GameStateController.Instance;
        if (cachedStateController != null)
        {
            cachedStateController.OnStateChanged += HandleStateChanged;
        }

        cachedInputReader = PlayerInputReader.Instance;
        if (cachedInputReader != null)
        {
            cachedInputReader.PausePressed += HandleCancelPressed;
        }
    }

    private void Unsubscribe()
    {
        if (runDirector != null)
        {
            runDirector.OnRunEnded -= HandleRunEnded;
        }

        if (cachedStateController != null)
        {
            cachedStateController.OnStateChanged -= HandleStateChanged;
        }

        if (cachedInputReader != null)
        {
            cachedInputReader.PausePressed -= HandleCancelPressed;
        }
    }

    // ======================================================================
    // MANEJADORES
    // ======================================================================

    /// <summary>Captura las estadísticas de la run en el instante en que termina.</summary>
    private void HandleRunEnded(bool victory)
    {
        if (!victory) return;

        victoryReached = true;
        CaptureRunStats();
        RefreshTexts();
    }

    private void HandleStateChanged(GameState previous, GameState current)
    {
        if (current == GameState.Victory)
        {
            if (!victoryReached)
            {
                // Victoria sin RunDirector (por ejemplo, forzada en pruebas): capturamos lo que haya.
                CaptureRunStats();
                RefreshTexts();
            }

            ShowPanel();
            return;
        }

        if (current == GameState.Playing || current == GameState.MainMenu)
        {
            HidePanel();
        }
    }

    /// <summary>Escape / Cancel del mando: vuelve al menú principal si está configurado.</summary>
    private void HandleCancelPressed()
    {
        if (!panelVisible || !cancelReturnsToMenu) return;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            // Todavía no existe escena de menú: el Cancel no hace nada (sin ruido en consola).
            return;
        }

        GoToMainMenu();
    }

    private void CaptureRunStats()
    {
        if (runDirector != null)
        {
            finalDifficulty = runDirector.Difficulty;
            finalRound = runDirector.CurrentRound;
            totalRounds = runDirector.TotalRounds;
            finalRunTime = runDirector.RunElapsedTime;
        }

        if (playerLevelSystem != null)
        {
            finalLevel = playerLevelSystem.CurrentLevel;
        }
    }

    private void RefreshTexts()
    {
        if (titleText != null)
        {
            titleText.text = "¡VICTORIA!";
        }

        if (difficultyText != null)
        {
            difficultyText.text = $"Dificultad superada: {finalDifficulty}";
        }

        if (levelText != null)
        {
            levelText.text = $"Nivel final: {finalLevel}";
        }

        if (timeText != null)
        {
            timeText.text = $"Tiempo total: {FormatTime(finalRunTime)}";
        }

        if (roundsText != null)
        {
            roundsText.text = $"Días superados: {finalRound} / {totalRounds}";
        }
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    // ======================================================================
    // VISIBILIDAD DEL PANEL
    // ======================================================================

    private void SetPanelInstant(bool visible)
    {
        GameObject root = ResolvePanelRoot();

        // Si el panel es un objeto distinto, se activa/desactiva. Si es este GameObject,
        // solo se puede ocultar con el CanvasGroup (por eso se avisa si falta).
        if (root != null && root != gameObject)
        {
            root.SetActive(visible);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (panelRect != null)
        {
            panelRect.localScale = visible ? Vector3.one : Vector3.one * 0.85f;
        }

        panelVisible = visible;
    }

    private void ShowPanel()
    {
        SetPanelInstant(true);
        SelectDefaultButton();

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        // Fade en tiempo real: en Victory el juego está congelado (timeScale = 0).
        if (isActiveAndEnabled && canvasGroup != null && fadeDuration > 0f)
        {
            canvasGroup.alpha = 0f;
            fadeCoroutine = StartCoroutine(FadeRoutine());
        }
    }

    private void HidePanel()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        SetPanelInstant(false);
        ClearSelection();
    }

    private IEnumerator FadeRoutine()
    {
        if (canvasGroup == null) yield break;

        float timer = 0f;
        Vector3 startScale = Vector3.one * 0.85f;
        Vector3 targetScale = Vector3.one;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / fadeDuration);

            canvasGroup.alpha = progress;

            if (panelRect != null)
            {
                panelRect.localScale = Vector3.Lerp(startScale, targetScale, Mathf.SmoothStep(0f, 1f, progress));
            }

            yield return null;
        }

        canvasGroup.alpha = 1f;

        if (panelRect != null)
        {
            panelRect.localScale = targetScale;
        }

        fadeCoroutine = null;
    }

    /// <summary>Selecciona un botón para poder navegar con teclado y mando.</summary>
    private void SelectDefaultButton()
    {
        if (EventSystem.current == null) return;

        Button target = retryButton != null ? retryButton : mainMenuButton;

        if (target != null)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }

    private void ClearSelection()
    {
        if (EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(null);
    }

    // ======================================================================
    // BOTONES
    // ======================================================================

    /// <summary>Reintentar: vuelve a jugar desde el Día 1 (recarga la escena de partida).</summary>
    public void RetryRun()
    {
        HidePanel();

        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.RequestRestart();
        }

        Scene currentScene = SceneManager.GetActiveScene();
        Debug.Log($"[VictoryUI] Reiniciando la partida (escena '{currentScene.name}')…", this);

        SceneManager.LoadScene(currentScene.buildIndex);
    }

    /// <summary>Carga la escena del menú principal (si está configurada).</summary>
    public void GoToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName) || !Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogWarning(
                "[VictoryUI] No se puede cargar el menú: escribe el nombre de la escena en 'Main Menu Scene Name' y " +
                "asegúrate de que está en Build Settings (File → Build Settings → Scenes In Build).",
                this);
            return;
        }

        HidePanel();

        // Que el tiempo no se quede congelado en la escena destino.
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.PrepareForSceneLoad();
        }

        Debug.Log($"[VictoryUI] Cargando menú principal ('{mainMenuSceneName}')…", this);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>Salir del juego (en el Editor detiene el Play mode).</summary>
    public void QuitGame()
    {
        Debug.Log("[VictoryUI] Saliendo del juego…", this);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void WarnAboutMissingReferences()
    {
        if (canvasGroup == null && ResolvePanelRoot() == gameObject)
        {
            Debug.LogWarning(
                "[VictoryUI] Para ocultar el panel necesitas un Canvas Group en este GameObject o asignar " +
                "'Panel Root' a un objeto hijo que lo tenga.",
                this);
        }

        if (titleText == null || difficultyText == null || levelText == null || timeText == null)
        {
            Debug.LogWarning(
                "[VictoryUI] Faltan textos por asignar: los que estén vacíos no se actualizarán.",
                this);
        }

        if (retryButton == null)
        {
            Debug.LogWarning("[VictoryUI] No hay 'Retry Button' asignado: el jugador no podrá reintentar.", this);
        }
    }

    [ContextMenu("Registrar referencias del panel de victoria")]
    private void DebugReferences()
    {
        GameObject root = ResolvePanelRoot();

        Debug.Log(
            $"[VictoryUI:'{name}'] Panel: {(root != null ? root.name : "—")} | CanvasGroup: {canvasGroup != null} | " +
            $"RunDirector: {(runDirector != null ? runDirector.name : "NO ASIGNADO")} | " +
            $"PlayerLevelSystem: {(playerLevelSystem != null ? playerLevelSystem.name : "NO ASIGNADO")} | " +
            $"Botones → Reintentar: {retryButton != null} · Menú: {mainMenuButton != null} · Salir: {quitButton != null} | " +
            $"Escena de menú: {(string.IsNullOrWhiteSpace(mainMenuSceneName) ? "SIN CONFIGURAR" : mainMenuSceneName)}",
            this);
    }
}
