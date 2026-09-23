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

    private void Update()
    {
        // Permitir reiniciar presionando R cuando el juego termine
        if (isGameOver && Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
        }
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;
        Time.timeScale = 0f; // Pausar el combate y movimiento

        // Actualizar estadísticas de nivel
        if (gameOverStatsText != null && playerLevelSystem != null && playerLevelSystem.Data != null)
        {
            gameOverStatsText.text = $"Sobreviviste hasta el <color=#FFCC00>Nivel {playerLevelSystem.Data.currentLevel}</color>";
        }

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
            StartCoroutine(AnimateGameOverUI());
        }

        Debug.Log("<color=red>[GameManager] Fin de la partida. Presiona R o el botón para reintentar.</color>");
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
        Time.timeScale = 1f; // Restaurar la velocidad del tiempo antes de reiniciar
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}