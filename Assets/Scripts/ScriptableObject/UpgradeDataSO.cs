using UnityEngine;

public enum UpgradeType
{
    IncreaseDamage,
    IncreaseRange,
    DecreaseAttackInterval,
    IncreaseMoveSpeed,
    HealPlayer
}

/// <summary>
/// Rareza de una mejora. Añadida en la Fase 2 y usada SOLO para presentación (color de la
/// carta, peso del sorteo): no cambia la mecánica. Empieza en <c>Common = 0</c> para que
/// los assets creados antes de esta versión se lean como "común" sin migración alguna.
/// </summary>
public enum UpgradeRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Stats/Upgrade Data")]
public class UpgradeDataSO : ScriptableObject
{
    [Header("Información de la Mejora")]
    public string upgradeName = "Más Daño";
    [TextArea] public string description = "+20% de daño al Destello de Luz";
    public Sprite icon;

    [Header("Efecto")]
    public UpgradeType upgradeType;
    public float value = 0.2f; // <-- Nombre exacto 'value' para corregir los errores

    // ==================================================================
    // AMPLIACIÓN v2 (Fase 2)
    //
    // Todos los campos nuevos van AL FINAL y con valor por defecto neutro. Unity
    // deserializa por nombre, así que los assets existentes (SO_Upgrade_Damage, ...) siguen
    // leyéndose igual: si un asset no trae estas claves, conservan el valor del inicializador.
    // ==================================================================

    [Header("Modificadores v2 (opcional)")]
    [Tooltip("Si la lista tiene elementos, MANDA ella y se ignoran 'upgradeType' y 'value'.\n" +
             "Permite que una sola mejora toque varias estadísticas a la vez (+10% daño y +5% velocidad).")]
    public StatModifierSO[] modifiers = new StatModifierSO[0];

    [Header("Arma otorgada (opcional)")]
    [Tooltip("Si se asigna, aplicar la mejora equipa (o sube de nivel) esta arma en el WeaponController del Player.")]
    public WeaponDataSO grantsWeapon;

    [Header("Rareza y acumulación (v2)")]
    public UpgradeRarity rarity = UpgradeRarity.Common;

    [Tooltip("Cuántas veces puede elegirse esta mejora en la misma partida. 1 = única.")]
    [Min(1)] public int maxStacks = 1;

    [Tooltip("Peso relativo en el sorteo del LevelUpUI. 0 = no aparece nunca.")]
    [Min(0)] public int weight = 100;

    /// <summary>True si esta mejora usa el sistema de modificadores v2.</summary>
    public bool UsesModifiers => modifiers != null && modifiers.Length > 0;

    /// <summary>
    /// True si la mejora declara algún efecto. Una mejora sin modificadores, sin arma y con
    /// value 0 no hace nada: <see cref="UpgradeManager"/> lo avisa en lugar de gastar la elección.
    /// </summary>
    public bool HasAnyEffect => UsesModifiers
        || grantsWeapon != null
        || upgradeType == UpgradeType.HealPlayer
        || !Mathf.Approximately(value, 0f);

    /// <summary>Color de presentación de la rareza, para el título de la carta de mejora.</summary>
    public Color RarityColor
    {
        get
        {
            switch (rarity)
            {
                case UpgradeRarity.Uncommon: return new Color(0.45f, 0.85f, 0.45f);
                case UpgradeRarity.Rare: return new Color(0.40f, 0.65f, 1f);
                case UpgradeRarity.Epic: return new Color(0.75f, 0.45f, 1f);
                case UpgradeRarity.Legendary: return new Color(1f, 0.70f, 0.25f);
                default: return Color.white;
            }
        }
    }

    /// <summary>
    /// Descripción autogenerada desde los modificadores. El campo 'description' sigue siendo
    /// la fuente principal; esto es el respaldo para las mejoras v2 que se olviden de rellenarlo.
    /// </summary>
    public string BuildModifierSummary()
    {
        if (!UsesModifiers)
        {
            return string.IsNullOrEmpty(description) ? upgradeType.ToString() : description;
        }

        System.Text.StringBuilder summary = new System.Text.StringBuilder(96);

        for (int i = 0; i < modifiers.Length; i++)
        {
            if (modifiers[i] == null)
            {
                continue;
            }

            if (summary.Length > 0)
            {
                summary.Append(" - ");
            }

            summary.Append(modifiers[i].DescribeEffect());
        }

        if (grantsWeapon != null)
        {
            if (summary.Length > 0)
            {
                summary.Append(" - ");
            }

            summary.Append("Arma: ").Append(grantsWeapon.weaponName);
        }

        return summary.ToString();
    }

}