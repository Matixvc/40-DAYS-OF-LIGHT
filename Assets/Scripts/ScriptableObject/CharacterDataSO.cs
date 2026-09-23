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

    [Header("Progreso de Nivel")]
    public int currentLevel = 1;
    public float currentXP = 0f;
    public float baseXPToNextLevel = 50f;
    public float xpMultiplier = 1.25f;

    public float GetXPToNextLevel()
    {
        return Mathf.Round(baseXPToNextLevel * Mathf.Pow(xpMultiplier, currentLevel - 1));
    }

    // --- BOTÓN MANUAL EN EL INSPECTOR ---
    [ContextMenu("Resetear a Valores Base")]
    public void ResetStats()
    {
        currentLevel = 1;
        currentXP = 0f;
        Debug.Log($"<color=cyan>[SO] {name} reseteado: Nivel 1, 0 XP.</color>");
    }
}