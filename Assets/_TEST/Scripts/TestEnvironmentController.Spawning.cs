using UnityEngine;
using UnityEngine.AI;

public partial class TestEnvironmentController
{
    /// <summary>Tamaños de horda que usa el panel (F1/F2/F3).</summary>
    public const int SmallHorde = 10;
    public const int MediumHorde = 50;
    public const int LargeHorde = 100;

    public void SpawnHordeSmall() => SpawnHorde(SmallHorde);
    public void SpawnHordeMedium() => SpawnHorde(MediumHorde);
    public void SpawnHordeLarge() => SpawnHorde(LargeHorde);

    /// <summary>
    /// Genera una horda de <paramref name="count"/> enemigos alrededor del jugador.
    /// Reutiliza el pool del proyecto: en el modo de 100 unidades esto valida que el
    /// prewarm y el límite del pool estén bien configurados.
    /// </summary>
    public void SpawnHorde(int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (!CanSpawnHorde())
        {
            if (logActions)
            {
                Debug.LogWarning(
                    "[TestEnvironmentController] Horda cancelada: falta el prefab enemigo, el jugador o el NavMesh todavía no está horneado.",
                    this);
            }

            return;
        }

        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            if (SpawnOne())
            {
                spawned++;
            }
        }

        if (logActions)
        {
            Debug.Log($"<color=orange>[TestEnvironmentController] Horda solicitada: {count} | generadas: {spawned}</color>", this);
        }
    }

    /// <summary>Genera un único enemigo en una posición válida del NavMesh.</summary>
    private bool SpawnOne()
    {
        if (!TryGetSpawnPoint(out Vector3 position))
        {
            return false;
        }

        ObjectPoolManager pool = poolManager != null ? poolManager : ObjectPoolManager.Instance;

        GameObject instance = pool != null
            ? pool.Spawn(enemyPrefab, position, Quaternion.identity)
            : Instantiate(enemyPrefab, position, Quaternion.identity);

        if (instance == null)
        {
            return false;
        }

        ConfigureTestEnemy(instance);
        return true;
    }

    /// <summary>
    /// Réplica de <c>EnemySpawner.ConfigureSpawnedEnemy</c> usando solo su API pública.
    /// Vive aquí, y no en el spawner de producción, a propósito: el sandbox no altera el
    /// juego real y se puede retirar borrando una carpeta.
    /// </summary>
    private void ConfigureTestEnemy(GameObject instance)
    {
        EnemyAI ai = instance.GetComponentInChildren<EnemyAI>(true);
        HealthComponent health = instance.GetComponentInChildren<HealthComponent>(true);

        if (ai != null)
        {
            // Orden idéntico al del spawner: primero se limpia el estado de la instancia
            // reciclada, después se asigna objetivo y escalado.
            ai.ResetEnemyState();

            if (spawnCenter != null)
            {
                ai.SetTarget(spawnCenter);
            }

            if (applyScaling)
            {
                ai.ApplySpawnScaling(hordeDamageMultiplier, hordeSpeedMultiplier);
            }

            if (!testSpawns.Contains(ai))
            {
                testSpawns.Add(ai);
            }
        }
        else
        {
            Debug.LogError("[TestEnvironmentController] El prefab enemigo no tiene EnemyAI.", instance);
        }

        if (health != null)
        {
            if (applyScaling)
            {
                health.ApplyHealthScaling(hordeHealthMultiplier);
            }
        }
        else
        {
            Debug.LogError("[TestEnvironmentController] El prefab enemigo no tiene HealthComponent.", instance);
        }
    }

    /// <summary>
    /// Sortea una posición alrededor del centro y la proyecta sobre el NavMesh.
    /// Se reintenta porque el punto puede caer fuera de la malla o sobre un obstáculo.
    /// </summary>
    private bool TryGetSpawnPoint(out Vector3 result)
    {
        result = default;

        if (spawnCenter == null)
        {
            return false;
        }

        for (int attempt = 0; attempt < navMeshSampleAttempts; attempt++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle;

            // insideUnitCircle puede devolver el vector cero: normalizarlo daría NaN.
            if (circle.sqrMagnitude < 0.0001f)
            {
                circle = Vector2.up;
            }
            else
            {
                circle.Normalize();
            }

            float distance = UnityEngine.Random.Range(spawnMinDistance, spawnMaxDistance);
            Vector3 candidate = spawnCenter.position + new Vector3(circle.x, 0f, circle.y) * distance;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        return false;
    }
}
