using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class TestSceneBuilder
{
    /// <summary>
    /// Estado compartido entre las fases del builder. Existe para que cada fase sea una
    /// función corta y el orden de construcción sea explícito y legible.
    /// </summary>
    internal class BuildContext
    {
        public Scene Scene;
        public TestBuildSettings Settings;

        public int UiLayer;
        public int TerrainLayer;
        public int PlayerLayer;

        // Assets resueltos
        public GameObject EnemyPrefab;
        public GameObject XpPrefab;
        public GameObject DamageNumberPrefab;
        public UpgradeDataSO[] Upgrades;

        // Armas (Fase 3)
        public WeaponDataSO[] TestWeapons;
        public GameObject WeaponProjectilePrefab;

        // Raíces
        public Transform SystemsRoot;
        public Transform ArenaRoot;
        public Transform LightingRoot;
        public Transform UiRoot;
        public GameObject Camera;

        // Objetos clave de producción
        public GameObject Player;
        public string PlayerSource = "SIN RESOLVER";
        public GameStateController StateController;
        public ObjectPoolManager PoolManager;
        public GameManager GameManager;
        public GameObject AudioManagerPlaceholder;
        public UpgradeManager UpgradeManager;
        public RunDirector RunDirector;
        public PlayerInputReader InputReader;
        public PostProcessingBootstrap PostProcessing;
        public EnemySpawner Spawner;
        public WeaponController WeaponController;

        // Sandbox
        public TestNavMeshGate NavMeshGate;
        public TestHotkeyMap HotkeyMap;
        public TestEnvironmentController TestController;
        public TestDebugPanel DebugPanel;

        // UI de juego
        public Canvas UiCanvas;
        public GameObject LevelUpPanel;
        public LevelUpUI LevelUpUi;
        public GameObject GameOverPanel;
        public CanvasGroup GameOverCanvasGroup;
        public RectTransform GameOverPanelRect;
        public TMPro.TextMeshProUGUI GameOverStatsText;
        public UnityEngine.UI.Button QuitToMenuButton;
    }

    /// <summary>
    /// Resuelve los assets que la escena necesita. Todo tiene una vía manual
    /// (TestBuildSettings) y una de autodescubrimiento: el builder nunca debe fallar
    /// por un asset sin localizar, siempre avisa y sigue.
    /// </summary>
    internal static void ResolveAssets(BuildContext context)
    {
        TestBuildSettings settings = context.Settings;

        context.EnemyPrefab = settings.enemyPrefab != null
            ? settings.enemyPrefab
            : FindPrefab("Enemy01 t:Prefab") ?? FindPrefab("Enemy t:Prefab");

        context.XpPrefab = settings.xpPrefab != null
            ? settings.xpPrefab
            : FindPrefab("XP t:Prefab") ?? FindPrefabAtPath("Assets/XP.prefab");

        context.DamageNumberPrefab = settings.damageNumberPrefab != null
            ? settings.damageNumberPrefab
            : FindPrefab("DamageNumber t:Prefab") ?? FindPrefabAtPath("Assets/DamageNumber.prefab");

        context.Upgrades = ResolveUpgrades(settings);

        if (context.EnemyPrefab == null)
        {
            Debug.LogError(
                "[TestSceneBuilder] No se encontró el prefab enemigo. Asínalo en TestBuildSettings " +
                "o crea uno; sin él el spawn masivo no tendrá nada que generar.");
        }

        if (context.Upgrades.Length == 0)
        {
            Debug.LogWarning(
                "[TestSceneBuilder] No hay UpgradeDataSO en el proyecto: el panel de nivel y el de " +
                "pruebas quedarán sin cartas que ofrecer.");
        }
    }

    private static UpgradeDataSO[] ResolveUpgrades(TestBuildSettings settings)
    {
        if (settings.testUpgrades != null && settings.testUpgrades.Length > 0)
        {
            return settings.testUpgrades;
        }

        string[] guids = AssetDatabase.FindAssets("t:UpgradeDataSO");
        List<UpgradeDataSO> upgrades = new List<UpgradeDataSO>(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            UpgradeDataSO upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (upgrade != null)
            {
                upgrades.Add(upgrade);
            }
        }

        upgrades.Sort((a, b) => string.Compare(a.upgradeName, b.upgradeName, System.StringComparison.Ordinal));
        return upgrades.ToArray();
    }

    internal static GameObject FindPrefab(string filter)
    {
        string[] guids = AssetDatabase.FindAssets(filter);

        for (int i = 0; i < guids.Length; i++)
        {
            GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    internal static GameObject FindPrefabAtPath(string path)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
