using UnityEngine;

/// <summary>
/// Modificador reutilizable de una estadística de partida (Fase 2).
///
/// Por qué un ScriptableObject y no un struct serializado dentro de <see cref="UpgradeDataSO"/>:
/// el mismo efecto (+20% de daño) se repite en varias mejoras y en varias rarezas. Como asset
/// existe una sola vez, se edita en un único sitio y las cartas de mejora lo referencian.
///
/// IMPORTANTE: este asset es INMUTABLE en runtime. Nunca se escribe en él durante la partida;
/// <see cref="RunStats"/> guarda los acumuladores en memoria. Es la misma regla que ya siguen
/// <see cref="WeaponDataSO"/> y <see cref="CharacterDataSO"/>.
/// </summary>
[CreateAssetMenu(fileName = "NewStatModifier", menuName = "Stats/Stat Modifier")]
public class StatModifierSO : ScriptableObject
{
    [Header("Estadística afectada")]
    [Tooltip("Debe existir en StatType. Los cinco primeros valores son los heredados de UpgradeType.")]
    public StatType stat = StatType.IncreaseDamage;

    [Header("Operación")]
    public StatOperation operation = StatOperation.PercentIncrease;

    [Header("Valor")]
    [Tooltip("Porcentaje (0.2 = +20%), multiplicador (1.25 = x1.25) o suma plana, según la operación.")]
    public float value = 0.2f;

    /// <summary>
    /// Aplica el modificador a un valor base. No muta el asset: es una función pura, así que
    /// se puede usar tanto para calcular el valor final como para previsualizarlo en la UI.
    /// </summary>
    public float Apply(float baseValue)
    {
        switch (operation)
        {
            case StatOperation.Add:
                return baseValue + value;

            case StatOperation.Multiply:
                return baseValue * value;

            default:
                return baseValue * (1f + value);
        }
    }

    /// <summary>Texto corto del efecto ("+20% Daño"), para cartas de mejora y logs del sandbox.</summary>
    public string DescribeEffect() => stat.DescribeEffect(operation, value);

    /// <summary>
    /// Un modificador con valor 0 no hace nada: quien lo detecte (UpgradeManager) puede
    /// avisar en consola en lugar de aplicar silenciosamente una mejora vacía.
    /// </summary>
    public bool HasEffect => !Mathf.Approximately(value, 0f);

    /// <summary>Restaura los valores por defecto desde el menú contextual del Inspector.</summary>
    [ContextMenu("Resetear a Valores Base")]
    public void ResetToDefault()
    {
        stat = StatType.IncreaseDamage;
        operation = StatOperation.PercentIncrease;
        value = 0.2f;

        Debug.Log($"[SO] Modificador '{name}' reseteado a sus valores base (+20% Daño).", this);
    }

    private void OnValidate()
    {
        // Un multiplicador 0 anularía la estadística para siempre y es casi siempre un error
        // de tecleo, no una intención: se avisa sin corregirlo, para no romper diseños legítimos.
        if (operation == StatOperation.Multiply && Mathf.Approximately(value, 0f))
        {
            Debug.LogWarning(
                $"[StatModifierSO] '{name}' usa la operación Multiply con value = 0: anularía por " +
                "completo la estadística. ¿Querías Add o PercentIncrease?",
                this);
        }
    }
}
