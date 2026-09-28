using System;
using UnityEngine;

/// <summary>
/// Estado en tiempo de ejecución (runtime) de las estadísticas de la partida.
/// Lee los valores base de los ScriptableObjects inmutables al iniciar y aplica
/// los modificadores de las mejoras SOBRE COPIAS EN MEMORIA.
/// Este script nunca modifica los assets de configuración.
/// </summary>
[DisallowMultipleComponent]
public class RunStats : MonoBehaviour
{
    [Header("Configuración base (ScriptableObjects inmutables)")]
    [SerializeField] private CharacterDataSO characterData;
    [SerializeField] private WeaponDataSO weaponData;

    [Header("Valores base de las estadísticas v2 (Fase 2)")]
    [Tooltip("Probabilidad de crítico inicial. Con 0 (valor por defecto) el daño del juego no cambia: " +
             "el crítico solo existe a partir de las mejoras que suben StatType.IncreaseCritChance.")]
    [SerializeField, Range(0f, 1f)] private float baseCritChance = 0f;
    [Tooltip("Multiplicador de daño de un golpe crítico. 1.5 = +50%.")]
    [SerializeField, Min(1f)] private float baseCritMultiplier = 1.5f;

    [Header("Depuración")]
    [SerializeField] private bool logChanges = true;

    /// <summary>Intervalo mínimo de ataque para evitar cadencias imposibles.</summary>
    private const float MinAttackInterval = 0.08f;

    /// <summary>Suelo del multiplicador de enfriamiento: nunca por debajo del 10%.</summary>
    private const float MinCooldownMultiplier = 0.1f;

    // Valores de respaldo si falta alguna referencia en el Inspector.
    private const float FallbackMoveSpeed = 5f;
    private const float FallbackSprintMultiplier = 1.6f;
    private const float FallbackSprintDuration = 2f;
    private const float FallbackSprintCooldown = 3f;
    private const float FallbackRotationSpeed = 10f;
    private const float FallbackPickupRadius = 3f;
    private const float FallbackDamage = 10f;
    private const float FallbackAttackRange = 4.5f;
    private const float FallbackAttackInterval = 0.8f;

    /// <summary>Se lanza cada vez que cambia cualquier estadística de partida.</summary>
    public event Action OnStatsChanged;

    /// <summary>
    /// Instancia activa de la partida. Permite que PlayerController, PlayerAttack y UpgradeManager
    /// encuentren EL MISMO RunStats aunque no esté en su mismo GameObject, sin búsquedas frágiles.
    /// </summary>
    public static RunStats Active { get; private set; }

    public static bool TryGetActive(out RunStats stats)
    {
        stats = Active;
        return stats != null;
    }

    public bool IsInitialized { get; private set; }

    /// <summary>True si al menos uno de los ScriptableObjects base está asignado.</summary>
    public bool HasBaseData => characterData != null || weaponData != null;

    // --- Configuración (solo lectura) ---
    public CharacterDataSO CharacterData => characterData;
    public WeaponDataSO WeaponData => weaponData;

    // --- Estadísticas de partida (valores FINALES, ya con las mejoras aplicadas) ---
    //
    // Cada propiedad se calcula sobre un canal de StatType:
    //     valor = (base + ΣAdd) * Π(1 + Percent) * Π(Multiply)
    // Los 'base*' son copias en memoria de los ScriptableObjects y se recargan en
    // ResetToBase(). Los canales arrancan a cero, así que mientras no se aplique ninguna
    // mejora el valor final es EXACTAMENTE el del SO: retrocompatibilidad garantizada.

    /// <summary>Velocidad de movimiento (base del CharacterDataSO).</summary>
    public float MoveSpeed => GetStat(StatType.IncreaseMoveSpeed, baseMoveSpeed);

    /// <summary>Radio de recogida de gemas de XP.</summary>
    public float PickupRadius => GetStat(StatType.IncreasePickupRadius, basePickupRadius);

    /// <summary>Daño por golpe del arma principal.</summary>
    public float AttackDamage => GetStat(StatType.IncreaseDamage, baseAttackDamage);

    /// <summary>Alcance del pulso / área de las armas.</summary>
    public float AttackRange => GetStat(StatType.IncreaseRange, baseAttackRange);

    /// <summary>Segundos entre ataques. Clampeado para que ninguna cadena de mejoras lo lleve a 0.</summary>
    public float AttackInterval => Mathf.Max(MinAttackInterval, GetStat(StatType.DecreaseAttackInterval, baseAttackInterval));

    // --- Estadísticas v2 (Fase 2). Todas con base 0 o 1: sin mejoras no cambian nada. ---

