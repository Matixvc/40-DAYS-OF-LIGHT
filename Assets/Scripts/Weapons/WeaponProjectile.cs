using UnityEngine;

/// <summary>
/// Proyectil reciclable del arquetipo Projectile.
///
/// Se mueve y resuelve su propio impacto (sin Rigidbody ni triggers): el daño se decide con
/// <c>OverlapSphereNonAlloc</c> sobre un segmento, no con <c>OnTriggerEnter</c>. Motivos:
///   1) Los triggers exigirían configurar la matriz de colisión y las capas físicas, y bastaría
///      un cambio en Project Settings para que el arma dejara de hacer daño en silencio.
///   2) Un proyectil rápido atraviesa enemigos finos entre dos frames (tunneling). Aquí se
///      comprueba el SEGMENTO recorrido, ampliando el radio por el desplazamiento del frame.
///
/// Implementa <see cref="IPooledObject"/> para reiniciar su estado al reciclarse: un proyectil
/// reutilizado con la lista de "ya atravesados" sucia no volvería a golpear a nadie.
/// </summary>
[AddComponentMenu("Gameplay/Weapons/Weapon Projectile")]
public class WeaponProjectile : MonoBehaviour, IPooledObject
{
    /// <summary>Buffer de física reutilizado. 32 es holgado para un proyectil concreto.</summary>
    private const int HitBufferSize = 32;

    /// <summary>Máximo de enemigos atravesados que se recuerdan para no golpearlos dos veces.</summary>
    private const int MaxPierced = 8;

    [Header("Valores por defecto (el arma los sobrescribe al lanzar)")]
    [SerializeField] private float defaultSpeed = 16f;
    [SerializeField] private float defaultLifetime = 1.6f;
    [SerializeField] private float defaultHitRadius = 0.6f;

    private readonly Collider[] hitBuffer = new Collider[HitBufferSize];
    private readonly HealthComponent[] piercedTargets = new HealthComponent[MaxPierced];

    private Vector3 direction;
    private Vector3 previousPosition;
    private float speed;
    private float remainingLife;
    private float hitRadius;
    private float damage;
    private int pierceRemaining;
    private int piercedCount;
    private LayerMask enemyLayer;
    private RunStats stats;

    /// <summary>True cuando ya se devolvió al pool o se destruyó: evita dobles impactos.</summary>
    private bool isDespawned;

    /// <summary>
    /// Configura y lanza el proyectil. Se llama SIEMPRE después de <c>ObjectPoolManager.Spawn</c>,
    /// que ya lo ha colocado en su posición y rotación de salida.
    /// </summary>
    public void Launch(
        Vector3 travelDirection,
        float travelSpeed,
        float lifeSeconds,
        float impactRadius,
        float impactDamage,
        int maxPierce,
        LayerMask targetLayer,
        RunStats runStats)
    {
        direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : transform.forward;

        // Los valores por defecto del Inspector son el respaldo: si el arma no envía uno
        // (0 o negativo), el proyectil sigue teniendo un comportamiento razonable en lugar
        // de quedarse quieto. Así el prefab también se puede probar suelto desde el Editor.
        speed = Mathf.Max(0.1f, travelSpeed > 0f ? travelSpeed : defaultSpeed);
        remainingLife = Mathf.Max(0.05f, lifeSeconds > 0f ? lifeSeconds : defaultLifetime);
        hitRadius = Mathf.Max(0.05f, impactRadius > 0f ? impactRadius : defaultHitRadius);
        damage = impactDamage;
        pierceRemaining = Mathf.Max(1, maxPierce);
        enemyLayer = targetLayer;
        stats = runStats;

        piercedCount = 0;
        isDespawned = false;
        previousPosition = transform.position;

        // Orientar el proyectil hacia su dirección real: un prefab con forma alargada (una
        // lanza de luz) apuntaría al objetivo en lugar de a donde mirase el jugador.
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    public void OnPoolSpawned()
    {
        isDespawned = false;
        piercedCount = 0;
        previousPosition = transform.position;
    }

    public void OnPoolDespawned()
    {
        isDespawned = true;
        piercedCount = 0;
    }

    private void Update()
    {
        if (isDespawned)
        {
            return;
        }

        float step = speed * Time.deltaTime;

        previousPosition = transform.position;
        transform.position += direction * step;

        // El radio de comprobación se amplía con la mitad del paso para cubrir el segmento
        // completo: así un enemigo fino no se cuela entre dos frames.
        float sweepRadius = hitRadius + step * 0.5f;
        Vector3 sweepCenter = (previousPosition + transform.position) * 0.5f;

        CheckHits(sweepCenter, sweepRadius);

        if (isDespawned)
        {
            return;
        }

        remainingLife -= Time.deltaTime;

        if (remainingLife <= 0f)
        {
            DespawnSelf();
        }
    }

    private void CheckHits(Vector3 center, float radius)
    {
        int found = Physics.OverlapSphereNonAlloc(center, radius, hitBuffer, enemyLayer);

        for (int i = 0; i < found; i++)
        {
            Collider candidate = hitBuffer[i];

            if (candidate == null)
            {
                continue;
            }

            HealthComponent target = WeaponTargeting.ResolveTarget(candidate);

            if (!WeaponTargeting.IsValidTarget(target) || WasAlreadyPierced(target))
            {
                continue;
            }

            RememberPierced(target);
            WeaponTargeting.ApplyDamage(target, damage, stats, out _);

            pierceRemaining--;

            if (pierceRemaining <= 0)
            {
                DespawnSelf();
                return;
            }
        }
    }

    private bool WasAlreadyPierced(HealthComponent target)
    {
        for (int i = 0; i < piercedCount; i++)
        {
            if (piercedTargets[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private void RememberPierced(HealthComponent target)
    {
        // Con más enemigos que huecos se sobrescribe el más antiguo: aceptable porque el
        // proyectil se desvanece tras pocos impactos.
        int slot = piercedCount < piercedTargets.Length ? piercedCount++ : piercedTargets.Length - 1;
        piercedTargets[slot] = target;
    }

    /// <summary>
    /// Se devuelve al pool si pertenece a uno; si no, se destruye. Mismo patrón que
    /// <see cref="XPGem"/>: fuera de un pool el juego sigue funcionando, solo que con GC.
    /// </summary>
    private void DespawnSelf()
    {
        if (isDespawned)
        {
            return;
        }

        isDespawned = true;

        ObjectPoolManager pool = ObjectPoolManager.Instance;

        if (pool != null && pool.IsPooled(gameObject))
        {
            pool.Despawn(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Refuerzo de <see cref="OnPoolSpawned"/>: si alguien reactiva el objeto a mano desde el
    /// Editor, la lista de impactos no debe sobrevivir a la reactivación.
    /// </summary>
    private void OnDisable()
    {
        piercedCount = 0;
    }
}
