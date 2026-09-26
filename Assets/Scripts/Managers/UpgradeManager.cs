using System;
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

    [Header("Depuración")]
    [SerializeField] private bool logUpgrades = true;

    /// <summary>Se lanza cuando una mejora se aplica correctamente.</summary>
    public event Action<UpgradeDataSO> OnUpgradeApplied;

    public RunStats RunStats => runStats;

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
    /// </summary>
    /// <returns><c>true</c> si la mejora se aplicó correctamente.</returns>
    public bool ApplyUpgrade(UpgradeDataSO upgrade)
    {
        if (upgrade == null)
        {
            Debug.LogWarning("[UpgradeManager] Se intentó aplicar una mejora nula.", this);
            return false;
        }

        if (Mathf.Approximately(upgrade.value, 0f))
        {
            Debug.LogWarning(
                $"[UpgradeManager] La mejora '{upgrade.upgradeName}' tiene value = 0 y no tendrá efecto.",
                upgrade);
            return false;
        }

        bool applied;

        switch (upgrade.upgradeType)
        {
            case UpgradeType.HealPlayer:
                applied = ApplyHeal(upgrade.value);
                break;

            default:
                applied = ApplyStatUpgrade(upgrade);
                break;
        }

        if (applied)
        {
            OnUpgradeApplied?.Invoke(upgrade);
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
            Debug.Log(
                $"<color=green>[UpgradeManager] Mejora aplicada: {upgrade.upgradeName} sobre RunStats de '{runStats.gameObject.name}'</color>",
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
}