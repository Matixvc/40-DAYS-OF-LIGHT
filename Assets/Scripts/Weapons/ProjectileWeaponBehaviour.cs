using UnityEngine;

/// <summary>
/// Arquetipo Projectile: dispara una salva hacia los enemigos más cercanos.
///
/// Los proyectiles salen del <see cref="ObjectPoolManager"/>, nunca de <c>Instantiate</c>:
/// con 100 enemigos en pantalla y varias armas, crear y destruir decenas de objetos por
/// segundo es la causa número uno de tirones por GC en Android.
///
/// El número de proyectiles se lee EN CADA SALVA (no al equipar), así que las mejoras de
/// <see cref="StatType.IncreaseProjectileCount"/> surten efecto de inmediato.
/// </summary>
[AddComponentMenu("Gameplay/Weapons/Projectile Weapon Behaviour")]
public class ProjectileWeaponBehaviour : WeaponBehaviourBase
{
    /// <summary>Tope de proyectiles por salva, para que un asset mal configurado no hunda el pool.</summary>
    private const int MaxProjectilesPerVolley = 12;

    [Header("Disparo")]
    [Tooltip("Altura (local) desde la que salen los proyectiles.")]
    [SerializeField] private float muzzleHeight = 0.9f;
    [Tooltip("Radio de búsqueda de objetivos. Más allá de esto el arma no dispara.")]
    [SerializeField, Min(1f)] private float targetSearchRadius = 14f;
    [Tooltip("Altura a la que se apunta sobre el enemigo (centro del cuerpo, no los pies).")]
    [SerializeField] private float aimHeight = 1f;

    private float nextFireTime;
    private bool warnedMissingPrefab;
    private GameObject proceduralTemplate;

    protected override void BuildVisuals()
    {
        if (Data == null)
        {
            return;
        }

        // Marca visible del arma en reposo: sin ella un arma de proyectiles es invisible
        // hasta que dispara y parecería que no se ha equipado nada.
        CreateVisual(Data.visualPrefab, Vector3.up * muzzleHeight, Mathf.Max(0.1f, Data.visualScale * 0.6f));

        nextFireTime = Time.time + Mathf.Max(0.05f, CurrentCooldown);
    }

    public override void SetLevel(int level)
    {
        base.SetLevel(level);
        nextFireTime = Mathf.Min(nextFireTime, Time.time);
    }

    public override void Tick(float deltaTime)
    {
        if (Data == null || Time.time < nextFireTime)
        {
            return;
        }

        nextFireTime = Time.time + Mathf.Max(0.05f, CurrentCooldown);

        FireVolley();
    }

    private void FireVolley()
    {
        int projectileCount = Data.projectileCount + (Stats != null ? Stats.ProjectileCountBonus : 0);
        projectileCount = Mathf.Clamp(projectileCount, 1, MaxProjectilesPerVolley);

        int maxTargets = Mathf.Min(projectileCount, WeaponTargeting.MaxDistinctTargets);

        int found = WeaponTargeting.FindNearestTargets(
            transform.position,
            targetSearchRadius,
            EnemyLayer,
            colliderBuffer,
            targetBuffer,
            distanceBuffer,
            maxTargets);

        if (found == 0)
        {
            // Sin objetivos no se gasta nada del pool. La cadencia sigue corriendo: consultar
            // la física en cada frame para nada sería el peor de los dos mundos.
            return;
        }

        Vector3 muzzle = transform.position + Vector3.up * muzzleHeight;

        float lifetime = Data.projectileLifetime * (Stats != null ? Stats.DurationMultiplier : 1f);
        float damage = CurrentDamage;
        float step = projectileCount > 1 ? Data.projectileSpreadDegrees / (projectileCount - 1) : 0f;
        float center = (projectileCount - 1) * 0.5f;

        for (int i = 0; i < projectileCount; i++)
        {
            // Se reparten los objetivos en round-robin: con más proyectiles que enemigos,
            // varios impactan en el mismo y el resto recibe también su golpe.
            HealthComponent target = targetBuffer[i % found];

            Vector3 aimPoint = target != null ? target.transform.position + Vector3.up * aimHeight : muzzle + transform.forward;

            Vector3 baseDirection = aimPoint - muzzle;

            if (baseDirection.sqrMagnitude < 0.0001f)
            {
                baseDirection = transform.forward;
            }

            Vector3 direction = Quaternion.AngleAxis((i - center) * step, Vector3.up) * baseDirection.normalized;

            SpawnProjectile(muzzle, direction, lifetime, damage);
        }
    }

    /// <summary>
    /// Saca un proyectil del pool y lo lanza. Si el SO no trae prefab se usa una plantilla
    /// procedural: el <see cref="ObjectPoolManager"/> crea pools dinámicos para cualquier
    /// prefab que no esté en su lista, así que el fallback también sale reciclado y sin GC.
    /// </summary>
    private void SpawnProjectile(Vector3 position, Vector3 direction, float lifetime, float damage)
    {
        GameObject prefab = ResolveProjectilePrefab();

        if (prefab == null)
        {
            return;
        }

        Quaternion rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction, Vector3.up)
            : transform.rotation;

        ObjectPoolManager pool = ObjectPoolManager.Instance;

        GameObject instance = pool != null
            ? pool.Spawn(prefab, position, rotation)
            : Instantiate(prefab, position, rotation);

        if (instance == null)
        {
            return;
        }

        WeaponProjectile projectile = instance.GetComponentInChildren<WeaponProjectile>(true);

        if (projectile == null)
        {
            Debug.LogError(
                $"[ProjectileWeaponBehaviour] El prefab '{prefab.name}' no tiene WeaponProjectile: " +
                "no podrá aplicar daño. Revisa 'Projectile Prefab' en el WeaponDataSO.",
                prefab);
            return;
        }

        projectile.Launch(
            direction,
            Data.projectileSpeed,
            lifetime,
            Data.projectileHitRadius,
            damage,
            Data.projectilePierce,
            EnemyLayer,
            Stats);
    }

    private GameObject ResolveProjectilePrefab()
    {
        if (Data.projectilePrefab != null)
        {
            return Data.projectilePrefab;
        }

        if (!warnedMissingPrefab)
        {
            warnedMissingPrefab = true;

            Debug.LogWarning(
                $"[ProjectileWeaponBehaviour] El arma '{Data.weaponName}' no tiene 'Projectile Prefab': " +
                "se usa una esfera procedural reciclada desde el pool. Asígnalo para tener arte propio.",
                this);
        }

        if (proceduralTemplate == null)
        {
            proceduralTemplate = BuildProceduralTemplate();
        }

        return proceduralTemplate;
    }

    /// <summary>
    /// Plantilla de proyectil generada por código: una esfera sin collider con
    /// <see cref="WeaponProjectile"/>. Vive desactivada como hija del host y solo sirve de
    /// molde para el pool.
    /// </summary>
    private GameObject BuildProceduralTemplate()
    {
        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        template.name = "ProyectilProcedural";

        Collider templateCollider = template.GetComponent<Collider>();

        if (templateCollider != null)
        {
            Destroy(templateCollider);
        }

        template.AddComponent<WeaponProjectile>();
        template.transform.SetParent(transform, false);
        template.transform.localPosition = Vector3.zero;
        template.transform.localScale = Vector3.one * Mathf.Max(0.1f, Data.visualScale * 0.45f);

        TintVisual(template, Data.flashColor);

        template.SetActive(false);

        return template;
    }
}
