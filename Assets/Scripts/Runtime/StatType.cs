/// <summary>
/// Operación matemática que aplica un <see cref="StatModifierSO"/> sobre una estadística.
///
/// Orden de evaluación dentro de <see cref="RunStats"/> (importante para predecir el
/// resultado de varias mejoras apiladas):
///   1) Se suman TODOS los <see cref="Add"/>.
///   2) Se multiplican entre sí los <see cref="PercentIncrease"/>.
///   3) Se multiplican entre sí los <see cref="Multiply"/>.
///
/// Resultado: <c>(base + Σadd) * Π(1 + percent) * Π(multiply)</c>.
/// Los <c>PercentIncrease</c> se MULTIPLICAN entre sí (no se suman) para conservar el
/// comportamiento del sistema de mejoras original: dos mejoras del +15% aplicadas en
/// cadena daban <c>x1.3225</c>, no <c>x1.30</c>.
/// </summary>
public enum StatOperation
{
    /// <summary>Suma plana: <c>base + value</c>.</summary>
    Add = 0,

    /// <summary>Incremento porcentual: <c>base * (1 + value)</c>. Con <c>value = -0.15</c> equivale a un -15%.</summary>
    PercentIncrease = 1,

    /// <summary>Multiplicador directo: <c>base * value</c>.</summary>
    Multiply = 2
}

/// <summary>
/// Estadística de partida que puede modificar un <see cref="StatModifierSO"/>.
///
/// REGLA DE ORO (retrocompatibilidad): los cinco primeros miembros existen, en este mismo
/// orden, desde la versión original del enum <see cref="UpgradeType"/>. Unity serializa los
/// enums como su índice, así que <b>NUNCA</b> se reordenan ni se renombran: cualquier
/// miembro nuevo se añade SIEMPRE al final. Un asset guardado con <c>upgradeType: 2</c>
/// debe seguir resolviendo a <c>DecreaseAttackInterval</c> después de esta actualización.
///
/// Los nombres se mantienen idénticos a <see cref="UpgradeType"/> a propósito: hace que la
/// conversión entre ambos (ver <see cref="StatTypeUtility.ToStatType"/>) sea obvia de leer.
/// </summary>
public enum StatType
{
    // ==================================================================
    // BLOQUE HEREDADO (índices 0-4). NO REORDENAR: hay assets serializados
    // que dependen de estos índices exactos.
    // ==================================================================

    /// <summary>Daño por golpe del arma principal.</summary>
    IncreaseDamage = 0,

    /// <summary>Radio de alcance del pulso / del área de las armas.</summary>
    IncreaseRange = 1,

    /// <summary>Segundos entre ataques. Un porcentaje positivo lo REDUCE.</summary>
    DecreaseAttackInterval = 2,

    /// <summary>Velocidad de movimiento del jugador.</summary>
    IncreaseMoveSpeed = 3,

    /// <summary>No es una estadística: lo gestiona <see cref="UpgradeManager"/> con la vida del jugador.</summary>
    HealPlayer = 4,

    // ==================================================================
    // AMPLIACIÓN v2 (Fase 2). TODO LO NUEVO VA AQUÍ HACIA ABAJO.
    // ==================================================================

    /// <summary>Vida máxima ADICIONAL del jugador (canal aditivo sobre base 0).</summary>
    IncreaseMaxHealth = 5,

    /// <summary>Radio de recogida de gemas de XP.</summary>
    IncreasePickupRadius = 6,

    /// <summary>Probabilidad de golpe crítico (0..1). Arranca en 0: sin mejoras, el daño no cambia.</summary>
    IncreaseCritChance = 7,

    /// <summary>Multiplicador de daño de un golpe crítico (1.5 = +50%).</summary>
    IncreaseCritDamage = 8,

    /// <summary>Armadura: reduce el daño recibido en puntos planos.</summary>
    IncreaseArmor = 9,

    /// <summary>Regeneración de vida por segundo.</summary>
    HealthRegen = 10,

    /// <summary>Proyectiles ADICIONALES en las armas de arquetipo Projectile.</summary>
    IncreaseProjectileCount = 11,

    /// <summary>Multiplicador de tamaño de área (pulso, aura, radio de órbita).</summary>
    IncreaseAreaSize = 12,