    /// <summary>Vida máxima ADICIONAL. <see cref="HealthComponent"/> la suma a su vida base.</summary>
    public float MaxHealthBonus => GetStat(StatType.IncreaseMaxHealth, 0f);

    /// <summary>Probabilidad de crítico (0..1). Base 0 ⇒ el daño no cambia hasta que se mejore.</summary>
    public float CritChance => Mathf.Clamp01(GetStat(StatType.IncreaseCritChance, baseCritChance));

    /// <summary>Multiplicador de daño del crítico (1.5 = +50%).</summary>
    public float CritMultiplier => Mathf.Max(1f, GetStat(StatType.IncreaseCritDamage, baseCritMultiplier));

    /// <summary>Armadura: puntos planos que se restan del daño recibido.</summary>
    public float Armor => Mathf.Max(0f, GetStat(StatType.IncreaseArmor, 0f));

    /// <summary>Vida recuperada por segundo.</summary>
    public float HealthRegenPerSecond => Mathf.Max(0f, GetStat(StatType.HealthRegen, 0f));

    /// <summary>Proyectiles ADICIONALES de las armas de arquetipo Projectile.</summary>
    public int ProjectileCountBonus => Mathf.Max(0, Mathf.RoundToInt(GetStat(StatType.IncreaseProjectileCount, 0f)));

    /// <summary>Multiplicador de tamaño de área. Base 1.</summary>
    public float AreaSizeMultiplier => Mathf.Max(0.1f, GetStat(StatType.IncreaseAreaSize, 1f));

    /// <summary>Multiplicador de duración de efectos. Base 1.</summary>
    public float DurationMultiplier => Mathf.Max(0.1f, GetStat(StatType.IncreaseDuration, 1f));

    /// <summary>Multiplicador de enfriamiento/cadencia. Base 1; &lt;1 ataca más rápido.</summary>
    public float CooldownMultiplier => Mathf.Clamp(GetStat(StatType.DecreaseCooldown, 1f), MinCooldownMultiplier, 10f);

    /// <summary>Multiplicador de XP obtenida. Base 1.</summary>
    public float XPGainMultiplier => Mathf.Max(0f, GetStat(StatType.IncreaseXPGain, 1f));

    // --- Estadísticas sin modificadores: siguen siendo campos planos ---
    public float SprintMultiplier { get; private set; }
    public float SprintDuration { get; private set; }
    public float SprintCooldown { get; private set; }
    public float RotationSpeed { get; private set; }

    // ==================================================================
    // MOTOR DE ESTADÍSTICAS (Fase 2)
    // ==================================================================

    /// <summary>
    /// Acumulador de UNA estadística. Es un struct dentro de un array preasignado:
    /// consultar el valor final no reserva memoria y aplicar una mejora tampoco.
    /// </summary>
    private struct StatChannel
    {
        /// <summary>Σ de todos los modificadores Add.</summary>
        public float AddSum;

        /// <summary>Π(1 + value) de los modificadores PercentIncrease. Empieza en 1.</summary>
        public float PercentProduct;

        /// <summary>Π(value) de los modificadores Multiply. Empieza en 1.</summary>
        public float MultiplierProduct;

        public void Reset()
        {
            AddSum = 0f;
            PercentProduct = 1f;
            MultiplierProduct = 1f;
        }

        /// <summary>Aplica el orden canónico: suma plana → porcentajes → multiplicadores.</summary>
        public float Evaluate(float baseValue)
        {
            return (baseValue + AddSum) * PercentProduct * MultiplierProduct;
        }
    }

    // Dimensionado con StatTypeUtility.Count: cero reflexión y cero asignaciones por consulta.
    // Se crea en el inicializador de campo (se ejecuta al construir la instancia, ANTES de
    // cualquier Awake) para que ningún componente cuyo Awake corra antes que el nuestro
    // pueda leer una estadística con el array a null.
    private StatChannel[] channels = CreateChannels();

    // Copias en memoria de los ScriptableObjects. Son la base sobre la que evalúan los canales.
    private float baseMoveSpeed;
    private float basePickupRadius;
    private float baseAttackDamage;
    private float baseAttackRange;
    private float baseAttackInterval;

    /// <summary>
    /// Aviso de "canal fuera de rango" emitido una sola vez por instancia: si se añade un
    /// miembro a StatType y se olvida ampliar StatTypeUtility.Count, hay que enterarse sin
    /// que la consola se inunde en cada frame.
    /// </summary>
    private bool warnedChannelOverflow;

