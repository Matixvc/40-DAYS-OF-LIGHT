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

    [Header("Secuencia de Muerte Cinematográfica")]
    [Tooltip("Controles del jugador que se desactivan al morir. Si están vacíos se resuelven automáticamente.")]
    [SerializeField] private PlayerController playerController;
    [Tooltip("Ataque automático del jugador: sin desactivarlo seguiría golpeando después de morir.")]
    [SerializeField] private PlayerAttack playerAttack;
    [Tooltip("Retardo en TIEMPO REAL (s) entre la muerte y el panel de Game Over con el juego congelado.")]
    [SerializeField, Min(0f)] private float deathSequenceDelay = 1.5f;
    [SerializeField] private bool logDeathSequence = true;

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
        ResolvePlayerReferences();
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

    /// <summary>
    /// Cachea los componentes del jugador que se apagan al morir (controles y ataque automático).
    /// Nunca se buscan por escena dentro de la secuencia de muerte.
    /// </summary>
    private void ResolvePlayerReferences()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        if (playerAttack == null)
        {
            playerAttack = FindAnyObjectByType<PlayerAttack>();
        }
    }

    /// <summary>
    /// Devuelve la música de partida. El AudioManager persiste entre escenas (DontDestroyOnLoad), así
    /// que sin esto la música de derrota seguiría sonando tras reiniciar o volver al menú.
    /// </summary>
    private void RestoreGameplayMusic()
    {
        AudioManager audio = AudioManager.Instance;

        if (audio == null) return;

        audio.RestorePlaylistMusic();
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

        RestoreGameplayMusic();

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

    /// <summary>
    /// Fin de partida: arranca la secuencia cinematográfica de muerte. NO congela el tiempo todavía:
    /// eso ocurre al final, cuando el panel de Game Over aparece.
    /// </summary>
    public void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;

        // Red de seguridad: si el Inspector quedó vacío (o el objeto se instanció en runtime),
        // se resuelve AQUÍ la referencia al jugador para que PlayDeathSequence() → Trigger 'Die'
        // se ejecute siempre y no aparezca el warning de referencia no asignada.
        if (playerController == null || playerAttack == null)
        {
            ResolvePlayerReferences();
        }

        // Preparar el texto ANTES de todo: la UI lo mostrará al entrar en GameOver.
        UpdateGameOverStatsText();

        // NOTA: Time.timeScale NO se toca aquí. La corrutina corre en tiempo REAL mientras la
        // animación de muerte y la música de derrota se reproducen; el congelado ocurre al final.
        StartCoroutine(GameOverSequenceRoutine());
    }

    /// <summary>
    /// Secuencia de muerte, en orden:
    /// 1) el jugador pierde el control y se reproduce la animación de muerte ("Die" / "Dead"),
    /// 2) suenan el SFX de muerte (2D) y la música de derrota con fundido cruzado,
    /// 3) se espera exactamente la duración de la animación en tiempo real con yield return new WaitForSecondsRealtime(),
    /// 4) ÚNICAMENTE al terminar la animación: se congela el juego (Time.timeScale = 0) y se despliega la GameOverUI.
    /// </summary>
    private IEnumerator GameOverSequenceRoutine()
    {
        PlayDeathFeedback();

        // Obtener la duración exacta de la animación de muerte del jugador (o fallback de 1.5s)
        float deathAnimDuration = playerController != null ? playerController.GetDeathAnimationDuration() : deathSequenceDelay;
        if (deathAnimDuration <= 0.1f)
        {
            deathAnimDuration = 1.5f;
        }

        Debug.Log($"[GameManager] Esperando finalización de animación de muerte: {deathAnimDuration:F2}s");

        // Espera en tiempo real mientras el personaje cae y reproduce su animación
        yield return new WaitForSecondsRealtime(deathAnimDuration);

        EnterGameOverState();
    }

    /// <summary>Deja la partida quieta y muda: controles fuera, enemigos congelados, spawn parado y audio de derrota.</summary>
    private void PlayDeathFeedback()
    {
        // 1. Jugador: Trigger 'Die' (si el Animator lo tiene) y controles/inputs desactivados.
        if (playerController != null)
        {
            playerController.PlayDeathSequence();
        }
        else if (logDeathSequence)
        {
            Debug.LogWarning(
                "[GameManager] Sin PlayerController asignado: los controles del jugador no se desactivarán al morir.",
                this);
        }

        // El ataque automático seguiría disparando desde un jugador ya muerto.
        if (playerAttack != null)
        {
            playerAttack.enabled = false;
        }

        // 2. Enemigos activos: NO se congelan artificialmente para evitar conflictos con el
        // NavMeshAgent y permitir que la caída fluya natural hasta el despliegue del Game Over.
        // FreezeActiveEnemies();

        // 3. Se cierra la partida: detiene el spawn y el jefe (y avisa a quien escuche OnRunEnded).
        StopSpawning();

        // 4. Audio: golpe de muerte en 2D y fundido hacia la música de derrota.
        AudioManager audio = AudioManager.Instance;

        if (audio != null)
        {
            audio.PlayPlayerDeathSFX();
            audio.PlayGameOverMusic();
        }
        else if (logDeathSequence)
        {
            Debug.LogWarning("[GameManager] No hay AudioManager en la escena: la muerte será muda.", this);
        }
    }

    /// <summary>
    /// Congela a todos los enemigos activos con EnemyAI.SetFrozen(true). Se busca una sola vez, en el
    /// instante de la muerte (nunca por frame).
    /// </summary>
    private void FreezeActiveEnemies()
    {
        // Overload sin FindObjectsSortMode: el que lo incluía está obsoleto en Unity 6 (CS0618).
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude);

        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].SetFrozen(true);
        }

        if (logDeathSequence)
        {
            Debug.Log($"[GameManager] Enemigos congelados al morir el jugador: {enemies.Length}.", this);
        }
    }

    /// <summary>
    /// Interrumpe la generación de enemigos. Si el RunDirector sigue activo se cierra la partida con
    /// él: FinishRun(false) para el spawner, desactiva al jefe y evita que su red de seguridad
    /// vuelva a reactivar el spawn (solo se protege mientras la partida está activa).
    /// </summary>
    private void StopSpawning()
    {
        RunDirector runDirector = RunDirector.Instance;

        if (runDirector != null && runDirector.IsRunActive)
        {
            runDirector.FinishRun(false);
            return;
        }

        EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();

        if (spawner != null)
        {
            spawner.SetSpawningEnabled(false);
        }
    }

    /// <summary>Último paso de la secuencia: panel de Game Over visible y tiempo congelado.</summary>
    private void EnterGameOverState()
    {
        GameStateController stateController = GameStateController.Instance;

        if (stateController == null)
        {
            // Respaldo de emergencia: sin controlador el tiempo no se congela, pero se avisa claramente.
            // Es el ÚNICO sitio del proyecto que escribe Time.timeScale fuera del GameStateController,
            // y solo cuando ese controlador no existe (escenas de prueba antiguas, p. ej.).
            Debug.LogError(
                "[GameManager] No hay GameStateController en la escena: el tiempo NO se congelará al morir.",
                this);

            Time.timeScale = 0f;
            ShowGameOverUI();
        }
        else if (!stateController.RequestGameOver())
        {
            Debug.LogWarning(
                $"[GameManager] La transición a GameOver fue rechazada (estado actual: {stateController.CurrentState}). Forzando despliegue de UI.",
                this);
            ShowGameOverUI();
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

        // Limpiar/reciclar todos los objetos y enemigos activos de los pools antes de reiniciar
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ClearAllPools();
        }

        // El AudioManager persiste entre escenas: hay que devolverle la música de partida.
        RestoreGameplayMusic();

        // PRIORIDAD: devolver el control del tiempo ANTES de resetear estados o recargar la escena.
        // Si la escena tardara en cargar (o algo fallara), el juego nunca quedaría congelado.
        // RequestRestart limpia además cualquier micro-pausa de impacto pendiente, así que la
        // partida nueva no arranca con el tiempo ralentizado por un golpe de la anterior.
        GameStateController stateController = GameStateController.Instance;

        if (stateController != null)
        {
            stateController.RequestRestart();
        }
        else
        {
            // Respaldo: sin controlador no hay a quién pedirle que restaure la escala.
            Time.timeScale = 1f;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}