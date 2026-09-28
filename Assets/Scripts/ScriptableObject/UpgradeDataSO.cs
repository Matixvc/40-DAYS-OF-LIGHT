using UnityEngine;

/// <summary>
/// Rareza de una mejora. Se usa SOLO para presentación (color de la carta, peso del sorteo):
/// no cambia la mecánica. Empieza en <c>Common = 0</c> para que los assets creados antes
/// de esta versión se lean como "común" sin migración alguna.
/// </summary>
/// <summary>
/// Enum LEGACY de efectos v1. Ya NO se usa para aplicar nada: el sistema de mejoras es
/// 100% v2 (<see cref="StatModifierSO"/> + <see cref="StatType"/>).
///
/// Se conserva solo para leer el método <see cref="StatTypeUtility.ToStatType"/>, que traduce
/// un valor antiguo al enum nuevo durante una migración. Nada del gameplay lo invoca.
///
/// NO añadir ni reordenar miembros: los índices 0..4 están congelados porque así quedaron
/// guardados en los assets del proyecto.
/// </summary>
[System.Obsolete("El sistema v1 (UpgradeType) está retirado. Escribe el StatType directamente " +
                 "en el StatModifierSO de la mejora.")]
public enum UpgradeType
{
    IncreaseDamage = 0,
    IncreaseRange = 1,
    DecreaseAttackInterval = 2,
    IncreaseMoveSpeed = 3,
    HealPlayer = 4
}

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
    [Header("Información Básica")]
    public string upgradeName = "Nueva Mejora";
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Arma Otorgada (opcional)")]
    [Tooltip("Si se asigna, aplicar la mejora equipa (o sube de nivel) esta arma en el " +
             "WeaponController del Player. Deja vacío para mejoras de estadística puras.")]
    public WeaponDataSO grantsWeapon;

    // ==================================================================
    // AMPLIACIÓN v2 (Fase 2)
    //
    // Todos los campos nuevos van AL FINAL y con valor por defecto neutro. Unity
    // deserializa por nombre, así que los assets existentes (SO_Upgrade_Damage, ...) siguen
    // leyéndose igual: si un asset no trae estas claves, conservan el valor del inicializador.
    // ==================================================================

    [Header("Modificadores Pasivos")]
    [Tooltip("Efecto de la mejora. Cada StatModifierSO es una estadística con su operación.\n" +
             "Se admiten varios por carta: así una sola mejora puede tocar dos estadísticas.")]
    public StatModifierSO[] modifiers = new StatModifierSO[0];

    [Header("Rareza y Acumulación")]
    public UpgradeRarity rarity = UpgradeRarity.Common;

    [Tooltip("Cuántas veces puede elegirse esta mejora en la misma partida. 1 = única.")]
    [Min(1)] public int maxStacks = 1;

    [Tooltip("Peso relativo en el sorteo del LevelUpUI. 0 = no aparece nunca.")]
    [Min(0)] public int weight = 100;

    /// <summary>True si esta mejora usa el sistema de modificadores v2.</summary>
    public bool UsesModifiers => modifiers != null && modifiers.Length > 0;

    /// <summary>
    /// True si la mejora declara algún efecto: al menos un modificador o un arma.
    ///
    /// Una carta sin ninguno de los dos no hace nada, y <see cref="UpgradeManager"/> avisa
    /// en vez de gastar la elección del jugador.
    /// </summary>
    public bool HasAnyEffect => UsesModifiers || grantsWeapon != null;

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
            return string.IsNullOrEmpty(description) ? "Sin modificadores" : description;
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