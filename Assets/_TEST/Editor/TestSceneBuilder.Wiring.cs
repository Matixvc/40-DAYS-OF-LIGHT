using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class TestSceneBuilder
{
    /// <summary>
    /// Última fase: enlazar todo lo que se construyó. Es aquí donde la copia del Player
    /// recupera las referencias que pointed a objetos de Prototype.unity y quedaron nulas
    /// al cerrar aquella escena.
    ///
    /// Nada de esto modifica código de producción: sólo se escriben campos privados
    /// [SerializeField] de la escena que se está generando.
    /// </summary>
    internal static void WireScene(BuildContext context)
    {
        WireRun(context);
        WirePlayer(context);
        WireGameplayUi(context);
        WireSandbox(context);
        Validate(context);
    }

    private static void WireRun(BuildContext context)
    {
        if (context.RunDirector != null)
        {
            TestSceneBuilderUtil.SetValue(context.RunDirector, "enemySpawner", context.Spawner);
        }

        if (context.PostProcessing != null && context.Camera != null)
        {
            TestSceneBuilderUtil.SetValue(context.PostProcessing, "targetCamera", context.Camera.GetComponent<Camera>());
        }
    }

    /// <summary>
    /// Recablea el Player. Al copiarlo desde Prototype, sus referencias a objetos de esa
    /// escena (LevelUpPanel, InputReader) quedaron nulas al cerrarla.
    /// </summary>
    private static void WirePlayer(BuildContext context)
    {
        if (context.Player == null)
        {
            Debug.LogError("[TestSceneBuilder] No hay Player: se omite el cableado del jugador.");
            return;
        }

        PlayerController controller = context.Player.GetComponent<PlayerController>();

        if (controller != null)
        {
            TestSceneBuilderUtil.SetValue(controller, "inputReader", context.InputReader);
            TestSceneBuilderUtil.SetValue(controller, "logStatsChanges", false);
        }

        PlayerLevelSystem levelSystem = context.Player.GetComponent<PlayerLevelSystem>();

        if (levelSystem != null)
        {
            TestSceneBuilderUtil.SetValue(levelSystem, "levelUpPanel", context.LevelUpPanel);
            TestSceneBuilderUtil.SetValue(levelSystem, "levelUpUI", context.LevelUpUi);
        }

        // El drop de XP y los números de daño apuntan a prefabs concretos: se fuerzan a
        // los resueltos para que el sandbox no dependa de cómo estuviera Prototype.
        HealthComponent health = context.Player.GetComponent<HealthComponent>();

        if (health != null)
        {
            if (context.XpPrefab != null)
            {
                TestSceneBuilderUtil.SetValue(health, "xpGemPrefab", context.XpPrefab);
            }

            if (context.DamageNumberPrefab != null)
            {
                TestSceneBuilderUtil.SetValue(health, "damageNumberPrefab", context.DamageNumberPrefab);
            }
        }

        if (context.Camera != null)
        {
            CameraFollow follow = context.Camera.GetComponent<CameraFollow>();

            if (follow != null)
            {
                TestSceneBuilderUtil.SetValue(follow, "target", context.Player.transform);
            }
        }
    }
}
