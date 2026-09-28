using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class TestEnvironmentController
{
    [Header("Mejoras de prueba")]
    [Tooltip("Mejoras que el panel puede aplicar al instante, sin pasar por la UI de LevelUp.")]
    [SerializeField] private UpgradeDataSO[] testUpgrades = new UpgradeDataSO[0];
    [Tooltip("XP que se inyecta al pedir una subida de nivel (abre el LevelUpUI real).")]
    [SerializeField, Min(1f)] private float levelUpXpBoost = 100000f;
    [SerializeField, Min(0.5f)] private float killRadius = 25f;

    // RunDirector expone RoundTimeRemaining con setter privado: es la única palanca
    // externa para forzar el cierre de una ronda sin tocar ese script de producción.
    private static readonly PropertyInfo RoundTimeProperty =
        typeof(RunDirector).GetProperty("RoundTimeRemaining", BindingFlags.Public | BindingFlags.Instance);

    private void Update()
    {
        if (!GodMode || playerHealth == null || playerHealth.IsDead)
        {
            return;
        }

        // Se cura sólo cuando falta vida: si no, dispararía OnHealthChanged cada frame.
        if (playerHealth.CurrentHealth < playerHealth.MaxHealth)
        {
            playerHealth.Heal(playerHealth.MaxHealth);
        }
    }

    /// <summary>
    /// Alterna el ataque del jugador. Con el sistema de armas nuevo activo, "atacar" son las
    /// armas: F7 apaga/enciende el WeaponController en lugar de un PlayerAttack que ya está
    /// desactivado a propósito. Sin esta redirección, F7 parecería no hacer nada.
    /// </summary>
    public void TogglePlayerAttack()
    {
        if (UsingWeaponSystem && weaponController != null)
        {
            weaponController.ToggleWeaponsActive();
            return;
        }

        SetPlayerAttackEnabled(!PlayerAttackEnabled);
    }

    public void SetPlayerAttackEnabled(bool value)
    {
        if (playerAttack == null)
        {
            return;
        }

        playerAttack.enabled = value;

        if (logActions)
        {
            Debug.Log($"[TestEnvironmentController] Ataque del jugador: {(value ? "ACTIVADO" : "DESACTIVADO")}.", this);
        }
    }

    /// <summary>
    /// Congela o reanuda TODA la IA enemiga. Usa <c>EnemyAI.SetFrozen</c>, que ya detiene
    /// el NavMeshAgent y pone la animación a cero: es la congelación "bonita" que el
    /// proyecto ya usa en la secuencia de muerte.
    /// </summary>
    public void ToggleEnemyAi() => SetEnemyAiFrozen(!EnemyAiFrozen);

    public void SetEnemyAiFrozen(bool value)
    {
        EnemyAiFrozen = value;
        PruneTestSpawns();

        for (int i = 0; i < testSpawns.Count; i++)
        {
            if (testSpawns[i] != null)
            {
                testSpawns[i].SetFrozen(value);
            }
        }

        if (logActions)
        {
            Debug.Log($"[TestEnvironmentController] IA enemiga: {(value ? "CONGELADA" : "ACTIVA")} ({testSpawns.Count} enemigos).", this);
        }
    }

    /// <summary>Activa o detiene el spawner continuo de la partida.</summary>
    public void ToggleSpawner() => SetSpawnerEnabled(!(enemySpawner != null && enemySpawner.SpawningEnabled));

    public void SetSpawnerEnabled(bool value)
    {
        if (enemySpawner == null)
        {
            if (logActions)
            {
                Debug.LogWarning("[TestEnvironmentController] No hay EnemySpawner en la escena.", this);
            }

            return;
        }

        enemySpawner.SetSpawningEnabled(value);

        if (logActions)
        {
            Debug.Log($"[TestEnvironmentController] Spawner continuo: {(value ? "ACTIVADO" : "DETENIDO")}.", this);
        }
    }

    public void TogglePanel()
    {
        if (debugPanel != null)
        {
            debugPanel.ToggleVisible();
        }
    }

    /// <summary>
    /// Sube de nivel de golpe inyectando XP. Abre el <c>LevelUpUI</c> real y congela la
    /// partida: sirve para probar el panel de mejora, no para aplicar stats al instante
    /// (para eso está <see cref="ApplySelectedUpgrade"/>).
    /// </summary>
    public void LevelUp()
    {
        if (levelSystem == null)
        {
            if (logActions)
            {
                Debug.LogWarning("[TestEnvironmentController] No hay PlayerLevelSystem en la escena.", this);
            }

            return;
        }

        levelSystem.AddXP(levelUpXpBoost);

        if (logActions)
        {
            Debug.Log($"[TestEnvironmentController] LevelUp solicitado (+{levelUpXpBoost:0} XP). Nivel actual: {levelSystem.CurrentLevel}.", this);
        }
    }

    /// <summary>
    /// Aplica la mejora seleccionada directamente sobre <see cref="UpgradeManager"/>, sin
    /// abrir la UI. Es la vía rápida para testear balance de stats sin pasar por el draft.
    /// </summary>
    public void ApplySelectedUpgrade()
    {
        if (upgradeManager == null)
        {
            if (logActions)
            {
                Debug.LogWarning("[TestEnvironmentController] No hay UpgradeManager en la escena.", this);
            }

            return;
        }

        UpgradeDataSO upgrade = GetSelectedUpgradeOrNull();

        if (upgrade == null)
        {
            if (logActions)
            {
                Debug.LogWarning("[TestEnvironmentController] No hay mejoras de prueba asignadas al panel.", this);
            }

            return;
        }

        bool applied = upgradeManager.ApplyUpgrade(upgrade);

        if (!logActions)
        {
            return;
        }

        int stacks = upgradeManager.GetStacks(upgrade);
        int limit = Mathf.Max(1, upgrade.maxStacks);

        Debug.Log(
            $"[TestEnvironmentController] Mejora '{upgrade.upgradeName}' [{upgrade.rarity}] " +
            $"({upgrade.BuildModifierSummary()}): {(applied ? "APLICADA" : "RECHAZADA")} | " +
            $"acumulaciones {stacks}/{limit}" +
            (upgrade.grantsWeapon != null ? $" | arma: {upgrade.grantsWeapon.weaponName}" : string.Empty) +
            (weaponController != null ? $" | loadout: {weaponController.GetLoadoutSummary()}" : string.Empty),
            this);
    }

    public void SelectNextUpgrade() => CycleUpgrade(1);
    public void SelectPreviousUpgrade() => CycleUpgrade(-1);

    /// <summary>Cambia la mejora seleccionada del panel. El índice es circular.</summary>
    public void CycleUpgrade(int direction)
    {
        if (TestUpgradeCount == 0)
        {
            return;
        }

        SelectedUpgradeIndex = (SelectedUpgradeIndex + direction + TestUpgradeCount) % TestUpgradeCount;
    }

    private UpgradeDataSO GetSelectedUpgradeOrNull()
    {
        if (testUpgrades == null || testUpgrades.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(SelectedUpgradeIndex, 0, testUpgrades.Length - 1);
        return testUpgrades[index];
    }
}
