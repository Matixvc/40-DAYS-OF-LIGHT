using UnityEngine;

/// <summary>
/// Configuración de partida compartida entre escenas (menú principal → gameplay).
/// Se guarda en PlayerPrefs para recordar la elección del jugador entre sesiones.
/// </summary>
public static class GameSessionConfig
{
    private const string DifficultyKey = "Run.SelectedDifficulty";

    /// <summary>
    /// Dificultad elegida en el menú principal. La lee <see cref="RunDirector"/> al arrancar la partida.
    /// Por defecto: Normal.
    /// </summary>
    public static RunDifficulty SelectedDifficulty
    {
        get
        {
            int storedValue = PlayerPrefs.GetInt(DifficultyKey, (int)RunDifficulty.Normal);
            return (RunDifficulty)Mathf.Clamp(storedValue, (int)RunDifficulty.Normal, (int)RunDifficulty.Expert);
        }
        set
        {
            PlayerPrefs.SetInt(DifficultyKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Borra la preferencia guardada (útil para pruebas).</summary>
    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(DifficultyKey);
        PlayerPrefs.Save();
    }
}
