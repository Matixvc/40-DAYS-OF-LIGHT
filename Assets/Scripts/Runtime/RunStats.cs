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

    [Header("Depuración")]
    [SerializeField] private bool logChanges = true;

    /// <summary>Intervalo mínimo de ataque para evitar cadencias imposibles.</summary>
    private const float MinAttackInterval = 0.08f;

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

    // --- Estadísticas de partida ---
    public float MoveSpeed { get; private set; }
    public float SprintMultiplier { get; private set; }
    public float SprintDuration { get; private set; }
    public float SprintCooldown { get; private set; }
    public float RotationSpeed { get; private set; }
    public float PickupRadius { get; private set; }

    public float AttackDamage { get; private set; }
    public float AttackRange { get; private set; }
    public float AttackInterval { get; private set; }

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
        MoveSpeed = characterData != null ? characterData.moveSpeed : FallbackMoveSpeed;
        SprintMultiplier = characterData != null ? characterData.sprintMultiplier : FallbackSprintMultiplier;
        SprintDuration = characterData != null ? characterData.sprintDuration : FallbackSprintDuration;
        SprintCooldown = characterData != null ? characterData.sprintCooldown : FallbackSprintCooldown;
        RotationSpeed = characterData != null ? characterData.rotationSpeed : FallbackRotationSpeed;
        PickupRadius = characterData != null ? characterData.pickupRadius : FallbackPickupRadius;

        AttackDamage = weaponData != null ? weaponData.damage : FallbackDamage;
        AttackRange = weaponData != null ? weaponData.attackRange : FallbackAttackRange;
        AttackInterval = weaponData != null ? weaponData.attackInterval : FallbackAttackInterval;

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
                AttackDamage = IncreaseByPercent(AttackDamage, percent);
                valueAfter = AttackDamage;
                break;

            case UpgradeType.IncreaseRange:
                valueBefore = AttackRange;
                AttackRange = IncreaseByPercent(AttackRange, percent);
                valueAfter = AttackRange;
                break;

            case UpgradeType.DecreaseAttackInterval:
                valueBefore = AttackInterval;
                AttackInterval = Mathf.Max(MinAttackInterval, DecreaseByPercent(AttackInterval, percent));
                valueAfter = AttackInterval;
                break;

            case UpgradeType.IncreaseMoveSpeed:
                valueBefore = MoveSpeed;
                MoveSpeed = IncreaseByPercent(MoveSpeed, percent);
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
    }

    [ContextMenu("Registrar valores actuales en consola")]
    private void DebugLogCurrentValues()
    {
        LogCurrentValues("Estado actual (Inspector)");
    }

    private static float IncreaseByPercent(float currentValue, float percent)
    {
        return currentValue + (currentValue * percent);
    }

    private static float DecreaseByPercent(float currentValue, float percent)
    {
        return currentValue - (currentValue * percent);
    }
}
