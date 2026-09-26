using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("Ajuste de Música en Pausa")]
    [Tooltip("Reducción de decibelios de la música en pausa (ej. -12dB la atenúa suavemente, -80dB la silencia por completo)")]
    [SerializeField] private float pauseMusicDeductionDb = -15f; 

    [Header("Panel de Estadísticas")]
    [Tooltip("Texto multilínea donde se muestran las estadísticas del jugador (UI > TextMeshPro - Text).")]
    [SerializeField] private TextMeshProUGUI statsText;
    [Tooltip("Si está activo y no asignas ningún texto, se creará uno automáticamente dentro del panel de pausa.")]
    [SerializeField] private bool autoCreateStatsText = true;

    [Header("Menú Principal")]
    [Tooltip("Escena del menú principal (debe estar en Build Settings).")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Botón opcional 'Menú principal' dentro del panel de pausa.")]
    [SerializeField] private Button mainMenuButton;

    [Header("Input (Input System)")]
    [Tooltip("Lector de input del jugador. Si se deja vacío se resuelve automáticamente.")]
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Depuración")]
    [SerializeField] private bool logPauseRequests = true;

    private float currentMusicDb = 0f;
    private bool isMusicDucked;
    private HealthComponent playerHealth;
    private TextMeshProUGUI autoCreatedStatsText;

    private void Start()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);

        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.OnStateChanged += HandleStateChanged;
        }
        else
        {
            Debug.LogError("[PauseMenu] Falta GameStateController en la escena: el menú de pausa no funcionará.", this);
        }

        ResolveInputReader();
        WireMainMenuButton();
    }

    private void WireMainMenuButton()
    {
        if (mainMenuButton == null) return;

        mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    /// <summary>Botón "Menú principal": sale de la partida y carga la escena del menú.</summary>
    public void GoToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName) || !Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                $"[PauseMenu] No se puede cargar el menú: la escena '{mainMenuSceneName}' no está configurada o no está en Build Settings.",
                this);
            return;
        }

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        // Que el tiempo no se quede congelado en la escena destino.
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.PrepareForSceneLoad();
        }

        Debug.Log($"[PauseMenu] Cargando menú principal ('{mainMenuSceneName}')…", this);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnDestroy()
    {
        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.OnStateChanged -= HandleStateChanged;
        }

        if (inputReader != null)
        {
            inputReader.PausePressed -= HandlePausePressed;
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
            Debug.LogError(
                "[PauseMenu] No se encontró PlayerInputReader: no se podrá pausar con teclado ni mando.",
                this);
            return;
        }

        inputReader.PausePressed -= HandlePausePressed;
        inputReader.PausePressed += HandlePausePressed;
    }

    /// <summary>Pausa/Reanuda disparado por el Input System (Escape o botón del mando).</summary>
    private void HandlePausePressed()
    {
        GameStateController stateController = GameStateController.Instance;
        if (stateController == null) return;

        if (stateController.IsPaused)
        {
            stateController.RequestResume();
        }
        else if (!stateController.RequestPause() && logPauseRequests && stateController.IsLevelUpActive)
        {
            // Solo es informativo cuando la causa es la selección de mejora.
            // En Victory/GameOver el bloqueo de la pausa es esperado y no se registra.
            Debug.Log("[PauseMenu] Pausa ignorada: hay una selección de mejora abierta.", this);
        }
    }

    /// <summary>Botón de pausa (llamada pública). El control del tiempo lo lleva GameStateController.</summary>
    public void PauseGame()
    {
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.RequestPause();
        }
    }

    /// <summary>Botón "Reanudar" del menú de pausa.</summary>
    public void ResumeGame()
    {
        GameStateController stateController = GameStateController.Instance;
        if (stateController == null) return;

        if (!stateController.RequestResume())
        {
            Debug.LogWarning($"[PauseMenu] No se pudo reanudar (estado actual: {stateController.CurrentState}).", this);
        }
    }

    /// <summary>El panel y el ducking de música se sincronizan con el estado global.</summary>
    private void HandleStateChanged(GameState previous, GameState current)
    {
        bool shouldShow = current == GameState.Paused;

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(shouldShow);
        }

        if (shouldShow)
        {
            DuckMusic();
            RefreshStatsText();
        }
        else
        {
            RestoreMusic();
        }
    }

    // ======================================================================
    // ESTADÍSTICAS DEL JUGADOR EN PAUSA
    // ======================================================================

    /// <summary>Rellena el texto de estadísticas con los valores de partida actuales.</summary>
    private void RefreshStatsText()
    {
        TextMeshProUGUI target = EnsureStatsText();
        if (target == null) return;

        RunStats stats = RunStats.Active;
        if (stats == null)
        {
            target.text = "ESTADÍSTICAS\n<i>No disponibles: falta RunStats en el Player.</i>";
            return;
        }

        if (playerHealth == null)
        {
            // RunStats y HealthComponent viven en el mismo GameObject del Player.
            playerHealth = stats.GetComponent<HealthComponent>();
        }

        string healthLine = playerHealth != null
            ? $"{playerHealth.CurrentHealth:0} / {playerHealth.MaxHealth:0}"
            : "—";

        target.text =
            "ESTADÍSTICAS\n" +
            $"Velocidad: {stats.MoveSpeed:0.00}\n" +
            $"Daño: {stats.AttackDamage:0.0}\n" +
            $"Rango: {stats.AttackRange:0.00}\n" +
            $"Cadencia: {stats.AttackInterval:0.00} s\n" +
            $"Vida: {healthLine}";
    }

    /// <summary>
    /// Devuelve el texto asignado en el Inspector o crea uno dentro del panel de pausa.
    /// </summary>
    private TextMeshProUGUI EnsureStatsText()
    {
        if (statsText != null)
        {
            if (!statsText.gameObject.activeSelf)
            {
                statsText.gameObject.SetActive(true);
            }

            return statsText;
        }

        if (autoCreatedStatsText != null)
        {
            return autoCreatedStatsText;
        }

        if (!autoCreateStatsText)
        {
            return null;
        }

        if (pauseMenuUI == null)
        {
            Debug.LogWarning(
                "[PauseMenu] No hay 'Pause Menu UI' asignado, así que no se puede crear el texto de estadísticas. Asígnalo en el Inspector.",
                this);
            return null;
        }

        GameObject textObject = new GameObject("StatsText_Auto", typeof(RectTransform));
        textObject.transform.SetParent(pauseMenuUI.transform, false);
        textObject.transform.SetAsLastSibling();

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 70f);
        rect.sizeDelta = new Vector2(700f, 260f);

        autoCreatedStatsText = textObject.AddComponent<TextMeshProUGUI>();
        autoCreatedStatsText.fontSize = 26f;
        autoCreatedStatsText.color = Color.white;
        autoCreatedStatsText.alignment = TextAlignmentOptions.Center;
        autoCreatedStatsText.raycastTarget = false; // No debe bloquear los botones del menú
        autoCreatedStatsText.text = string.Empty;

        Debug.Log(
            "[PauseMenu] Se creó automáticamente 'StatsText_Auto' dentro del panel de pausa. Puedes sustituirlo por tu propio texto.",
            this);

        return autoCreatedStatsText;
    }

    private void DuckMusic()
    {
        if (mainMixer == null || isMusicDucked) return;

        if (!mainMixer.GetFloat("MusicVol", out float baseDb))
        {
            Debug.LogWarning("[PauseMenu] El AudioMixer no tiene el parámetro expuesto 'MusicVol'.", this);
            return;
        }

        currentMusicDb = baseDb;
        mainMixer.SetFloat("MusicVol", baseDb + pauseMusicDeductionDb);
        isMusicDucked = true;
    }

    private void RestoreMusic()
    {
        if (mainMixer == null || !isMusicDucked) return;

        mainMixer.SetFloat("MusicVol", currentMusicDb);
        isMusicDucked = false;
    }

    public void SetMusicVolume(float value)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
        currentMusicDb = dbValue;

        if (mainMixer == null) return;

        // Si la música está atenuada por la pausa, mantenemos la atenuación.
        mainMixer.SetFloat("MusicVol", isMusicDucked ? dbValue + pauseMusicDeductionDb : dbValue);
    }

    public void SetSFXVolume(float value)
    {
        if (mainMixer != null)
        {
            float dbValue = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
            mainMixer.SetFloat("SFXVol", dbValue);
        }
    }
}