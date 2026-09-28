using UnityEngine;

/// <summary>
/// Utilidades compartidas por los comportamientos de arma.
///
/// REGLA DE ORO DE ESTA CLASE: no asignar memoria. Todos los métodos escriben en buffers que
/// el llamante posee y reutiliza, y usan <c>OverlapSphereNonAlloc</c> en lugar de
/// <c>OverlapSphere</c>. Con una horda de 100 enemigos y varias armas disparando a la vez,
/// una consulta que reservara memoria por frame provocaría un pico de GC apreciable en Android.
///
/// Los buffers son PARÁMETROS, no campos estáticos: un buffer estático compartido se rompe en
/// cuanto una consulta se anida dentro de otra (p. ej. un aura que golpea y dispara un efecto
/// que a su vez consulta enemigos). Cada comportamiento tiene el suyo.
/// </summary>
public static class WeaponTargeting
{
    /// <summary>Capacidad recomendada para los buffers de colisión (horda de 100 + jefes + colliders múltiples).</summary>
    public const int DefaultBufferSize = 128;

    /// <summary>Máximo de enemigos distintos a los que apuntar en un mismo disparo múltiple.</summary>
    public const int MaxDistinctTargets = 12;

    /// <summary>
    /// Resuelve el <see cref="HealthComponent"/> de un collider. Usa <c>GetComponentInParent</c>
    /// (genérico, sin boxing): los enemigos pueden tener más de un collider y el componente
    /// vive en la raíz.
    /// </summary>
    public static HealthComponent ResolveTarget(Collider collider)
    {
        return collider != null ? collider.GetComponentInParent<HealthComponent>() : null;
    }

    /// <summary>True si el objetivo existe y puede recibir daño.</summary>
    public static bool IsValidTarget(HealthComponent target)
    {
        return target != null && !target.IsDead;
    }

    /// <summary>
    /// Rellena <paramref name="buffer"/> con N enemigos distintos más cercanos a
    /// <paramref name="origin"/>, ordenados de menor a mayor distancia.
    ///
    /// Implementación: un único <c>OverlapSphereNonAlloc</c> seguido de una inserción ordenada
    /// sobre arrays preasignados (sin List, sin LINQ, sin closures). El coste está acotado por
    /// <paramref name="maxTargets"/>, no por el número de enemigos de la esfera.
    /// </summary>
    /// <returns>Cuántos objetivos válidos se escribieron (0 si no hay ninguno a tiro).</returns>
    public static int FindNearestTargets(
        Vector3 origin,
        float radius,
        LayerMask enemyLayer,
        Collider[] colliderBuffer,
        HealthComponent[] targetBuffer,
        float[] distanceBuffer,
        int maxTargets)
    {
        if (colliderBuffer == null || targetBuffer == null || distanceBuffer == null)
        {
            return 0;
        }

        int capacity = Mathf.Min(maxTargets, Mathf.Min(targetBuffer.Length, distanceBuffer.Length));

        if (capacity <= 0)
        {
            return 0;
        }

        int found = Physics.OverlapSphereNonAlloc(origin, radius, colliderBuffer, enemyLayer);
        int written = 0;

        for (int i = 0; i < found; i++)
        {
            Collider candidate = colliderBuffer[i];

            if (candidate == null)
            {
                continue;
            }

            HealthComponent target = ResolveTarget(candidate);

            if (!IsValidTarget(target) || ContainsTarget(targetBuffer, written, target))
            {
                continue;
            }

            float sqrDistance = (target.transform.position - origin).sqrMagnitude;

            // La lista está llena y este candidato está más lejos que el peor de los guardados.
            if (written == capacity && sqrDistance >= distanceBuffer[written - 1])
            {
                continue;
            }

            InsertSorted(targetBuffer, distanceBuffer, written, target, sqrDistance);

            if (written < capacity)
            {
                written++;
            }
        }

        return written;
    }

    /// <summary>Inserta (objetivo, distancia) manteniendo el orden ascendente por distancia.</summary>
    private static void InsertSorted(
        HealthComponent[] targets,
        float[] distances,
        int count,
        HealthComponent target,
        float sqrDistance)
    {
        int insertAt = count;

        // Si el array ya está lleno, 'count' apunta al hueco inexistente: se desplaza desde
        // count-1 y el último elemento se descarta al desplazarse.
        int last = Mathf.Min(count, targets.Length - 1);

        for (int i = 0; i < count; i++)
        {
            if (sqrDistance < distances[i])
            {
                insertAt = i;
                break;
            }
        }

        if (insertAt > last)
        {
            insertAt = last;
        }

        for (int i = last; i > insertAt; i--)
        {
            targets[i] = targets[i - 1];
            distances[i] = distances[i - 1];
        }

        targets[insertAt] = target;
        distances[insertAt] = sqrDistance;
    }

    private static bool ContainsTarget(HealthComponent[] targets, int count, HealthComponent target)
    {
        for (int i = 0; i < count; i++)
        {
            if (targets[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Aplica daño ya resuelto, contando el crítico si el llamante lo pidió.
    /// Centralizarlo aquí evita que cada arquetipo olvide el <c>IsDead</c> o el roll de crítico.
    /// </summary>
    public static void ApplyDamage(HealthComponent target, float amount, RunStats stats, out bool wasCritical)
    {
        wasCritical = false;

        if (!IsValidTarget(target))
        {
            return;
        }

        float finalDamage = stats != null ? stats.RollDamage(amount, out wasCritical) : amount;

        target.TakeDamage(finalDamage);
    }
}
