using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Referencias de UI GameOver")]
    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private CanvasGroup gameOverCanvasGroup;
    [SerializeField] private RectTransform gameOverPanelTransform;
    [SerializeField] private TextMeshProUGUI gameOverStatsText;

    [Header("Referencias del Jugador")]
    [SerializeField] private PlayerLevelSystem playerLevelSystem;

    [Header("Menú principal")]
    [Tooltip("Escena del menú principal (debe estar en Build Settings).")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Botón opcional 'Menú principal' dentro del panel de Game Over.")]
    [SerializeField] private UnityEngine.UI.Button quitToMenuButton;

    [Header("Input (Input System)")]
    [Tooltip("Lector de input del jugador. Si se deja vacío se resuelve automáticamente.")]
    [SerializeField] private PlayerInputReader inputReader;

    private bool isGameOver = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.OnStateChanged += HandleStateChanged;
        }
        else
        {
            Debug.LogError(
                "[GameManager] Falta GameStateController en la escena. El fin de partida no se congelará correctamente.",
                this);
        }

        ResolveInputReader();
        WireQuitToMenuButton();

        // Garantizar que la UI de Game Over esté apagada al iniciar la partida
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.OnStateChanged -= HandleStateChanged;
        }

        if (inputReader != null)
        {
            inputReader.RestartPressed -= HandleRestartPressed;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void ResolveInputReader()
    {
        if (inputReader == null)
        {
            inputReader = PlayerInputReader.Instance;
        }

        if (inputReader == null)
        {
            Debug.LogWarning(
                "[GameManager] No se encontró PlayerInputReader: no se podrá reiniciar con teclado ni mando desde el Game Over.",
                this);
            return;
        }

        inputReader.RestartPressed -= HandleRestartPressed;
        inputReader.RestartPressed += HandleRestartPressed;
    }

    private void WireQuitToMenuButton()
    {
        if (quitToMenuButton == null) return;

        quitToMenuButton.onClick.RemoveListener(GoToMainMenu);
        quitToMenuButton.onClick.AddListener(GoToMainMenu);
    }

    /// <summary>Salir al menú principal: restaura el tiempo antes de cargar la escena.</summary>
    public void GoToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName) || !Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                $"[GameManager] No se puede cargar el menú: la escena '{mainMenuSceneName}' no está configurada o no está en Build Settings.",
                this);
            return;
        }

        isGameOver = false;

        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.PrepareForSceneLoad();
        }
        else
        {
            Time.timeScale = 1f;
        }

        Debug.Log($"[GameManager] Cargando menú principal ('{mainMenuSceneName}')…", this);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>Reinicio disparado por el Input System (R o botón del mando).</summary>
    private void HandleRestartPressed()
    {
        // Solo se reinicia desde la pantalla de Game Over.
        if (!isGameOver) return;

        RestartGame();
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;

        // Preparar el texto ANTES de cambiar de estado (la UI se muestra al entrar en GameOver).
        UpdateGameOverStatsText();

        GameStateController stateController = GameStateController.Instance;

        if (stateController == null)
        {
            // Respaldo de emergencia: sin controlador el tiempo no se congela, pero se avisa claramente.
            Debug.LogError(
                "[GameManager] No hay GameStateController en la escena: el tiempo NO se congelará al morir.",
                this);
            ShowGameOverUI();
        }
        else if (!stateController.RequestGameOver())
        {
            Debug.LogWarning(
                $"[GameManager] La transición a GameOver fue rechazada (estado actual: {stateController.CurrentState}).",
                this);
        }

        Debug.Log("<color=red>[GameManager] Fin de la partida. Presiona R o el botón para reintentar.</color>");
    }

    private void UpdateGameOverStatsText()
    {
        if (gameOverStatsText != null && playerLevelSystem != null)
        {
            gameOverStatsText.text = $"Sobreviviste hasta el <color=#FFCC00>Nivel {playerLevelSystem.CurrentLevel}</color>";
        }
    }

    /// <summary>La UI de Game Over se muestra cuando el estado global entra en GameOver.</summary>
    private void HandleStateChanged(GameState previous, GameState current)
    {
        if (current == GameState.GameOver)
        {
            ShowGameOverUI();
        }
    }

    private void ShowGameOverUI()
    {
        if (gameOverUI == null) return;

        if (!gameOverUI.activeSelf)
        {
            gameOverUI.SetActive(true);
        }

        StopAllCoroutines();
        StartCoroutine(AnimateGameOverUI());
    }

    private IEnumerator AnimateGameOverUI()
    {
        float duration = 0.5f; // Duración de la animación en segundos
        float elapsed = 0f;

        Vector3 initialScale = new Vector3(0.7f, 0.7f, 0.7f);
        Vector3 finalScale = Vector3.one;

        if (gameOverPanelTransform != null)
        {
            gameOverPanelTransform.localScale = initialScale;
        }

        while (elapsed < duration)
        {
            // Importante: Usamos unscaledDeltaTime porque Time.timeScale está en 0
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // Curva de suavizado (SmoothStep)
            t = t * t * (3f - 2f * t);

            if (gameOverCanvasGroup != null)
            {
                gameOverCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            }

            if (gameOverPanelTransform != null)
            {
                gameOverPanelTransform.localScale = Vector3.Lerp(initialScale, finalScale, t);
            }

            yield return null;
        }

        if (gameOverCanvasGroup != null) gameOverCanvasGroup.alpha = 1f;
        if (gameOverPanelTransform != null) gameOverPanelTransform.localScale = finalScale;
    }

    public void RestartGame()
    {
        isGameOver = false;

        // Devolver el control del tiempo al estado de juego antes de recargar la escena.
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.RequestRestart();
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}