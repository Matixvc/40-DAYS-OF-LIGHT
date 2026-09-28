using UnityEngine;

public partial class TestEnvironmentController
{
    /// <summary>
    /// Recicla al pool SOLO los enemigos que generó el sandbox, sin otorgar XP.
    /// Es la limpieza correcta para medir rendimiento: no deja gemas ni números de daño
    /// en pantalla, así que el siguiente spawn parte de una escena realmente limpia.
    /// </summary>
    public void ClearTestSpawns()
    {
        PruneTestSpawns();

        int count = testSpawns.Count;
        ObjectPoolManager pool = poolManager != null ? poolManager : ObjectPoolManager.Instance;

        for (int i = 0; i < testSpawns.Count; i++)
        {
            EnemyAI ai = testSpawns[i];

            if (ai == null)
            {
                continue;
            }

            if (pool != null && pool.IsPooled(ai.gameObject))
            {
                pool.Despawn(ai.gameObject);
            }
            else
            {
                Destroy(ai.gameObject);
            }
        }

        testSpawns.Clear();

        if (logActions)
        {
            Debug.Log($"<color=orange>[TestEnvironmentController] Horda de prueba limpiada: {count} enemigos.</color>", this);
        }
    }

    /// <summary>
    /// Mata por daño TODO enemigo vivo dentro del radio indicado. A diferencia de
    /// <see cref="ClearTestSpawns"/>, ésta SÍ recorre el ciclo de muerte completo:
    /// sirve para probar drops de XP, audio de impacto y números de daño.
    /// </summary>
    public void KillEnemiesInRadius(float radius)
    {
        if (radius <= 0f)
        {
            return;
        }

        Vector3 center = spawnCenter != null ? spawnCenter.position : Vector3.zero;
        Collider[] buffer = Physics.OverlapSphere(center, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        int killed = 0;

        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == null)
            {
                continue;
            }

            HealthComponent health = buffer[i].GetComponentInParent<HealthComponent>();

            if (health == null || health.IsDead || health.CompareTag("Player"))
            {
                continue;
            }

            health.TakeDamage(health.MaxHealth * 10f);
            killed++;
        }

        if (logActions)
        {
            Debug.Log($"<color=orange>[TestEnvironmentController] {killed} enemigos eliminados en radio {radius:0.#}.</color>", this);
        }
    }

    /// <summary>
    /// Vacía TODOS los pools (enemigos, gemas de XP, números de daño). Limpieza total
    /// del sandbox, no una muerte: nada otorga recompensas ni dispara el ciclo de muerte.
    /// </summary>
    public void ClearEverything()
    {
        testSpawns.Clear();

        ObjectPoolManager pool = poolManager != null ? poolManager : ObjectPoolManager.Instance;

        if (pool == null)
        {
            if (logActions)
            {
                Debug.LogWarning("[TestEnvironmentController] No hay ObjectPoolManager: nada que limpiar.", this);
            }

            return;
        }

        pool.ReleaseAll();

        if (logActions)
        {
            Debug.Log("[TestEnvironmentController] Todos los pools devueltos.", this);
        }
    }
}
