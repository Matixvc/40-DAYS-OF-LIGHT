using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de la partida: día/ronda, dificultad, aviso de noche y temporizador.
/// Solo escucha los eventos del RunDirector; no calcula nada por su cuenta.
/// </summary>
[DisallowMultipleComponent]
public class RunHUD : MonoBehaviour
{
    [Header("Referencia")]
    [Tooltip("Si se deja vacío se resuelve con RunDirector.Instance.")]
    [SerializeField] private RunDirector runDirector;

    [Header("Textos (opcionales: se ignoran si están vacíos)")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private TextMeshProUGUI warningText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider timerSlider;

    [Header("Formato")]
    [SerializeField] private string dayPrefix = "Día";
    [SerializeField] private string nightPrefix = "Noche";
    [Tooltip("Segundos que permanece visible un aviso no persistente.")]
    [SerializeField] private float warningDuration = 3.5f;

    [Header("Depuración")]
    [SerializeField] private bool logMissingReferences = true;

    private Coroutine warningCoroutine;

    private void Start()
    {
        ResolveDirector();
        Subscribe();
        WarnAboutMissingReferences();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
            warningCoroutine = null;
        }
    }

    private void ResolveDirector()
    {
        if (runDirector == null)
        {
            runDirector = RunDirector.Instance;
        }

        if (runDirector == null && logMissingReferences)
        {
            Debug.LogError("[RunHUD] No se encontró RunDirector: el HUD no se actualizará.", this);
        }
    }

    private void Subscribe()
    {
        if (runDirector == null) return;

        runDirector.OnRunStarted += HandleRunStarted;
        runDirector.OnRoundStarted += HandleRoundStarted;
        runDirector.OnNightWarning += HandleNightWarning;
        runDirector.OnBossSpawned += HandleBossSpawned;
        runDirector.OnBossDefeated += HandleBossDefeated;
        runDirector.OnRoundTimeChanged += HandleRoundTimeChanged;
        runDirector.OnDifficultyChanged += HandleDifficultyChanged;
        runDirector.OnRunEnded += HandleRunEnded;
    }

    private void Unsubscribe()
    {
        if (runDirector == null) return;

        runDirector.OnRunStarted -= HandleRunStarted;
        runDirector.OnRoundStarted -= HandleRoundStarted;
        runDirector.OnNightWarning -= HandleNightWarning;
        runDirector.OnBossSpawned -= HandleBossSpawned;
        runDirector.OnBossDefeated -= HandleBossDefeated;
        runDirector.OnRoundTimeChanged -= HandleRoundTimeChanged;
        runDirector.OnDifficultyChanged -= HandleDifficultyChanged;
        runDirector.OnRunEnded -= HandleRunEnded;
    }

    // ======================================================================
    // MANEJADORES DE EVENTOS
    // ======================================================================

    private void HandleRunStarted(RunDifficulty difficulty, int totalRounds)
    {
        SetDifficultyText(difficulty);
        SetDayText(1, totalRounds, false);
        SetTimer(0f, 1f);
        HideWarning();
    }

    private void HandleRoundStarted(int round, bool isNight)
    {
        int total = runDirector != null ? runDirector.TotalRounds : round;
        SetDayText(round, total, isNight);
    }

    private void HandleNightWarning(int round)
    {
        ShowWarning($"¡NOCHE {round}!", new Color(1f, 0.55f, 0.15f));
    }

    private void HandleBossSpawned(int round, float bossMaxHealth)
    {
        string bossLabel = runDirector != null ? runDirector.BossName : "Jefe de Noche";
        ShowWarning($"¡APARECE {bossLabel}!", new Color(1f, 0.3f, 0.3f));
    }

    /// <summary>Mensaje distinto según la noche superada; en la noche final asume el control VictoryUI.</summary>
    private void HandleBossDefeated(int round)
    {
        int nightIndex = runDirector != null ? runDirector.CurrentNightIndex : 0;
        Color color = new Color(0.4f, 1f, 0.4f);

        switch (nightIndex)
        {
            case 1:
                ShowWarning("¡HAS VENCIDO EL PRIMER DESAFÍO!", color);
                break;

            case 2:
                ShowWarning("¡SEGUNDO DESAFÍO SUPERADO!", color);
                break;

            case 3:
                ShowWarning("¡TERCER DESAFÍO SUPERADO!", color);
                break;

            default:
                // Noche final (o sin datos): sin texto, VictoryUI toma el control de la pantalla.
                HideWarning();
                break;
        }
    }

    private void HandleRoundTimeChanged(float remaining, float duration)
    {
        SetTimer(remaining, duration);
    }

    private void HandleDifficultyChanged(RunDifficulty difficulty)
    {
        SetDifficultyText(difficulty);
    }

    /// <summary>
    /// Al terminar la partida el HUD no muestra nada: la presentación de la victoria la lleva
    /// VictoryUI y la derrota la muestra el panel de Game Over.
    /// </summary>
    private void HandleRunEnded(bool victory)
    {
        HideWarning();
    }

    // ======================================================================
    // UI
    // ======================================================================

    private void SetDayText(int round, int total, bool isNight)
    {
        if (dayText == null) return;

        string prefix = isNight ? nightPrefix : dayPrefix;
        dayText.text = $"{prefix} {round} / {total}";
    }

    private void SetDifficultyText(RunDifficulty difficulty)
    {
        if (difficultyText == null) return;

        difficultyText.text = $"Dificultad: {difficulty}";
    }

    private void SetTimer(float remaining, float duration)
    {
        if (timerText != null)
        {
            timerText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, remaining))} s";
        }

        if (timerSlider != null)
        {
            timerSlider.value = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        }
    }

    private void ShowWarning(string message, Color color, bool persistent = false)
    {
        if (warningText == null) return;

        warningText.text = message;
        warningText.color = color;
        warningText.gameObject.SetActive(true);

        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
            warningCoroutine = null;
        }

        if (persistent) return;

        warningCoroutine = StartCoroutine(HideWarningAfterDelay());
    }

    private IEnumerator HideWarningAfterDelay()
    {
        yield return new WaitForSecondsRealtime(warningDuration);
        warningCoroutine = null;

        if (warningText != null)
        {
            warningText.gameObject.SetActive(false);
        }
    }

    private void HideWarning()
    {
        if (warningText == null) return;

        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
            warningCoroutine = null;
        }

        warningText.gameObject.SetActive(false);
    }

    private void WarnAboutMissingReferences()
    {
        if (!logMissingReferences) return;

        if (dayText == null || difficultyText == null || warningText == null)
        {
            Debug.LogWarning(
                "[RunHUD] Faltan textos por asignar (Day Text / Difficulty Text / Warning Text): " +
                "esos elementos del HUD no se actualizarán.",
                this);
        }
    }

    [ContextMenu("Registrar referencias del HUD")]
    private void DebugReferences()
    {
        Debug.Log(
            $"[RunHUD:'{name}'] Director: {(runDirector != null ? runDirector.name : "NO ASIGNADO")} | " +
            $"Day: {dayText != null} | Difficulty: {difficultyText != null} | Warning: {warningText != null} | " +
            $"Timer: {timerText != null} | Slider: {timerSlider != null}",
            this);
    }
}