    /// <summary>Resuelve el canal de una estadística y devuelve su valor final.</summary>
    private float GetStat(StatType stat, float baseValue)
    {
        // Lectura temprana: algún componente cuyo Awake corre antes que el nuestro. Se
        // materializan los valores base para no devolver 0 (ResetToBase no lee propiedades,
        // así que no hay recursión posible).
        if (!IsInitialized)
        {
            ResetToBase();
        }

        StatChannel[] localChannels = channels;

        if (localChannels == null || (int)stat >= localChannels.Length)
        {
            WarnChannelOverflow(stat);
            return baseValue;
        }

        return localChannels[(int)stat].Evaluate(baseValue);
    }

    /// <summary>
    /// Suma un modificador al canal correspondiente. Es el único punto donde se escriben
    /// estadísticas: así el orden de evaluación es imposible de romper desde fuera.
    /// </summary>
    private void Accumulate(StatType stat, StatOperation operation, float value)
    {
        StatChannel[] localChannels = channels;

        if (localChannels == null || (int)stat >= localChannels.Length)
        {
            WarnChannelOverflow(stat);
            return;
        }

        switch (operation)
        {
            case StatOperation.Add:
                localChannels[(int)stat].AddSum += value;
                break;

            case StatOperation.Multiply:
                localChannels[(int)stat].MultiplierProduct *= value;
                break;

            default:
                localChannels[(int)stat].PercentProduct *= 1f + value;
                break;
        }
    }

    private void WarnChannelOverflow(StatType stat)
    {
        if (warnedChannelOverflow)
        {
            return;
        }

        warnedChannelOverflow = true;

        Debug.LogError(
            $"[RunStats] StatTypeUtility.Count ({StatTypeUtility.Count}) no cubre '{stat}' ({(int)stat}). " +
            "Amplía la constante al añadir miembros a StatType: mientras tanto esas mejoras se ignorarán.",
            this);
    }

    /// <summary>Crea el array de canales en estado neutro (sin mejoras aplicadas).</summary>
    private static StatChannel[] CreateChannels()
    {
        StatChannel[] created = new StatChannel[StatTypeUtility.Count];

        for (int i = 0; i < created.Length; i++)
        {
            created[i].Reset();
        }

        return created;
    }

    /// <summary>
    /// Garantiza que el array de canales existe y cubre todo el enum. Sin la segunda
    /// comprobación, ampliar StatTypeUtility.Count en caliente (recarga de scripts con la
    /// partida en marcha) dejaría los canales nuevos sin inicializar.
    /// </summary>
    private void EnsureChannels()
    {
        if (channels == null || channels.Length < StatTypeUtility.Count)
        {
            channels = CreateChannels();
        }
    }

    /// <summary>Devuelve todos los canales a su estado neutro: sin mejoras aplicadas.</summary>
    private void ResetChannels()
    {
        for (int i = 0; i < channels.Length; i++)
        {
            channels[i].Reset();
        }
    }

    private void Awake()
    {
        RegisterAsActiveInstance();
        ResetToBase();
    }

    private void OnDestroy()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    /// <summary>
    /// Registra esta instancia para que el resto del gameplay la encuentre.
    /// Avisa si hay duplicados: es la causa más habitual de que las mejoras "no hagan nada".
    /// </summary>
    private void RegisterAsActiveInstance()
    {
        if (Active != null && Active != this)
        {
            Debug.LogWarning(
                $"[RunStats] Hay más de un RunStats activo en la escena: '{Active.gameObject.name}' y '{gameObject.name}'. " +
                "Las mejoras solo afectarán a la instancia que use UpgradeManager. Elimina el duplicado y deja RunStats solo en el Player.",
                this);

            // Si la instancia ya registrada no tiene datos y esta sí, preferimos esta.
            if (!Active.HasBaseData && HasBaseData)
            {
                Active = this;
            }

            return;
        }

        Active = this;
    }

