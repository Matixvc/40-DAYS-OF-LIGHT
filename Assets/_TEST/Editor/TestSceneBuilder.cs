using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Generador de la escena de pruebas <c>Assets/_TEST/TEST.unity</c>.
///
/// Por qué un generador y no una escena hecha a mano: la escena arrastra ~30 referencias
/// cruzadas entre producción (PlayerLevelSystem → LevelUpUI → UpgradeManager → RunStats,
/// RunDirector → EnemySpawner → Player...). Cablearlas a mano es frágil y no es
/// reproducible; este generador es idempotente y se puede volver a ejecutar cuando cambie
/// un script de producción.
///
/// IMPORTANTE: no modifica ningún script del juego. Los campos privados de producción se
/// cablean con SerializedObject, que sólo escribe en la escena que se está construyendo.
///
/// Modo batch (verificación en CI o por consola):
///   Unity.exe -batchmode -quit -projectPath "..." -executeMethod TestSceneBuilder.BuildTestScene
/// </summary>
public static partial class TestSceneBuilder
{
    public const string TestScenePath = "Assets/_TEST/TEST.unity";
    public const string PrototypeScenePath = "Assets/Scenes/Prototype.unity";

    private const string MenuRoot = "Tools/40 Days of Light/";

    [MenuItem(MenuRoot + "Construir escena TEST", false, 0)]
    public static void BuildTestSceneMenu()
    {
        BuildTestScene();
    }

    [MenuItem(MenuRoot + "Ajustes de la escena TEST", false, 20)]
    public static void SelectBuildSettings()
    {
        TestBuildSettings settings = TestBuildSettings.GetOrCreate();
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    [MenuItem(MenuRoot + "Quitar TEST de Build Settings", false, 21)]
    public static void RemoveFromBuildSettings()
    {
        List<EditorBuildSettingsScene> remaining = new List<EditorBuildSettingsScene>();

        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (entry.path != TestScenePath)
            {
                remaining.Add(entry);
            }
        }

        EditorBuildSettings.scenes = remaining.ToArray();
        Debug.Log("[TestSceneBuilder] Assets/_TEST/TEST.unity retirada de Build Settings.");
    }

    /// <summary>Construye (o reconstruye) la escena TEST completa.</summary>
    public static void BuildTestScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        TestBuildSettings settings = TestBuildSettings.GetOrCreate();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildContext context = new BuildContext
        {
            Scene = scene,
            Settings = settings,
            UiLayer = TestSceneBuilderUtil.LayerIndex("UI"),
            TerrainLayer = TestSceneBuilderUtil.LayerIndex("Terreno"),
            PlayerLayer = TestSceneBuilderUtil.LayerIndex("Player")
        };

        // Los assets de la Fase 3 se crean ANTES de resolver: así los UpgradeDataSO nuevos
        // entran en el mismo barrido que el resto y aparecen en el panel de nivel.
        EnsureWeaponContent(context);
        ResolveAssets(context);
        BuildArena(context);
        BuildLighting(context);
        BuildCamera(context);
        // BuildPlayer va ANTES que BuildSystems a propósito: así el sistema de input
        // puede reutilizar el PlayerInputReader que ya viene en la jerarquía del Player
        // en vez de crear un duplicado que rompería el singleton.
        BuildPlayer(context);
        BuildWeapons(context);
        BuildSystems(context);
        BuildEnemySpawner(context);
        BuildGameplayUi(context);
        BuildDebugUi(context);
        WireWeapons(context);
        WireScene(context);

        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene, TestScenePath))
        {
            Debug.LogError($"[TestSceneBuilder] No se pudo guardar la escena en {TestScenePath}.");
            return;
        }

        RegisterInBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"<color=green>[TestSceneBuilder] Escena TEST creada en {TestScenePath}\n" +
            $"  Jugador: {context.PlayerSource}\n" +
            $"  Enemigo: {(context.EnemyPrefab != null ? context.EnemyPrefab.name : "NO ENCONTRADO")}\n" +
            $"  Mejoras de prueba: {context.Upgrades.Length}\n" +
            $"  Armas de prueba: {(context.TestWeapons != null ? context.TestWeapons.Length : 0)}\n" +
            $"  Atajos: F1-F3 hordas · F4 limpiar · F5 nivel · F6 mejora · F7 ataque · F8 IA · F9 spawner · F10 panel · F11 arma · F12 sistema · Supr reiniciar</color>",
            null);
    }

    /// <summary>
    /// Registra TEST en Build Settings habilitada. Hace falta porque
    /// <c>SceneManager.LoadScene</c> sólo funciona con escenas registradas, y el botón
    /// "reiniciar" del sandbox depende de ello.
    /// </summary>
    private static void RegisterInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == TestScenePath)
            {
                Debug.Log("[TestSceneBuilder] TEST ya estaba en Build Settings.");
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(TestScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.LogWarning(
            "[TestSceneBuilder] TEST se ha añadido a Build Settings (habilitada) para que el reinicio del " +
            "sandbox funcione. Recuerda quitarla con Tools/40 Days of Light/Quitar TEST de Build Settings " +
            "antes de construir la versión de producción.");
    }

    /// <summary>True si la escena de gameplay de referencia existe (fuente del fallback del Player).</summary>
    internal static bool PrototypeSceneExists => File.Exists(PrototypeScenePath);
}
