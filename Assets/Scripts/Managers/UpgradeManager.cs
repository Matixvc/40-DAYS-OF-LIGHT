using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aplica las mejoras elegidas por el jugador.
/// No modifica los ScriptableObjects de configuración: escribe siempre en <see cref="RunStats"/>
/// (estado de partida) o en componentes de runtime como <see cref="HealthComponent"/>.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    [Header("Referencias de la partida (asignar en el Inspector)")]
    [SerializeField] private RunStats runStats;
    [SerializeField] private HealthComponent playerHealth;

    [Tooltip("Gestor de armas del jugador. Necesario para las mejoras con 'grantsWeapon'. " +
             "Si se deja vacío se resuelve en escena al vuelo.")]
    [SerializeField] private WeaponController weaponController;

    [Header("Depuración")]
    [SerializeField] private bool logUpgrades = true;

    /// <summary>Se lanza cuando una mejora se aplica correctamente.</summary>
    public event Action<UpgradeDataSO> OnUpgradeApplied;

    /// <summary>
    /// Acumulaciones por mejora aplicadas en ESTA partida. Vive aquí y no en el
    /// UpgradeDataSO porque el asset es inmutable: escribir en él contaminaría la siguiente
    /// partida. El Dictionary se consulta solo al aplicar mejoras (unas pocas veces por run),
    /// así que no importa que no sea Zero-GC.
    /// </summary>
    private readonly Dictionary<UpgradeDataSO, int> appliedStacks = new Dictionary<UpgradeDataSO, int>();

    public RunStats RunStats => runStats;
    public WeaponController Weapons => weaponController;

    private void Awake()
    {
        // Intento temprano y silencioso: el orden de Awake entre GameObjects no está garantizado.
        if (runStats == null)
        {
            runStats = RunStats.Active;
        }
    }

    private void Start()
    {
        // Aquí ya terminaron todos los Awake de la escena.
        if (runStats == null)
        {
            runStats = RunStats.Active;
        }

        if (runStats == null)
        {
            Debug.LogError(
                "[UpgradeManager] No hay RunStats asignado ni ninguna instancia activa. " +
                "Las mejoras de estadísticas no se aplicarán: añade RunStats al Player y asígnalo en el campo 'Run Stats'.",
                this);
        }
        else if (logUpgrades)
        {
            Debug.Log($"[UpgradeManager] Aplicará las mejoras sobre RunStats del GameObject '{runStats.gameObject.name}'.", this);
        }
    }

    [ContextMenu("Diagnóstico de referencias de mejora")]
    private void DebugReferences()
    {
        Debug.Log(
            $"[UpgradeManager] RunStats = {(runStats != null ? runStats.gameObject.name : "NO ASIGNADO")} | " +
            $"Player Health = {(playerHealth != null ? playerHealth.gameObject.name : "NO ASIGNADO")}",
            this);

        if (runStats != null)
        {
            runStats.LogCurrentValues("Diagnóstico desde UpgradeManager");
        }
    }

    /// <summary>
    /// Aplica el efecto real de la mejora sobre el estado de partida.
    ///
    /// Dos caminos, y solo dos:
    ///   1) v2 — la mejora declara <c>modifiers[]</c>: cada modificador se acumula en el canal
    ///      de su <see cref="StatType"/>. <c>upgradeType</c> y <c>value</c> se IGNORAN.
    ///   2) Heredado — sin modificadores: el comportamiento original intacto, para que los
    ///      assets SO_Upgrade_Damage/Range/AttackSpeed/Speed/Heal sigan funcionando igual.
    ///
    /// <c>grantsWeapon</c> es ortogonal: puede acompañar a cualquiera de los dos caminos.
    /// </summary>
    /// <returns><c>true</c> si la mejora se aplicó correctamente.</returns>
    public bool ApplyUpgrade(UpgradeDataSO upgrade)
    {
        if (upgrade == null)
        {
            Debug.LogWarning("[UpgradeManager] Se intentó aplicar una mejora nula.", this);
            return false;
        }

        // La UI de nivel elige la carta, pero el tope de acumulaciones es del manager: es el
        // único que sabe cuántas veces se ha aplicado ya esta mejora en la partida.
        if (!CanApply(upgrade))
        {
            Debug.LogWarning(
                $"[UpgradeManager] La mejora '{upgrade.upgradeName}' ya alcanzó su tope de " +
                $"{upgrade.maxStacks} acumulacion(es) y no se volverá a aplicar.",
                upgrade);
            return false;
        }

        bool applied = ApplyUpgradeEffect(upgrade);

        if (!applied)
        {
            return false;
        }

        RegisterStack(upgrade);

        if (logUpgrades)
        {
            Debug.Log(
                $"<color=green>[UpgradeManager] Mejora aplicada: {upgrade.upgradeName} " +
                $"[{upgrade.rarity}, {GetStacks(upgrade)}/{upgrade.maxStacks}]</color>",
                this);
        }

        OnUpgradeApplied?.Invoke(upgrade);
        return true;
    }

    /// <summary>Dispatcher del efecto. Devuelve false si la mejora no tenía nada que hacer.</summary>
    private bool ApplyUpgradeEffect(UpgradeDataSO upgrade)
    {
        bool applied;

        if (upgrade.UsesModifiers)
        {
            applied = ApplyModifierList(upgrade);
        }
        else if (upgrade.upgradeType == UpgradeType.HealPlayer)
        {
            applied = ApplyHeal(upgrade.value);
        }
        else
        {
            if (Mathf.Approximately(upgrade.value, 0f))
            {
                Debug.LogWarning(
                    $"[UpgradeManager] La mejora '{upgrade.upgradeName}' tiene value = 0 y no tendrá efecto.",
                    upgrade);
                return false;
            }

            applied = ApplyStatUpgrade(upgrade);
        }

        // El arma se concede aunque los modificadores fallen (o no existan): es un efecto
        // independiente y perderlo dejaría la carta inútil sin motivo.
        if (GrantWeapon(upgrade))
        {
            applied = true;
        }

        if (!applied)
        {
            Debug.LogWarning(
                $"[UpgradeManager] La mejora '{upgrade.upgradeName}' no ha producido ningún efecto: " +
                "revisa sus modificadores, su tipo y su arma.",
                upgrade);
        }

        return applied;
    }

    private bool ApplyModifierList(UpgradeDataSO upgrade)
    {
        if (runStats == null)
        {
            Debug.LogError(
                "[UpgradeManager] Falta asignar RunStats en el Inspector: los modificadores no se aplicarán.",
                this);
            return false;
        }

        bool applied = runStats.ApplyModifiers(upgrade.modifiers);

        if (applied)
        {
            PushDerivedStatsToPlayer();
        }

        return applied;
    }

    private bool ApplyStatUpgrade(UpgradeDataSO upgrade)
    {
        if (runStats == null)
        {
            Debug.LogError(
                "[UpgradeManager] Falta asignar RunStats en el Inspector. La mejora no se aplicará.",
                this);
            return false;
        }

        bool applied = runStats.ApplyStatUpgrade(upgrade.upgradeType, upgrade.value);

        if (applied && logUpgrades)
        {
            // El "mejora aplicada" lo registra ApplyUpgrade; aquí solo se deja constancia del
            // RunStats concreto que la recibió: si aparece otro GameObject, es un duplicado.
            Debug.Log(
                $"<color=cyan>[UpgradeManager] '{upgrade.upgradeName}' aplicada sobre RunStats de " +
                $"'{runStats.gameObject.name}' (camino heredado).</color>",
                this);
        }

        return applied;
    }

    /// <summary>
    /// Cura un porcentaje de la vida máxima. El valor del SO se interpreta como fracción (0.3 = 30%).
    /// </summary>
    private bool ApplyHeal(float healthPercent)
    {
        if (playerHealth == null)
        {
            Debug.LogError(
                "[UpgradeManager] Falta asignar Player Health en el Inspector. La curación no se aplicará.",
                this);
            return false;
        }

        float amount = playerHealth.MaxHealth * Mathf.Clamp01(healthPercent);
        playerHealth.Heal(amount);

        if (logUpgrades)
        {
            Debug.Log($"<color=green>[UpgradeManager] Curación aplicada: +{amount:0.0} HP</color>", this);
        }

        return true;
    }

    // ==================================================================
    // Fase 2: modificadores, acumulaciones y armas
    // ==================================================================

    /// <summary>
    /// Equipa (o sube de nivel) el arma que concede la mejora. Sin WeaponController en escena
    /// no es un error grave: se avisa y la mejora se limita a sus modificadores.
    /// </summary>
    private bool GrantWeapon(UpgradeDataSO upgrade)
    {
        if (upgrade.grantsWeapon == null)
        {
            return false;
        }

        if (weaponController == null)
        {
            weaponController = FindAnyObjectByType<WeaponController>();
        }

        if (weaponController == null)
        {
            Debug.LogWarning(
                $"[UpgradeManager] La mejora '{upgrade.upgradeName}' concede el arma " +
                $"'{upgrade.grantsWeapon.weaponName}', pero no hay WeaponController en la escena. " +
                "Añádelo al Player para que las armas nuevas funcionen.",
                this);
            return false;
        }

        return weaponController.EquipWeapon(upgrade.grantsWeapon);
    }

    /// <summary>
    /// Traslada a componentes de runtime las estadísticas que no viven solo en RunStats:
    /// vida máxima adicional y mitigación plana del daño.
    ///
    /// Se hace en un único punto en lugar de que cada modificador toque componentes: así
    /// aplicar diez mejoras seguidas no multiplica los sitios donde puede descuadrarse el estado.
    /// </summary>
    private void PushDerivedStatsToPlayer()
    {
        if (runStats == null || playerHealth == null)
        {
            return;
        }

        playerHealth.SetFlatDamageReduction(runStats.Armor);
        playerHealth.ApplyMaxHealthBonus(runStats.MaxHealthBonus);
    }

    /// <summary>
    /// True si la mejora todavía puede aplicarse. <see cref="LevelUpUI"/> la usa para no
    /// ofrecer cartas ya agotadas, y el sandbox para explicar por qué una mejora no hace nada.
    /// </summary>
    public bool CanApply(UpgradeDataSO upgrade)
    {
        if (upgrade == null)
        {
            return false;
        }

        return GetStacks(upgrade) < Mathf.Max(1, upgrade.maxStacks);
    }

    /// <summary>Cuántas veces se ha aplicado esta mejora en la partida actual.</summary>
    public int GetStacks(UpgradeDataSO upgrade)
    {
        if (upgrade == null || appliedStacks.Count == 0)
        {
            return 0;
        }

        return appliedStacks.TryGetValue(upgrade, out int stacks) ? stacks : 0;
    }

    private void RegisterStack(UpgradeDataSO upgrade)
    {
        appliedStacks.TryGetValue(upgrade, out int current);
        appliedStacks[upgrade] = current + 1;
    }

}