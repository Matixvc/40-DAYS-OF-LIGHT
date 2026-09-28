using UnityEngine;

/// <summary>
/// Regeneración de vida del jugador (Fase 2).
///
/// Vive en su propio componente en lugar de dentro de <see cref="HealthComponent"/> por una
/// razón de coste: <see cref="HealthComponent"/> lo llevan TAMBIÉN los enemigos, y añadirle un
/// Update supondría 100+ Updates por frame en una horda solo para que el jugador recupere vida.
/// Aquí hay exactamente un Update, y además solo hace trabajo si la regeneración es mayor que 0.
///
/// La cura va por ticks discretos (no por frame) para no disparar <c>OnHealthChanged</c>
/// sesenta veces por segundo, que arrastraría a la barra de vida y al HUD.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Player Regeneration")]
public class PlayerRegeneration : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RunStats runStats;
    [SerializeField] private HealthComponent health;

    [Header("Cadencia")]
    [Tooltip("Segundos entre aplicaciones de la regeneración.")]
    [SerializeField, Min(0.05f)] private float tickInterval = 0.5f;

    /// <summary>
    /// Regeneración por segundo cacheada en cada cambio de estadísticas. Sin este caché habría
    /// que evaluar el canal de StatType en cada Update.
    /// </summary>
    private float regenerationPerSecond;

    private float nextTickTime;

    private void Awake()
    {
        ResolveReferences(false);
    }

    private void OnEnable()
    {
        SubscribeToStats();
    }

    private void Start()
    {
        // En Start todos los Awake han terminado: momento seguro para resolver RunStats si
        // vive en otro GameObject (misma regla que PlayerController y WeaponController).
        ResolveReferences(true);
        SubscribeToStats();
        CacheRegeneration();
    }

    private void OnDisable()
    {
        if (runStats != null)
        {
            runStats.OnStatsChanged -= HandleStatsChanged;
        }
    }

    private void Update()
    {
        if (regenerationPerSecond <= 0f || health == null || health.IsDead)
        {
            return;
        }

        if (Time.time < nextTickTime)
        {
            return;
        }

        nextTickTime = Time.time + tickInterval;

        // Se cura la cantidad equivalente al intervalo, no la de un frame: así el ritmo de
        // vida por segundo es independiente del framerate.
        health.Heal(regenerationPerSecond * tickInterval);
    }

    private void ResolveReferences(bool logIfMissing)
    {
        if (runStats == null)
        {
            runStats = GetComponent<RunStats>();
        }

        if (runStats == null)
        {
            runStats = RunStats.Active;
        }

        if (health == null)
        {
            health = GetComponent<HealthComponent>();
        }

        if (logIfMissing && (runStats == null || health == null))
        {
            Debug.LogWarning(
                "[PlayerRegeneration] Faltan referencias (RunStats y/o HealthComponent): la " +
                "regeneración no se aplicará. Es inofensivo si ninguna mejora usa StatType.HealthRegen.",
                this);
        }
    }

    private void SubscribeToStats()
    {
        if (runStats == null)
        {
            return;
        }

        runStats.OnStatsChanged -= HandleStatsChanged;
        runStats.OnStatsChanged += HandleStatsChanged;
    }

    private void HandleStatsChanged()
    {
        CacheRegeneration();
    }

    private void CacheRegeneration()
    {
        regenerationPerSecond = runStats != null ? runStats.HealthRegenPerSecond : 0f;
    }
}
