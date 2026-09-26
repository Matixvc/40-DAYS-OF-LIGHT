using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú principal: elegir dificultad, jugar y salir.
/// La dificultad se guarda en <see cref="GameSessionConfig"/> y la lee <see cref="RunDirector"/>
/// al arrancar la partida (no se necesita GameStateController en esta escena).
/// </summary>
[DisallowMultipleComponent]
public class MainMenuUI : MonoBehaviour
{
    [Header("Escenas")]
    [Tooltip("Escena de gameplay que se carga al pulsar Jugar (debe estar en Build Settings).")]
    [SerializeField] private string gameplaySceneName = "Prototype";

    [Header("Botones de dificultad")]
    [SerializeField] private Button normalButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button expertButton;

    [Header("Textos de dificultad")]
    [SerializeField] private TextMeshProUGUI difficultyText;
    [Tooltip("Texto opcional con una descripción corta de la dificultad.")]
    [SerializeField] private TextMeshProUGUI difficultyDescriptionText;

    [Header("Botones principales")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [Header("Colores de selección")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color unselectedColor = Color.white;

    [Header("Depuración")]
    [SerializeField] private bool logMissingReferences = true;

    private RunDifficulty selectedDifficulty = RunDifficulty.Normal;

    private void Start()
    {
        // Al volver de una partida el tiempo puede venir congelado: el menú lo restaura.
        ResetTimeScale();

        // Recuperar la última dificultad elegida por el jugador.
        selectedDifficulty = GameSessionConfig.SelectedDifficulty;

        WireButtons();
        RefreshDifficultyVisuals();
        SelectDefaultButton();

        if (logMissingReferences)
        {
            WarnAboutMissingReferences();
        }
    }

    /// <summary>Garantiza que el menú funciona con el tiempo a velocidad normal.</summary>
    private void ResetTimeScale()
    {
        GameStateController stateController = GameStateController.Instance;

        if (stateController != null)
        {
            stateController.PrepareForSceneLoad();
            return;
        }

        // Escena de menú sin GameStateController: aquí el menú es el responsable del tiempo.
        Time.timeScale = 1f;
    }

    private void WireButtons()
    {
        WireButton(normalButton, () => SetDifficulty(RunDifficulty.Normal));
        WireButton(hardButton, () => SetDifficulty(RunDifficulty.Hard));
        WireButton(expertButton, () => SetDifficulty(RunDifficulty.Expert));
        WireButton(playButton, PlayGame);
        WireButton(quitButton, QuitGame);
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    // ======================================================================
    // ACCIONES
    // ======================================================================

    /// <summary>Cambia la dificultad y la guarda para la escena de gameplay.</summary>
    public void SetDifficulty(RunDifficulty newDifficulty)
    {
        selectedDifficulty = newDifficulty;
        GameSessionConfig.SelectedDifficulty = newDifficulty;

        RefreshDifficultyVisuals();

        Debug.Log($"[MainMenuUI] Dificultad seleccionada: {newDifficulty}", this);
    }

    /// <summary>Carga la escena de gameplay con la dificultad elegida.</summary>
    public void PlayGame()
    {
        GameSessionConfig.SelectedDifficulty = selectedDifficulty;

        if (string.IsNullOrWhiteSpace(gameplaySceneName) || !Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError(
                $"[MainMenuUI] No se puede cargar la escena de juego '{gameplaySceneName}': " +
                "revisa el nombre y que esté en Build Settings (File → Build Settings → Scenes In Build).",
                this);
            return;
        }

        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.PrepareForSceneLoad();
        }
        else
        {
            Time.timeScale = 1f;
        }

        Debug.Log($"[MainMenuUI] Iniciando partida en '{gameplaySceneName}' (Dificultad: {selectedDifficulty})…", this);
        SceneManager.LoadScene(gameplaySceneName);
    }

    /// <summary>Salir del juego (en el Editor detiene el Play mode).</summary>
    public void QuitGame()
    {
        Debug.Log("[MainMenuUI] Saliendo del juego…", this);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ======================================================================
    // VISUALES
    // ======================================================================

    private void RefreshDifficultyVisuals()
    {
        ApplyButtonColor(normalButton, selectedDifficulty == RunDifficulty.Normal);
        ApplyButtonColor(hardButton, selectedDifficulty == RunDifficulty.Hard);
        ApplyButtonColor(expertButton, selectedDifficulty == RunDifficulty.Expert);

        if (difficultyText != null)
        {
            difficultyText.text = $"Dificultad: {selectedDifficulty}";
        }

        if (difficultyDescriptionText != null)
        {
            difficultyDescriptionText.text = GetDifficultyDescription(selectedDifficulty);
        }
    }

    private void ApplyButtonColor(Button button, bool selected)
    {
        if (button == null) return;

        Image image = button.targetGraphic as Image;

        if (image != null)
        {
            image.color = selected ? selectedColor : unselectedColor;
        }
    }

    private static string GetDifficultyDescription(RunDifficulty value)
    {
        switch (value)
        {
            case RunDifficulty.Hard:
                return "Enemigos más resistentes y letales.";

            case RunDifficulty.Expert:
                return "Solo para veteranos: cualquier error se paga caro.";

            default:
                return "Combate equilibrado, ideal para conocer los 40 días.";
        }
    }

    /// <summary>Deja el foco en un botón para poder navegar con teclado y mando.</summary>
    private void SelectDefaultButton()
    {
        if (EventSystem.current == null) return;

        Button target = playButton != null ? playButton : normalButton;

        if (target != null)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }

    private void WarnAboutMissingReferences()
    {
        if (playButton == null)
        {
            Debug.LogWarning("[MainMenuUI] No hay 'Play Button' asignado.", this);
        }

        if (normalButton == null || hardButton == null || expertButton == null)
        {
            Debug.LogWarning("[MainMenuUI] Faltan botones de dificultad por asignar.", this);
        }

        if (difficultyText == null)
        {
            Debug.LogWarning("[MainMenuUI] No hay 'Difficulty Text': no se reflejará la dificultad elegida.", this);
        }
    }

    [ContextMenu("Registrar referencias del menú")]
    private void DebugReferences()
    {
        Debug.Log(
            $"[MainMenuUI:'{name}'] Escena de juego: '{gameplaySceneName}' " +
            $"(en Build Settings: {Application.CanStreamedLevelBeLoaded(gameplaySceneName)}) | " +
            $"Dificultad guardada: {GameSessionConfig.SelectedDifficulty} | " +
            $"Botones → Normal: {normalButton != null} · Hard: {hardButton != null} · Expert: {expertButton != null} · " +
            $"Jugar: {playButton != null} · Salir: {quitButton != null}",
            this);
    }
}