    /// <summary>Multiplicador de duración de efectos (proyectiles, aura, órbitas).</summary>
    IncreaseDuration = 13,

    /// <summary>Multiplicador de cadencia: &lt;1 significa atacar más rápido.</summary>
    DecreaseCooldown = 14,

    /// <summary>Multiplicador de XP obtenida de las gemas.</summary>
    IncreaseXPGain = 15
}

/// <summary>
/// Utilidades de <see cref="StatType"/>: conversión desde el enum heredado y textos de panel.
/// Vive en el mismo archivo que el enum para que añadir un miembro nuevo obligue a mirar
/// (y completar) estas tablas.
/// </summary>
public static class StatTypeUtility
{
    /// <summary>
    /// Número de valores del enum, para dimensionar los arrays de canales de <see cref="RunStats"/>
    /// sin usar reflexión. Si se añade un miembro nuevo hay que subir este número:
    /// el guardia de <c>RunStats.Awake</c> avisa en consola si se queda corto.
    /// </summary>
    public const int Count = 16;

    /// <summary>
    /// Traduce una mejora heredada al enum nuevo. Los cinco primeros coinciden por índice,
    /// así que la conversión es una proyección directa; el <c>default</c> cubre valores
    /// corruptos o futuros sin lanzar excepciones en mitad de una partida.
    /// </summary>
    [System.Obsolete("El sistema v1 (UpgradeType) se eliminó. Usa StatModifierSO: escribe el StatType directamente. " +
                     "Este método se conserva solo para leer assets antiguos durante una migración.")]
    public static StatType ToStatType(this UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.IncreaseDamage: return StatType.IncreaseDamage;
            case UpgradeType.IncreaseRange: return StatType.IncreaseRange;
            case UpgradeType.DecreaseAttackInterval: return StatType.DecreaseAttackInterval;
            case UpgradeType.IncreaseMoveSpeed: return StatType.IncreaseMoveSpeed;
            case UpgradeType.HealPlayer: return StatType.HealPlayer;
            default: return (StatType)(int)type;
        }
    }

    /// <summary>Nombre legible de la estadística, para el panel de depuración y los logs.</summary>
    public static string GetDisplayName(this StatType stat)
    {
        switch (stat)
        {
            case StatType.IncreaseDamage: return "Daño";
            case StatType.IncreaseRange: return "Rango";
            case StatType.DecreaseAttackInterval: return "Cadencia";
            case StatType.IncreaseMoveSpeed: return "Velocidad";
            case StatType.HealPlayer: return "Curación";
            case StatType.IncreaseMaxHealth: return "Vida máxima";
            case StatType.IncreasePickupRadius: return "Radio de recogida";
            case StatType.IncreaseCritChance: return "Prob. crítico";
            case StatType.IncreaseCritDamage: return "Daño crítico";
            case StatType.IncreaseArmor: return "Armadura";
            case StatType.HealthRegen: return "Regeneración";
            case StatType.IncreaseProjectileCount: return "Proyectiles";
            case StatType.IncreaseAreaSize: return "Área";
            case StatType.IncreaseDuration: return "Duración";
            case StatType.DecreaseCooldown: return "Enfriamiento";
            case StatType.IncreaseXPGain: return "Ganancia de XP";
            default: return stat.ToString();
        }
    }

    /// <summary>
    /// Formatea el efecto de un modificador para mostrarlo en una carta de mejora o en un log.
    /// La cadencia y el enfriamiento se imprimen en negativo porque su convención es
    /// "más valor = mejor", pero el jugador lee "atacar más rápido" como un -X%.
    /// </summary>
    public static string DescribeEffect(this StatType stat, StatOperation operation, float value)
    {
        switch (operation)
        {
            case StatOperation.Add:
                return $"+{value:0.##} {GetDisplayName(stat)}";

            case StatOperation.Multiply:
                return $"x{value:0.##} {GetDisplayName(stat)}";

            default:
                bool inverted = stat == StatType.DecreaseAttackInterval || stat == StatType.DecreaseCooldown;
                float shown = inverted ? -value : value;
                return $"{shown * 100f:+0;-0;0}% {GetDisplayName(stat)}";
        }
    }
}
