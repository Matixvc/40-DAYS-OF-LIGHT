using UnityEngine;

/// <summary>
/// Comportamiento de un arma. Añadido en la Fase 3.
///
/// <c>Pulse = 0</c> es deliberado: es el pulso de área que ya existía (PlayerAttack), así que
/// cualquier <see cref="WeaponDataSO"/> creado antes de esta versión se lee como Pulse y el
/// balance del juego no cambia. Los arquetipos nuevos se añaden SIEMPRE al final.
/// </summary>
public enum WeaponArchetype
{
    /// <summary>Pulso de área instantáneo alrededor del jugador. Comportamiento original.</summary>
    Pulse = 0,

    /// <summary>Objetos que orbitan al jugador y golpean al contacto.</summary>
    Orbit = 1,

    /// <summary>Disparos hacia los enemigos más cercanos, reciclados desde el ObjectPoolManager.</summary>
    Projectile = 2,

    /// <summary>Zona persistente que aplica daño por ticks.</summary>
    Aura = 3
}


[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Stats/Weapon Data")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Información del Arma")]
    public string weaponName = "Destello de Luz";

    [Header("Estadísticas del Arma")]
    public float damage = 35f;
    public float attackRange = 4.5f;
    public float attackInterval = 0.8f;

    [Header("Visuales")]
    public Color flashColor = new Color(1f, 0.9f, 0.3f, 0.5f);


    // ==================================================================
    // AMPLIACIÓN v2 (Fase 3): arquetipos de arma
    //
    // Todos los campos nuevos van AL FINAL y con valor por defecto que reproduce el
    // comportamiento original (archetype = Pulse, que es el pulso de área que ya existía).
    // Los assets creados antes de esta versión se leen igual: si el YAML no trae la clave,
    // el campo conserva el valor de su inicializador.
    // ==================================================================

    [Header("Arquetipo (v2)")]
    [Tooltip("Pulse: pulso de área alrededor del jugador (comportamiento original).\n" +
             "Orbit: objetos que giran alrededor del jugador.\n" +
             "Projectile: disparos hacia los enemigos más cercanos.\n" +
             "Aura: daño continuo en un radio permanente.")]
    public WeaponArchetype archetype = WeaponArchetype.Pulse;

    [Header("Órbita (arquetipo Orbit)")]
    [Min(1)] public int orbitCount = 3;
    [Tooltip("Distancia al centro del jugador. Se multiplica por RunStats.AreaSizeMultiplier.")]
    public float orbitRadius = 2.4f;
    [Tooltip("Velocidad angular en grados por segundo.")]
    public float orbitDegreesPerSecond = 130f;
    [Tooltip("Segundos mínimos entre dos impactos del MISMO orbe sobre el MISMO enemigo.")]
    public float orbitHitInterval = 0.4f;
    [Tooltip("Radio de golpe de cada orbe.")]
    public float orbitOrbRadius = 0.45f;

    [Header("Proyectil (arquetipo Projectile)")]
    [Min(1)] public int projectileCount = 1;
    public float projectileSpeed = 16f;
    public float projectileLifetime = 1.6f;
    [Tooltip("Radio de impacto del proyectil.")]
    public float projectileHitRadius = 0.6f;
    [Tooltip("Cuántos enemigos atraviesa antes de desvanecerse. 1 = solo el primero.")]
    [Min(1)] public int projectilePierce = 1;
    [Tooltip("Apertura del abanico cuando se disparan varios proyectiles (grados totales).")]
    public float projectileSpreadDegrees = 6f;
    [Tooltip("Prefab con WeaponProjectile. Si se deja vacío, el WeaponController crea un proyectil " +
             "procedural sin pooling (solo válido para pruebas rápidas).")]
    public GameObject projectilePrefab;

    [Header("Aura (arquetipo Aura)")]
    [Tooltip("Radio del aura. Se multiplica por RunStats.AreaSizeMultiplier.")]
    public float auraRadius = 3.6f;
    [Tooltip("Segundos entre ticks de daño del aura.")]
    public float auraTickInterval = 0.5f;

    [Header("Visual genérico (v2)")]
    [Tooltip("Prefab del visual del arma: el aro del pulso, cada orbe de una órbita o el anillo del aura.\n" +
             "Si se deja vacío se genera un primitivo procedural al equipar (visible pero sin arte).")]
    public GameObject visualPrefab;
    [Tooltip("Escala del visual (o del primitivo procedural cuando no hay prefab).")]
    [Min(0.01f)] public float visualScale = 1f;

    /// <summary>
    /// Cadencia efectiva de ESTE arma, ya con los multiplicadores de <see cref="RunStats"/>.
    /// Se lee del RunStats cuando existe y se cae a los valores del SO cuando no: así el
    /// sandbox y una escena sin RunStats siguen funcionando.
    /// </summary>
    public float GetCooldown(RunStats stats)
    {
        float baseCooldown = Mathf.Max(0.05f, attackInterval);

        if (stats == null)
        {
            return baseCooldown;
        }

        return Mathf.Max(0.05f, baseCooldown * stats.CooldownMultiplier);
    }

    /// <summary>Daño efectivo de ESTE arma según <see cref="RunStats"/> (o el del SO si no hay).</summary>
    public float GetDamage(RunStats stats)
    {
        return stats != null ? stats.AttackDamage : damage;
    }

    // --- BOTÓN MANUAL EN EL INSPECTOR ---
    [ContextMenu("Resetear a Valores Base")]
    public void ResetStats()
    {
        damage = 35f;
        attackRange = 4.5f;
        attackInterval = 0.8f;
        Debug.Log($"<color=cyan>[SO] Arma {weaponName} reseteada a sus valores base.</color>");
    }
}