    /// <summary>
    /// Copia los valores base de los ScriptableObjects a las estadísticas de partida.
    /// Llamar también al reiniciar una partida.
    /// </summary>
    public void ResetToBase()
    {
        EnsureChannels();
        ResetChannels();

        baseMoveSpeed = characterData != null ? characterData.moveSpeed : FallbackMoveSpeed;
        basePickupRadius = characterData != null ? characterData.pickupRadius : FallbackPickupRadius;

        baseAttackDamage = weaponData != null ? weaponData.damage : FallbackDamage;
        baseAttackRange = weaponData != null ? weaponData.attackRange : FallbackAttackRange;
        baseAttackInterval = weaponData != null ? weaponData.attackInterval : FallbackAttackInterval;

        SprintMultiplier = characterData != null ? characterData.sprintMultiplier : FallbackSprintMultiplier;
        SprintDuration = characterData != null ? characterData.sprintDuration : FallbackSprintDuration;
        SprintCooldown = characterData != null ? characterData.sprintCooldown : FallbackSprintCooldown;
        RotationSpeed = characterData != null ? characterData.rotationSpeed : FallbackRotationSpeed;

        IsInitialized = true;

        if (characterData == null)
        {
            Debug.LogWarning(
                $"[RunStats] '{gameObject.name}' no tiene Character Data asignado: se usarán valores de respaldo (Velocidad {FallbackMoveSpeed}).",
                this);
        }

        if (weaponData == null)
        {
            Debug.LogWarning(
                $"[RunStats] '{gameObject.name}' no tiene Weapon Data asignado: se usarán valores de respaldo (Daño {FallbackDamage}, Rango {FallbackAttackRange}, Cadencia {FallbackAttackInterval}).",
                this);
        }

        if (logChanges)
        {
            LogCurrentValues("Valores base cargados");
        }

        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// Aplica una mejora porcentual sobre las estadísticas de partida.
    /// </summary>
    /// <returns><c>true</c> si la mejora correspondía a una estadística aplicable.</returns>
    public bool ApplyStatUpgrade(UpgradeType type, float percent)
    {
        if (!IsInitialized)
        {
            ResetToBase();
        }

        float valueBefore;
        float valueAfter;

        switch (type)
        {
            case UpgradeType.IncreaseDamage:
                valueBefore = AttackDamage;
                Accumulate(StatType.IncreaseDamage, StatOperation.PercentIncrease, percent);
                valueAfter = AttackDamage;
                break;

            case UpgradeType.IncreaseRange:
                valueBefore = AttackRange;
                Accumulate(StatType.IncreaseRange, StatOperation.PercentIncrease, percent);
                valueAfter = AttackRange;
                break;

            case UpgradeType.DecreaseAttackInterval:
                valueBefore = AttackInterval;
                // Un porcentaje POSITIVO acorta el intervalo, por eso se acumula en negativo.
                Accumulate(StatType.DecreaseAttackInterval, StatOperation.PercentIncrease, -percent);
                valueAfter = AttackInterval;
                break;

            case UpgradeType.IncreaseMoveSpeed:
                valueBefore = MoveSpeed;
                Accumulate(StatType.IncreaseMoveSpeed, StatOperation.PercentIncrease, percent);
                valueAfter = MoveSpeed;
                break;

            default:
                // HealPlayer y futuros efectos que no son estadísticas los gestiona UpgradeManager.
                return false;
        }

        // Log obligatorio: si aquí aparece un GameObject distinto al del Player, el problema es de referencias.
        Debug.Log(
            $"<color=cyan>[RunStats:'{gameObject.name}'] {type} | ANTES: {valueBefore:0.00} → DESPUÉS: {valueAfter:0.00} (+{percent * 100f:0}%)</color>",
            this);

        LogCurrentValues($"{type} aplicada");
        OnStatsChanged?.Invoke();
        return true;
    }

    /// <summary>Imprime en consola todos los valores de partida actuales.</summary>
    public void LogCurrentValues(string context)
    {
        Debug.Log(
            $"<color=cyan>[RunStats:'{gameObject.name}'] {context} | Velocidad: {MoveSpeed:0.00} | Daño: {AttackDamage:0.0} | Rango: {AttackRange:0.00} | Cadencia: {AttackInterval:0.00}s</color>",
            this);
        LogExtendedValues(context);
    }

    [ContextMenu("Registrar valores actuales en consola")]
    private void DebugLogCurrentValues()
    {
        LogCurrentValues("Estado actual (Inspector)");
    }

    // ==================================================================
    // API v2 (Fase 2): modificadores por StatType
    // ==================================================================

    /// <summary>
    /// Aplica un <see cref="StatModifierSO"/> al canal de su estadística.
    /// </summary>
    /// <returns>
    /// <c>true</c> solo si el modificador ha cambiado algo. Devuelve <c>false</c> para
    /// <see cref="StatType.HealPlayer"/>: la curación no es una estadística y la resuelve
    /// <see cref="UpgradeManager"/> contra el <see cref="HealthComponent"/> del jugador.
    /// </returns>
    public bool ApplyModifier(StatModifierSO modifier)
    {
        if (modifier == null)
        {
            Debug.LogWarning("[RunStats] Se intentó aplicar un StatModifierSO nulo.", this);
            return false;
        }

        if (!modifier.HasEffect)
        {
            Debug.LogWarning(
                $"[RunStats] El modificador '{modifier.name}' tiene value = 0: no cambia nada.",
                modifier);
            return false;
        }

        if (modifier.stat == StatType.HealPlayer)
        {
            // No es un error: es una mejora de curación mal autorada como modificador.
            // Se avisa porque lo esperable es usar UpgradeType.HealPlayer en su lugar.
            Debug.LogWarning(
                $"[RunStats] El modificador '{modifier.name}' apunta a HealPlayer, que no es una " +
                "estadística. Usa UpgradeType.HealPlayer en el UpgradeDataSO para curar.",
                modifier);
            return false;
        }

        if (!IsInitialized)
        {
            ResetToBase();
        }

        float valueBefore = GetStat(modifier.stat, GetBaseForStat(modifier.stat));

        Accumulate(modifier.stat, modifier.operation, modifier.value);

        float valueAfter = GetStat(modifier.stat, GetBaseForStat(modifier.stat));

        if (logChanges)
        {
            Debug.Log(
                $"<color=cyan>[RunStats:'{gameObject.name}'] {modifier.stat.GetDisplayName()} " +
                $"({modifier.operation} {modifier.value:0.###}) | ANTES: {valueBefore:0.00} -> " +
                $"DESPUES: {valueAfter:0.00} [{modifier.name}]</color>",
                this);
        }

        OnStatsChanged?.Invoke();
        return true;
    }

    /// <summary>Aplica una lista completa de modificadores (los de una <see cref="UpgradeDataSO"/>).</summary>
    /// <returns><c>true</c> si al menos uno de ellos surtió efecto.</returns>
    public bool ApplyModifiers(StatModifierSO[] modifiers)
    {
        if (modifiers == null || modifiers.Length == 0)
        {
            return false;
        }

        bool anyApplied = false;

        for (int i = 0; i < modifiers.Length; i++)
        {
            if (ApplyModifier(modifiers[i]))
            {
                anyApplied = true;
            }
        }

        return anyApplied;
    }

    /// <summary>
    /// Base real (la del ScriptableObject) de una estadística. Es la única traducción
    /// StatType -> base que existe: si se añade un miembro nuevo hay que añadirlo aquí.
    /// </summary>
    private float GetBaseForStat(StatType stat)
    {
        switch (stat)
        {
            case StatType.IncreaseDamage: return baseAttackDamage;
            case StatType.IncreaseRange: return baseAttackRange;
            case StatType.DecreaseAttackInterval: return baseAttackInterval;
            case StatType.IncreaseMoveSpeed: return baseMoveSpeed;
            case StatType.IncreasePickupRadius: return basePickupRadius;
            case StatType.IncreaseCritDamage: return baseCritMultiplier;
            case StatType.IncreaseCritChance: return baseCritChance;
            default: return 0f;
        }
    }

    /// <summary>
    /// Tira el golpe crítico y devuelve el daño final.
    ///
    /// Con <c>CritChance = 0</c> (el valor por defecto) devuelve <paramref name="baseDamage"/>
    /// sin tocar: por eso conectar esto en el arma no cambia el balance actual del juego.
    /// </summary>
    public float RollDamage(float baseDamage, out bool isCritical)
    {
        isCritical = false;

        float chance = CritChance;

        if (chance <= 0f || baseDamage <= 0f)
        {
            return baseDamage;
        }

        if (UnityEngine.Random.value >= chance)
        {
            return baseDamage;
        }

        isCritical = true;
        return baseDamage * CritMultiplier;
    }

    /// <summary>
    /// Suma vida máxima y avisa a los suscriptores. La usa <see cref="UpgradeManager"/> para
    /// que las mejoras de <see cref="StatType.IncreaseMaxHealth"/> tengan efecto real.
    /// </summary>
    public void NotifyStatsChanged()
    {
        OnStatsChanged?.Invoke();
    }

    /// <summary>Imprime en consola las estadísticas v2 (las que no salen en el log heredado).</summary>
    public void LogExtendedValues(string context)
    {
        Debug.Log(
            $"<color=cyan>[RunStats:'{gameObject.name}'] {context} (v2) | Vida max +{MaxHealthBonus:0} | " +
            $"Critico: {CritChance * 100f:0.#}% x{CritMultiplier:0.00} | Armadura: {Armor:0.#} | " +
            $"Regen: {HealthRegenPerSecond:0.#}/s | Proyectiles +{ProjectileCountBonus} | " +
            $"Area x{AreaSizeMultiplier:0.00} | Duracion x{DurationMultiplier:0.00} | " +
            $"Enfriamiento x{CooldownMultiplier:0.00} | XP x{XPGainMultiplier:0.00}</color>",
            this);
    }
}
