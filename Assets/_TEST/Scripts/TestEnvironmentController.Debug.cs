using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class TestEnvironmentController
{
    /// <summary>
    /// Devuelve las estadísticas de partida a los valores base del ScriptableObject.
    /// Se usa <c>RunStats.ResetToBase()</c>, no se tocan los assets: el sandbox nunca
    /// escribe en un ScriptableObject de configuración.
    /// </summary>
    public void ResetRunStats()
    {
        if (runStats == null)
        {
            if (logActions)
            {
                Debug.LogWarning("[TestEnvironmentController] No hay RunStats en la escena.", this);
            }

            return;
        }

        runStats.ResetToBase();

        if (logActions)
        {
            Debug.Log("[TestEnvironmentController] Estadísticas de partida restauradas a los valores base.", this);
        }
    }

    /// <summary>Atajo del panel para matar todo lo que haya alrededor del jugador.</summary>
    public void KillEnemiesAroundPlayer() => KillEnemiesInRadius(killRadius);

    public void HealPlayer()
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.Heal(playerHealth.MaxHealth);

        if (logActions)
        {
            Debug.Log("[TestEnvironmentController] Jugador curado al máximo.", this);
        }
    }

    public void ToggleGodMode() => SetGodMode(!GodMode);

    /// <summary>
    /// Invulnerabilidad para probar armas y hordas sin morir. La cura se aplica en
    /// <see cref="Update"/> y sólo cuando falta vida, para no saturar el evento de salud.
    /// </summary>
    public void SetGodMode(bool value)
    {
        GodMode = value;

        if (logActions)
        {
            Debug.Log($"[TestEnvironmentController] Modo dios: {(value ? "ACTIVADO" : "DESACTIVADO")}.", this);
        }
    }

    /// <summary>
    /// Fuerza el cierre de la ronda actual. <c>RunDirector.RoundTimeRemaining</c> tiene un
    /// setter privado, así que se usa reflexión a propósito: es preferible un hack local
    /// y explícito en la carpeta de tests que ampliar la API pública del director.
    /// </summary>
    public void ForceEndRound()
    {
        if (runDirector == null)
        {
            if (logActions)
            {
                Debug.LogWarning("[TestEnvironmentController] No hay RunDirector en la escena.", this);
            }

            return;
        }

        if (RoundTimeProperty == null || !RoundTimeProperty.CanWrite)
        {
            Debug.LogError(
                "[TestEnvironmentController] No se pudo acceder a RunDirector.RoundTimeRemaining por reflexión. " +
                "Si el nombre de la propiedad cambió, actualiza TestEnvironmentController.Debug.cs.",
                this);
            return;
        }

        RoundTimeProperty.SetValue(runDirector, 0f);

        if (logActions)
        {
            Debug.Log("[TestEnvironmentController] Fin de ronda forzado.", this);
        }
    }

    /// <summary>
    /// Reinicia la escena TEST.
    ///
    /// No reutiliza <c>GameManager.RestartGame()</c> a propósito: aquel hace
    /// <c>LoadScene(GetActiveScene().buildIndex)</c> y el índice es -1 si la escena no está
    /// en Build Settings, lo que dejaría el sandbox sin recargar. Además restaura el
    /// timeScale ANTES de nada, para que un estado congelado no impida la recarga.
    /// </summary>
    public void RestartScene()
    {
        Time.timeScale = 1f;

        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.RequestRestart();
        }

        string sceneName = SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(sceneName) || !Application.isPlaying)
        {
            Debug.LogWarning("[TestEnvironmentController] No se puede recargar fuera de Play Mode.", this);
            return;
        }

        Debug.Log("[TestEnvironmentController] Recargando la escena de pruebas...", this);
        SceneManager.LoadScene(sceneName);
    }
}
