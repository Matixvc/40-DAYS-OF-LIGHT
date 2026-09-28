using UnityEngine;

public static partial class TestSceneBuilder
{
    /// <summary>
    /// Crea el spawner de la arena. La densidad sube respecto a la partida real porque en
    /// un terreno de 40x40 con 100 enemigos vivos a la vez se puede medir de verdad el
    /// coste de CPU de la IA y del NavMesh.
    /// </summary>
    internal static void BuildEnemySpawner(BuildContext context)
    {
        GameObject spawnerGo = new GameObject("EnemySpawner");
        spawnerGo.transform.SetParent(context.SystemsRoot, false);

        context.Spawner = spawnerGo.AddComponent<EnemySpawner>();

        if (context.EnemyPrefab != null)
        {
            TestSceneBuilderUtil.SetValue(context.Spawner, "enemyPrefab", context.EnemyPrefab);
        }

        if (context.Player != null)
        {
            TestSceneBuilderUtil.SetValue(context.Spawner, "playerTransform", context.Player.transform);
        }

        // Arena de 40x40: el radio de spawn baja para que la horda sea visible de verdad.
        TestSceneBuilderUtil.SetValue(context.Spawner, "minSpawnDistance", 7f);
        TestSceneBuilderUtil.SetValue(context.Spawner, "maxSpawnDistance", 14f);
        TestSceneBuilderUtil.SetValue(context.Spawner, "initialMaxEnemies", 30);
        TestSceneBuilderUtil.SetValue(context.Spawner, "absoluteMaxEnemies", 150);
        TestSceneBuilderUtil.SetValue(context.Spawner, "logSpawnScaling", false);
    }
}
