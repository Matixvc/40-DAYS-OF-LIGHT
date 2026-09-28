using UnityEditor.Events;
using UnityEditor;
using UnityEngine;

public static partial class TestSceneBuilder
{
    /// <summary>Cablea el panel de nivel, el Game Over y el botón de reintento.</summary>
    private static void WireGameplayUi(BuildContext context)
    {
        if (context.LevelUpUi != null)
        {
            TestSceneBuilderUtil.SetValue(context.LevelUpUi, "upgradeManager", context.UpgradeManager);
        }

        if (context.GameManager != null)
        {
            TestSceneBuilderUtil.SetValue(context.GameManager, "gameOverUI", context.GameOverPanel);
            TestSceneBuilderUtil.SetValue(context.GameManager, "gameOverCanvasGroup", context.GameOverCanvasGroup);
            TestSceneBuilderUtil.SetValue(context.GameManager, "gameOverPanelTransform", context.GameOverPanelRect);
            TestSceneBuilderUtil.SetValue(context.GameManager, "gameOverStatsText", context.GameOverStatsText);
            TestSceneBuilderUtil.SetValue(context.GameManager, "inputReader", context.InputReader);

            if (context.Player != null)
            {
                TestSceneBuilderUtil.SetValue(context.GameManager, "playerLevelSystem",
                    context.Player.GetComponent<PlayerLevelSystem>());
                TestSceneBuilderUtil.SetValue(context.GameManager, "playerController",
                    context.Player.GetComponent<PlayerController>());
                TestSceneBuilderUtil.SetValue(context.GameManager, "playerAttack",
                    context.Player.GetComponent<PlayerAttack>());
            }

            TestSceneBuilderUtil.SetValue(context.GameManager, "logDeathSequence", false);
        }

        if (context.UpgradeManager != null && context.Player != null)
        {
            TestSceneBuilderUtil.SetValue(context.UpgradeManager, "runStats", context.Player.GetComponent<RunStats>());
            TestSceneBuilderUtil.SetValue(context.UpgradeManager, "playerHealth", context.Player.GetComponent<HealthComponent>());
            TestSceneBuilderUtil.SetValue(context.UpgradeManager, "logUpgrades", false);
        }

        // El botón de reintento llama a RestartGame, que sí usa el índice de build: por eso
        // la escena se registra en Build Settings al final del proceso.
        if (context.QuitToMenuButton != null)
        {
            context.QuitToMenuButton.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(context.QuitToMenuButton.onClick, context.GameManager.RestartGame);
        }
    }

    /// <summary>Cablea el controlador del sandbox. Puede resolver solo, pero se deja explícito.</summary>
    private static void WireSandbox(BuildContext context)
    {
        TestEnvironmentController test = context.TestController;

        if (test == null)
        {
            return;
        }

        TestSceneBuilderUtil.SetValue(test, "navMeshGate", context.NavMeshGate);
        TestSceneBuilderUtil.SetValue(test, "debugPanel", context.DebugPanel);
        TestSceneBuilderUtil.SetValue(test, "hotkeyMap", context.HotkeyMap);
        TestSceneBuilderUtil.SetValue(test, "poolManager", context.PoolManager);
        TestSceneBuilderUtil.SetValue(test, "upgradeManager", context.UpgradeManager);
        TestSceneBuilderUtil.SetValue(test, "enemySpawner", context.Spawner);
        TestSceneBuilderUtil.SetValue(test, "runDirector", context.RunDirector);
        TestSceneBuilderUtil.SetValue(test, "gameManager", context.GameManager);
        TestSceneBuilderUtil.SetValue(test, "enemyPrefab", context.EnemyPrefab);

        if (context.Player != null)
        {
            TestSceneBuilderUtil.SetValue(test, "player", context.Player.GetComponent<PlayerController>());
            TestSceneBuilderUtil.SetValue(test, "playerAttack", context.Player.GetComponent<PlayerAttack>());
            TestSceneBuilderUtil.SetValue(test, "playerHealth", context.Player.GetComponent<HealthComponent>());
            TestSceneBuilderUtil.SetValue(test, "runStats", context.Player.GetComponent<RunStats>());
            TestSceneBuilderUtil.SetValue(test, "levelSystem", context.Player.GetComponent<PlayerLevelSystem>());
            TestSceneBuilderUtil.SetValue(test, "spawnCenter", context.Player.transform);
        }

        TestSceneBuilderUtil.SetObjectArray(test, "testUpgrades", context.Upgrades);
    }

