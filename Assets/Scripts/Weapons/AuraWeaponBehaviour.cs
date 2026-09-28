using UnityEngine;

/// <summary>
/// Arquetipo Aura: zona de daño permanente alrededor del jugador.
///
/// No dispara: mantiene un aro visible y aplica daño a intervalos. Cada tick usa un único
/// <c>OverlapSphereNonAlloc</c> sobre arrays preasignados, así que el coste no depende del
/// número de armas ni del número de impactos, solo del número de enemigos en el radio.
/// </summary>
[AddComponentMenu("Gameplay/Weapons/Aura Weapon Behaviour")]
public class AuraWeaponBehaviour : WeaponBehaviourBase
{
    /// <summary>
    /// Aro del aura. Es UN solo objeto escalado: si el radio cambia con una mejora de área,
    /// basta con volver a escalarlo cada frame en lugar de reconstruir nada.
    /// </summary>
    private GameObject ringVisual;

    private float nextTickTime;

    /// <summary>Deduplicación por tick (un enemigo puede tener varios colliders).</summary>
    private readonly System.Collections.Generic.HashSet<HealthComponent> damagedThisTick =
        new System.Collections.Generic.HashSet<HealthComponent>();

    protected override void BuildVisuals()
    {
        if (Data == null)
        {
            return;
        }

        ringVisual = CreateVisual(Data.visualPrefab, Vector3.up * 0.05f, 1f);
        ApplyRingScale();

        nextTickTime = Time.time + Mathf.Max(0.05f, CurrentCooldown);
    }

    public override void SetLevel(int level)
    {
        base.SetLevel(level);
        nextTickTime = Mathf.Min(nextTickTime, Time.time);
    }

    public override void Tick(float deltaTime)
    {
        if (Data == null)
        {
            return;
        }

        // Barato (una multiplicación y un escalado): mantenerlo por frame hace que el aro
        // siga las mejoras de área al instante en lugar de solo en el siguiente tick.
        ApplyRingScale();

        if (Time.time < nextTickTime)
        {
            return;
        }

        nextTickTime = Time.time + Mathf.Max(0.05f, CurrentCooldown);

        ApplyTickDamage();
    }

    private void ApplyTickDamage()
    {
        float radius = ScaleArea(Data.auraRadius);

        int found = Physics.OverlapSphereNonAlloc(transform.position, radius, colliderBuffer, EnemyLayer);

        damagedThisTick.Clear();

        for (int i = 0; i < found; i++)
        {
            Collider candidate = colliderBuffer[i];

            if (candidate == null)
            {
                continue;
            }

            HealthComponent target = WeaponTargeting.ResolveTarget(candidate);

            if (!WeaponTargeting.IsValidTarget(target) || !damagedThisTick.Add(target))
            {
                continue;
            }

            WeaponTargeting.ApplyDamage(target, CurrentDamage, Stats, out _);
        }
    }

    /// <summary>Escala el aro al diámetro real del aura (que depende de las mejoras de área).</summary>
    private void ApplyRingScale()
    {
        if (ringVisual == null || Data == null)
        {
            return;
        }

        float diameter = ScaleArea(Data.auraRadius) * 2f;
        ringVisual.transform.localScale = new Vector3(diameter, 0.06f, diameter);
    }
}
