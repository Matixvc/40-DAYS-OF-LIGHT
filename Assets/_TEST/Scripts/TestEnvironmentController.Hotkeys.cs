using UnityEngine;

public partial class TestEnvironmentController
{
    /// <summary>
    /// Registra los atajos del sandbox contra los comandos públicos de este componente.
    /// Nunca se reutiliza <see cref="PlayerInputReader"/>: si el sandbox compartiera sus
    /// acciones, al desregistarse dejaría al juego sin input.
    /// </summary>
    private void SubscribeHotkeys()
    {
        if (hotkeyMap == null)
        {
            hotkeyMap = FindAnyObjectByType<TestHotkeyMap>();
        }

        if (hotkeyMap == null)
        {
            Debug.LogWarning("[TestEnvironmentController] No hay TestHotkeyMap: los atajos F1-F12 no funcionan.", this);
            return;
        }

        // -= antes de += : evita suscripciones duplicadas si se llama dos veces.
        hotkeyMap.Spawn10 -= SpawnHordeSmall;
        hotkeyMap.Spawn10 += SpawnHordeSmall;
        hotkeyMap.Spawn50 -= SpawnHordeMedium;
        hotkeyMap.Spawn50 += SpawnHordeMedium;
        hotkeyMap.Spawn100 -= SpawnHordeLarge;
        hotkeyMap.Spawn100 += SpawnHordeLarge;
        hotkeyMap.KillAll -= ClearTestSpawns;
        hotkeyMap.KillAll += ClearTestSpawns;
        hotkeyMap.LevelUp -= LevelUp;
        hotkeyMap.LevelUp += LevelUp;
        hotkeyMap.ApplyUpgrade -= ApplySelectedUpgrade;
        hotkeyMap.ApplyUpgrade += ApplySelectedUpgrade;
        hotkeyMap.TogglePlayerAttack -= TogglePlayerAttack;
        hotkeyMap.TogglePlayerAttack += TogglePlayerAttack;
        hotkeyMap.ToggleEnemyAi -= ToggleEnemyAi;
        hotkeyMap.ToggleEnemyAi += ToggleEnemyAi;
        hotkeyMap.ToggleSpawner -= ToggleSpawner;
        hotkeyMap.ToggleSpawner += ToggleSpawner;
        hotkeyMap.TogglePanel -= TogglePanel;
        hotkeyMap.TogglePanel += TogglePanel;
        hotkeyMap.CycleWeapon -= CycleWeapon;
        hotkeyMap.CycleWeapon += CycleWeapon;
        hotkeyMap.ToggleWeaponSystem -= ToggleWeaponSystem;
        hotkeyMap.ToggleWeaponSystem += ToggleWeaponSystem;
        hotkeyMap.RestartScene -= RestartScene;
        hotkeyMap.RestartScene += RestartScene;
    }

    private void UnsubscribeHotkeys()
    {
        if (hotkeyMap == null)
        {
            return;
        }

        hotkeyMap.Spawn10 -= SpawnHordeSmall;
        hotkeyMap.Spawn50 -= SpawnHordeMedium;
        hotkeyMap.Spawn100 -= SpawnHordeLarge;
        hotkeyMap.KillAll -= ClearTestSpawns;
        hotkeyMap.LevelUp -= LevelUp;
        hotkeyMap.ApplyUpgrade -= ApplySelectedUpgrade;
        hotkeyMap.TogglePlayerAttack -= TogglePlayerAttack;
        hotkeyMap.ToggleEnemyAi -= ToggleEnemyAi;
        hotkeyMap.ToggleSpawner -= ToggleSpawner;
        hotkeyMap.TogglePanel -= TogglePanel;
        hotkeyMap.CycleWeapon -= CycleWeapon;
        hotkeyMap.ToggleWeaponSystem -= ToggleWeaponSystem;
        hotkeyMap.RestartScene -= RestartScene;
    }
}