    /// <summary>
    /// Comprobación final. Un builder que falla en silencio genera escenas medio cableadas,
    /// que son mucho más caras de depurar que un error visible: aquí se lista en consola
    /// exactamente qué falta, para poder corregirlo antes de entrar en Play Mode.
    /// </summary>
    private static void Validate(BuildContext context)
    {
        int problems = 0;

        problems += Report(context.Player != null, "Player");
        problems += Report(context.EnemyPrefab != null, "Prefab enemigo");
        problems += Report(context.StateController != null, "GameStateController");
        problems += Report(context.PoolManager != null, "ObjectPoolManager");
        problems += Report(Object.FindAnyObjectByType<AudioManager>() != null, "AudioManager");
        problems += Report(Object.FindAnyObjectByType<PlayerInputReader>() != null, "PlayerInputReader");
        problems += Report(Object.FindObjectsByType<GameStateController>().Length == 1,
            "Un único GameStateController (sin singletons duplicados)");
        problems += Report(Object.FindObjectsByType<AudioManager>().Length == 1,
            "Un único AudioManager (sin singletons duplicados)");
        problems += Report(context.RunDirector != null, "RunDirector");
        problems += Report(context.Spawner != null, "EnemySpawner");
        problems += Report(context.LevelUpUi != null, "LevelUpUI");
        problems += Report(context.GameOverPanel != null, "Panel de Game Over");
        problems += Report(context.TestController != null, "TestEnvironmentController");
        problems += Report(context.DebugPanel != null, "TestDebugPanel");
        problems += Report(context.NavMeshGate != null, "TestNavMeshGate");
        problems += Report(context.Upgrades.Length > 0, "Alguna UpgradeDataSO");

        // --- Sistema de armas (Fase 3) ---
        problems += Report(context.WeaponController != null, "WeaponController en el Player");
        problems += Report(context.WeaponProjectilePrefab != null, "Prefab de proyectil de arma");
        problems += Report(context.TestWeapons != null && context.TestWeapons.Length > 0,
            "Alguna WeaponDataSO de prueba");
        problems += Report(
            context.TestController == null
            || (context.TestWeapons != null && context.TestWeapons.Length > 0),
            "Catálogo de armas inyectado en el sandbox (para F11)");

        // Cableado real en la escena (no en el contexto): si un [SerializeField] cambia de
        // nombre en producción, SetValue ya avisa, pero aquí se recoge como pieza sin resolver.
        problems += ReportWired(context.UpgradeManager, "weaponController", "UpgradeManager → WeaponController");
        problems += ReportWired(context.TestController, "weaponController", "Sandbox → WeaponController");

        if (context.Player != null)
        {
            problems += Report(context.Player.GetComponent<RunStats>() != null, "RunStats en el Player");
            problems += Report(context.Player.GetComponent<HealthComponent>() != null, "HealthComponent en el Player");
            problems += Report(context.Player.GetComponent<PlayerAttack>() != null, "PlayerAttack en el Player");
            problems += Report(context.Player.GetComponent<PlayerLevelSystem>() != null, "PlayerLevelSystem en el Player");
        }

        if (problems > 0)
        {
            Debug.LogWarning(
                $"<color=yellow>[TestSceneBuilder] Escena creada con {problems} pieza(s) sin resolver. " +
                "La lista concreta está arriba. Revisa Assets/_TEST/Editor/TestBuildSettings.asset.</color>",
                null);
        }
    }

    private static int Report(bool condition, string what)
    {
        if (condition)
        {
            return 0;
        }

        Debug.LogError($"[TestSceneBuilder] Falta: {what}.", null);
        return 1;
    }
}
