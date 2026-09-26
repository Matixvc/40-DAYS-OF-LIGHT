using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Stats/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    [Header("Estadísticas del Jugador")]
    public float maxHealth = 100f;
    public float moveSpeed = 5f;
    
    [Header("Mecánica de Sprint")]
    public float sprintMultiplier = 1.6f;
    public float sprintDuration = 2.0f; // Duración máxima del sprint (en segundos)
    public float sprintCooldown = 3.0f; // Tiempo de espera antes de volver a correr

    [Header("Otros Parametros")]
    public float rotationSpeed = 10f;
    public float pickupRadius = 3f;

    [Header("Curva de Progresión (solo configuración)")]
    [Tooltip("XP necesaria para pasar del nivel 1 al 2.")]
    public float baseXPToNextLevel = 50f;
    [Tooltip("Multiplicador de XP por nivel (1.25 = +25% por nivel).")]
    public float xpMultiplier = 1.25f;

    /// <summary>
    /// XP necesaria para superar el nivel indicado (1 = primer ascenso).
    /// Es una función pura: este asset NO guarda progreso de partida.
    /// </summary>
    public float GetXPToNextLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        float baseValue = Mathf.Max(1f, baseXPToNextLevel);
        float multiplier = Mathf.Max(0.1f, xpMultiplier);
        return Mathf.Max(1f, Mathf.Round(baseValue * Mathf.Pow(multiplier, safeLevel - 1)));
    }
}