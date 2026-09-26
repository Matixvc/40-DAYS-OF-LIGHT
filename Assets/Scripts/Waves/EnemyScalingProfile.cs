using System;
using UnityEngine;

/// <summary>
/// Escalado de estadísticas que se aplica a los enemigos de una ronda.
/// Lo calcula el <see cref="RunDirector"/> (escalado por ronda × dificultad global)
/// y lo consume el <see cref="EnemySpawner"/>.
/// Es un tipo de datos puro: no modifica ScriptableObjects.
/// </summary>
[Serializable]
public struct EnemyScalingProfile
{
    [Tooltip("Multiplicador de vida máxima.")]
    public float healthMultiplier;

    [Tooltip("Multiplicador de daño de ataque.")]
    public float damageMultiplier;

    [Tooltip("Multiplicador de velocidad de movimiento.")]
    public float speedMultiplier;

    /// <summary>Perfil neutro (sin escalado).</summary>
    public static EnemyScalingProfile One => new EnemyScalingProfile
    {
        healthMultiplier = 1f,
        damageMultiplier = 1f,
        speedMultiplier = 1f
    };
}
