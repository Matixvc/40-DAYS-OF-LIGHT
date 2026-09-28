using UnityEngine;

/// <summary>
/// Arquetipo Orbit: varios objetos giran alrededor del jugador y golpean al contacto.
///
/// El daño por enemigo está limitado por <c>orbitHitInterval</c> con un registro de impactos
/// de capacidad FIJA. Un <c>Dictionary&lt;HealthComponent, float&gt;</c> sería lo más obvio,
/// pero cada entrada nueva reserva memoria: en una horda de 100 enemigos eso son 100
/// inserciones y un crecimiento que se dispara con cada oleada. El array con cursor circular
/// hace lo mismo en memoria constante y con una búsqueda lineal acotada.
/// </summary>
[AddComponentMenu("Gameplay/Weapons/Orbit Weapon Behaviour")]
public class OrbitWeaponBehaviour : WeaponBehaviourBase
{
    /// <summary>Impactos recientes recordados. 64 cubre de sobra 12 orbes contra varios enemigos.</summary>
    private const int HitRecordCapacity = 64;

    /// <summary>Máximo de orbes simultáneos, por seguridad ante un asset mal configurado.</summary>
    private const int MaxOrbs = 12;

    /// <summary>Registro de "a este enemigo ya le pegué hace poco".</summary>
    private struct HitRecord
    {
        public HealthComponent Target;
        public float NextHitTime;
    }

    private readonly HitRecord[] hitRecords = new HitRecord[HitRecordCapacity];
    private Transform[] orbs = new Transform[0];

    private float angleDegrees;
    private int nextRecordSlot;

    protected override void BuildVisuals()
    {
        if (Data == null)
        {
            return;
        }

        int count = Mathf.Clamp(Data.orbitCount, 1, MaxOrbs);

        orbs = new Transform[count];

        for (int i = 0; i < count; i++)
        {
            GameObject orb = CreateVisual(Data.visualPrefab, Vector3.zero, Mathf.Max(0.1f, Data.visualScale));

            if (orb == null)
            {
                continue;
            }

            orb.name = $"Orbe_{i:00}";
            orbs[i] = orb.transform;
        }

        // Se reparten desde el primer frame: sin esto, los orbes aparecerían apilados en el
        // mismo punto hasta el primer Tick.
        SpreadOrbs();
    }

    public override void Tick(float deltaTime)
    {
        if (orbs.Length == 0 || Data == null)
        {
            return;
        }

        RotateOrbs(deltaTime);
        CheckOrbHits();
    }

    /// <summary>Gira el conjunto de orbes y recoloca cada uno según el ángulo actual.</summary>
    private void RotateOrbs(float deltaTime)
    {
        float degreesPerSecond = Data.orbitDegreesPerSecond;

        if (Stats != null && Stats.CooldownMultiplier > 0f)
        {
            // Las mejoras de enfriamiento aceleran la órbita en la misma proporción que la
            // cadencia de las demás armas: una estadística, un comportamiento coherente.
            degreesPerSecond /= Stats.CooldownMultiplier;
        }

        angleDegrees = Mathf.Repeat(angleDegrees + degreesPerSecond * deltaTime, 360f);
        SpreadOrbs();
    }

    private void SpreadOrbs()
    {
        float radius = ScaleArea(Data.orbitRadius);
        float step = 360f / orbs.Length;

        for (int i = 0; i < orbs.Length; i++)
        {
            if (orbs[i] == null)
            {
                continue;
            }

            float rad = (angleDegrees + step * i) * Mathf.Deg2Rad;

            orbs[i].localPosition = new Vector3(Mathf.Cos(rad) * radius, 0.8f, Mathf.Sin(rad) * radius);
        }
    }

    /// <summary>
    /// Comprueba el solape de cada orbe contra la capa de enemigos. El buffer es el mismo para
    /// todos los orbes porque las comprobaciones son secuenciales, nunca anidadas.
    /// </summary>
    private void CheckOrbHits()
    {
        float orbRadius = Mathf.Max(0.1f, Data.orbitOrbRadius);
        float sqrOrbRadius = orbRadius * orbRadius;
        float hitInterval = Mathf.Max(0.05f, Data.orbitHitInterval);

        for (int o = 0; o < orbs.Length; o++)
        {
            if (orbs[o] == null)
            {
                continue;
            }

            Vector3 orbPosition = orbs[o].position;
            int found = Physics.OverlapSphereNonAlloc(orbPosition, orbRadius, colliderBuffer, EnemyLayer);

            for (int i = 0; i < found; i++)
            {
                Collider candidate = colliderBuffer[i];

                if (candidate == null)
                {
                    continue;
                }

                HealthComponent target = WeaponTargeting.ResolveTarget(candidate);

                if (!WeaponTargeting.IsValidTarget(target))
                {
                    continue;
                }

                // Comprobación por distancia real: OverlapSphere filtra por bounds, así que un
                // enemigo corpulento puede entrar en el radio sin estar realmente en contacto.
                if ((target.transform.position - orbPosition).sqrMagnitude > sqrOrbRadius)
                {
                    continue;
                }

                if (IsOnHitCooldown(target))
                {
                    continue;
                }

                RegisterHit(target, Time.time + hitInterval);
                WeaponTargeting.ApplyDamage(target, CurrentDamage, Stats, out _);
            }
        }
    }

    private bool IsOnHitCooldown(HealthComponent target)
    {
        for (int i = 0; i < hitRecords.Length; i++)
        {
            if (hitRecords[i].Target == target)
            {
                return Time.time < hitRecords[i].NextHitTime;
            }
        }

        return false;
    }

    /// <summary>Guarda el impacto en el registro circular (sobrescribe la entrada más antigua).</summary>
    private void RegisterHit(HealthComponent target, float nextHitTime)
    {
        for (int i = 0; i < hitRecords.Length; i++)
        {
            if (hitRecords[i].Target == target)
            {
                hitRecords[i].NextHitTime = nextHitTime;
                return;
            }
        }

        hitRecords[nextRecordSlot].Target = target;
        hitRecords[nextRecordSlot].NextHitTime = nextHitTime;
        nextRecordSlot = (nextRecordSlot + 1) % hitRecords.Length;
    }
}
